using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bing.DynamicApi.Contracts;
using Bing.Resilience;
using Bing.Resilience.Discovery;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DynamicApi.Client.Resilience;

/// <summary>
/// 支持并行竞速的动态 API 描述来源。
/// </summary>
/// <remarks>
/// 专用描述传输完整竞速；缓存及下载生命周期属于宿主，不属于首个等待者。
/// </remarks>
internal sealed class HedgedDefinitionSource : IBingDynamicApiDefinitionSource, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 发布定义恢复诊断指标的计量器。
    /// </summary>
    private static readonly Meter Meter = new("Bing.Resilience");
    /// <summary>
    /// 统计对冲定义恢复分支的计数器。
    /// </summary>
    private static readonly Counter<long> Branches = Meter.CreateCounter<long>("bing.resilience.definition.branches");
    /// <summary>
    /// 保存定义恢复注册信息，用于按类型创建恢复策略。
    /// </summary>
    private readonly DefinitionRecoveryRegistration _registration;
    /// <summary>
    /// 提供创建恢复请求所用 HTTP 客户端的工厂。
    /// </summary>
    private readonly IHttpClientFactory _clients;
    /// <summary>
    /// 提供按名称解析弹性管道的服务。
    /// </summary>
    private readonly IResiliencePipelineProvider _pipelines;
    /// <summary>
    /// 创建每次恢复尝试使用的依赖注入作用域。
    /// </summary>
    private readonly IServiceScopeFactory _scopes;
    /// <summary>
    /// 提供可选的服务端点发现来源。
    /// </summary>
    private readonly IServiceEndpointSource? _discovery;
    /// <summary>
    /// 提供可选的端点清单来源。
    /// </summary>
    private readonly IEndpointManifestSource? _manifests;
    /// <summary>
    /// 提供定义恢复使用的时间。
    /// </summary>
    private readonly TimeProvider _clock;
    /// <summary>
    /// 发出定义源关闭时的取消信号。
    /// </summary>
    private readonly CancellationTokenSource _shutdown = new();
    /// <summary>
    /// 同步定义缓存的并发读写。
    /// </summary>
    private readonly object _gate = new();
    /// <summary>
    /// 缓存按服务键解析的定义，键使用序号比较。
    /// </summary>
    private readonly Dictionary<string, Entry> _cache = new(StringComparer.Ordinal);
    /// <summary>
    /// 指示定义源是否已释放。
    /// </summary>
    private bool _disposed;
    /// <summary>
    /// 共享描述下载任务的缓存条目。
    /// </summary>
    /// <param name="Task">共享的描述下载任务。</param>
    /// <param name="Expires">缓存条目的到期时间。</param>
    private sealed record Entry(Task<BingDynamicApiDefinition> Task, DateTimeOffset Expires);
    /// <summary>
    /// 描述恢复使用的候选端点快照。
    /// </summary>
    /// <param name="Endpoints">允许使用的候选端点集合。</param>
    /// <param name="Version">候选端点集合的来源版本。</param>
    /// <param name="Digest">预期内容摘要。</param>
    /// <param name="Discovery">服务发现提供的端点快照。</param>
    /// <param name="Manifest">发布的服务端点清单。</param>
    private sealed record Candidates(IReadOnlyList<BingDefinitionEndpoint> Endpoints, string Version, string? Digest,
        ServiceEndpointSnapshot? Discovery, EndpointManifest? Manifest);
    /// <summary>
    /// 初始化一个 <see cref="HedgedDefinitionSource" /> 类型的实例。
    /// </summary>
    /// <param name="registration">用于配置当前远程描述源的注册信息。</param>
    /// <param name="clients">用于创建 HTTP 客户端的工厂。</param>
    /// <param name="pipelines">用于解析恢复管道的提供程序。</param>
    /// <param name="scopes">用于创建依赖注入作用域的工厂。</param>
    /// <param name="clock">用于提供当前时间的时钟。</param>
    /// <param name="discovery">用于发现服务端点的来源。</param>
    /// <param name="manifests">用于提供服务端点清单的来源。</param>
    internal HedgedDefinitionSource(DefinitionRecoveryRegistration registration, IHttpClientFactory clients, IResiliencePipelineProvider pipelines,
        IServiceScopeFactory scopes, TimeProvider clock, IServiceEndpointSource? discovery, IEndpointManifestSource? manifests)
    {
        _registration = registration; _clients = clients; _pipelines = pipelines; _scopes = scopes; _clock = clock; _discovery = discovery; _manifests = manifests;
        if (registration.Options.UseDiscoveryBootstrap && discovery is null || registration.Options.UseManifestCandidates && manifests is null)
            throw new ResilienceConfigurationException("描述源缺少显式启用的 Discovery/Manifest 依赖。");
    }
    /// <inheritdoc />
    public string RemoteName => _registration.RemoteName;
    /// <inheritdoc />
    public async ValueTask<BingDynamicApiDefinition> GetAsync(BingDynamicApiDefinitionRequest request, CancellationToken cancellationToken)
    {
        var options = _registration.Options;
        var started = _clock.GetTimestamp();
        var captured = options.CaptureAccess!(request);
        if (captured is null || string.IsNullOrWhiteSpace(captured.Scope) || captured.Scope.Length > 512 ||
            string.IsNullOrWhiteSpace(captured.CredentialAudience) || captured.CredentialAudience.Length > 128)
            throw new ResilienceConfigurationException("描述访问范围和凭据受众不能为空。");
        var access = captured with { Properties = new ReadOnlyDictionary<string, string>(captured.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)) };
        using var preparing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        using var timer = _clock.CreateTimer(_ => { try { preparing.Cancel(); } catch (ObjectDisposedException) { } }, null, _registration.Timeout, Timeout.InfiniteTimeSpan);
        Candidates candidates;
        try { candidates = await GetCandidates(request, access, preparing.Token).ConfigureAwait(false); }
        catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested && !_shutdown.IsCancellationRequested)
        { throw new TimeoutException($"远程服务 '{RemoteName}' 的描述下载超时。", error); }
        var key = Hash(string.Join("", new[] { request.HttpClientName, options.ServiceName, request.ApiVersion, options.ContractRevision,
            options.CompatibilityGroup, access.Scope, access.CredentialAudience, candidates.Version, candidates.Digest ?? "" }.Select(x => x.Length + ":" + x)));
        Entry entry;
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HedgedDefinitionSource));
            foreach (var old in _cache.Where(x => x.Value.Task.IsFaulted || x.Value.Task.IsCanceled || x.Value.Expires <= _clock.GetUtcNow() && x.Value.Task.IsCompleted).ToArray()) _cache.Remove(old.Key);
            if (!_cache.TryGetValue(key, out entry!))
            {
                if (_cache.Count >= options.MaxCachedDefinitions) throw new ResilienceConfigurationException("描述缓存分区数量达到上限。");
                var remaining = _registration.Timeout - _clock.GetElapsedTime(started);
                entry = new(Task.Run(() => Fetch(request, access, candidates, remaining)), _clock.GetUtcNow() + options.CacheLifetime);
                _cache.Add(key, entry);
                _ = entry.Task.ContinueWith(_ => RemoveFailure(key, entry), CancellationToken.None, TaskContinuationOptions.NotOnRanToCompletion, TaskScheduler.Default);
            }
        }
        return await entry.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
    /// <summary>
    /// 仅当缓存条目仍是指定对象时移除失败项。
    /// </summary>
    /// <param name="key">定义缓存键。</param>
    /// <param name="entry">触发移除的缓存条目。</param>
    /// <remarks>
    /// 按引用身份核对当前条目，避免迟到的失败清除同键的新下载。
    /// </remarks>
    private void RemoveFailure(string key, Entry entry)
    {
        lock (_gate) if (_cache.TryGetValue(key, out var current) && ReferenceEquals(current, entry)) _cache.Remove(key);
    }
    /// <summary>
    /// 从配置、服务发现和签名清单生成可信候选端点快照。
    /// </summary>
    /// <param name="request">当前动态 API 定义请求。</param>
    /// <param name="access">当前请求的访问范围及凭据受众。</param>
    /// <param name="token">用于取消发现和清单读取的令牌。</param>
    /// <returns>通过契约和信任校验的候选端点、来源快照及内容摘要。</returns>
    /// <remarks>
    /// 校验服务契约版本、兼容组、清单有效期、内容摘要、端点信任和凭据受众；没有可信候选时抛出 DefinitionTrustException。
    /// </remarks>
    private async ValueTask<Candidates> GetCandidates(BingDynamicApiDefinitionRequest request, BingDefinitionAccess access, CancellationToken token)
    {
        var options = _registration.Options;
        var endpoints = options.FrozenEndpoints;
        ServiceEndpointSnapshot? snapshot = null;
        EndpointManifest? manifest = null;
        var version = "static";
        string? digest = options.ExpectedDigest;
        if (options.UseDiscoveryBootstrap)
        {
            snapshot = await _discovery!.GetSnapshotAsync(options.ServiceName, token).ConfigureAwait(false);
            var path = new Uri(request.BaseUrl.TrimEnd('/') + "/api/dynamic-api/" + Uri.EscapeDataString(request.ApiVersion) + "/definition").PathAndQuery;
            endpoints = snapshot.Endpoints.Where(e => e.ContractRevision == options.ContractRevision && e.ConsistencyGroup == options.CompatibilityGroup &&
                e.CredentialAudience == access.CredentialAudience && !_discovery.IsRevoked(options.ServiceName, e))
                .Select(e => new BingDefinitionEndpoint(e.Id, new Uri(e.Origin.GetLeftPart(UriPartial.Authority) + path))).ToArray();
            version = snapshot.SourceVersion;
        }
        if (options.UseManifestCandidates)
        {
            manifest = await _manifests!.GetAsync(options.ServiceName, token).ConfigureAwait(false);
            if (manifest.ServiceName != options.ServiceName || manifest.ContractRevision != options.ContractRevision ||
                manifest.ConsistencyGroup != options.CompatibilityGroup || manifest.ValidUntil <= _clock.GetUtcNow())
                throw new DefinitionTrustException("描述 Manifest 契约或有效期不匹配。");
            endpoints = endpoints.Where(e => manifest.Endpoints.Any(m => m.Id == e.Id && SameOrigin(m.Origin, e.Address) &&
                m.CredentialAudience == access.CredentialAudience)).ToArray();
            version += ":" + manifest.Revision;
            if (digest is not null && !digest.Equals(manifest.Digest, StringComparison.OrdinalIgnoreCase))
                throw new DefinitionTrustException("Manifest digest 与宿主固定值不一致。");
            digest = manifest.Digest;
        }
        if (endpoints.Count == 0 || endpoints.Any(e => !BingDynamicApiDefinitionRecoveryOptions.ValidAddress(e.Address) || options.TrustEndpoint!(e.Address) != true))
            throw new DefinitionTrustException("没有可信的描述 bootstrap 候选。");
        return new(Array.AsReadOnly(endpoints.ToArray()), version, digest, snapshot, manifest);
    }
    /// <summary>
    /// 在定义恢复管线和剩余时间预算内执行端点请求。
    /// </summary>
    /// <param name="request">待获取描述的请求。</param>
    /// <param name="access">当前请求的访问上下文。</param>
    /// <param name="candidates">已验证的候选端点及来源快照。</param>
    /// <param name="remaining">当前下载剩余时间预算。</param>
    /// <returns>经恢复管线获取并验证的动态 API 定义。</returns>
    /// <remarks>
    /// 在重试或对冲尝试前等待 Retry-After；候选撤销或过期时终止；预算耗尽时抛出 TimeoutException。
    /// </remarks>
    private async Task<BingDynamicApiDefinition> Fetch(BingDynamicApiDefinitionRequest request, BingDefinitionAccess access, Candidates candidates, TimeSpan remaining)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        using var timer = _clock.CreateTimer(_ => { try { lifetime.Cancel(); } catch (ObjectDisposedException) { } }, null, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        var state = new DefinitionAttemptState(_clock);
        var context = new ResilienceExecutionContext();
        context.Set(DefinitionAttemptState.Key, state);
        try
        {
            return await _pipelines.GetPipeline<BingDynamicApiDefinition>(_registration.TransportName, "definition.fetch").ExecuteAsync(
                async (attempt, token) =>
                {
                    await state.WaitAsync(token).ConfigureAwait(false);
                    var endpoint = candidates.Endpoints[attempt.AttemptNumber % candidates.Endpoints.Count];
                    if (candidates.Manifest?.ValidUntil <= _clock.GetUtcNow() ||
                        candidates.Discovery is { } snapshot && (snapshot.MaxStaleUntil <= _clock.GetUtcNow() ||
                            snapshot.Endpoints.Any(e => e.Id == endpoint.Id && _discovery!.IsRevoked(_registration.Options.ServiceName, e))))
                        throw new DefinitionTrustException("描述候选已过期或撤销。");
                    return await FetchBranch(request, access, endpoint, candidates.Digest, state, token).ConfigureAwait(false);
                }, context, lifetime.Token).ConfigureAwait(false);
        }
        catch (ResilienceTimeoutException error) { throw new TimeoutException($"远程服务 '{RemoteName}' 的描述下载超时。", error); }
        catch (OperationCanceledException error) when (!_shutdown.IsCancellationRequested)
        { throw new TimeoutException($"远程服务 '{RemoteName}' 的描述下载超时。", error); }
    }
    /// <summary>
    /// 请求候选端点并验证远程描述响应。
    /// </summary>
    /// <param name="descriptor">待获取定义的请求描述。</param>
    /// <param name="access">当前请求的访问上下文。</param>
    /// <param name="endpoint">已验证的远程定义端点。</param>
    /// <param name="digest">期望的内容摘要；未配置时为 null。</param>
    /// <param name="state">记录服务端 Retry-After 的尝试状态。</param>
    /// <param name="token">用于取消请求和读取响应的令牌。</param>
    /// <returns>通过响应身份、摘要、大小和契约校验的动态 API 定义。</returns>
    /// <remarks>
    /// 每个分支使用独立依赖注入作用域；请求准备程序不得改写方法、地址或请求体。响应须通过身份、摘要、大小与定义契约校验。
    /// </remarks>
    private async ValueTask<BingDynamicApiDefinition> FetchBranch(BingDynamicApiDefinitionRequest descriptor, BingDefinitionAccess access,
        BingDefinitionEndpoint endpoint, string? digest, DefinitionAttemptState state, CancellationToken token)
    {
        var options = _registration.Options;
        await using var scope = _scopes.CreateAsyncScope();
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint.Address);
        request.Options.Set(new HttpRequestOptionsKey<bool>("Bing.Resilience.IsDefinition"), true);
        if (options.PrepareRequestAsync is { } prepare) await prepare(scope.ServiceProvider, access, request, token).ConfigureAwait(false);
        if (request.Method != HttpMethod.Get || request.Content is not null || request.RequestUri != endpoint.Address)
            throw new DefinitionTrustException("认证/签名处理不能改变描述候选地址、方法或请求体。");
        using var client = _clients.CreateClient(_registration.TransportName);
        var failureKind = "transport";
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new DefinitionTrustException("描述访问被拒绝。");
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 408 or 429 or 500 or 502 or 503 or 504)
                {
                    state.Observe(response);
                    throw new DefinitionCandidateException("描述端点暂时不可用。");
                }
                throw new DefinitionTrustException("描述端点返回不可恢复状态。");
            }
            if (options.ValidateIdentity!(response, access) != true) throw new DefinitionTrustException("描述服务身份、契约或访问范围不匹配。");
            failureKind = "contract_invalid";
            if (response.Content.Headers.ContentLength > options.MaxWireBytes) throw new DefinitionCandidateException("描述传输体超过上限。");
            await using var wire = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using var bounded = new BoundedReadStream(wire, options.MaxWireBytes);
            var encodings = response.Content.Headers.ContentEncoding.ToArray();
            if (encodings.Length > 1) throw new DefinitionCandidateException("不支持多层描述压缩。");
            await using Stream decoded = encodings.FirstOrDefault()?.ToLowerInvariant() switch
            {
                null or "identity" => new BoundedReadStream(bounded, options.MaxDecodedBytes),
                "gzip" => new GZipStream(bounded, CompressionMode.Decompress, true),
                "deflate" => new DeflateStream(bounded, CompressionMode.Decompress, true),
                "br" => new BrotliStream(bounded, CompressionMode.Decompress, true),
                _ => throw new DefinitionCandidateException("不支持的描述压缩。")
            };
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int count;
            while ((count = await decoded.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
            {
                if (output.Length + count > options.MaxDecodedBytes) throw new DefinitionCandidateException("描述解压体超过上限。");
                await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            }
            var bytes = output.ToArray();
            if (options.VerifySignature is not null && !options.VerifySignature(bytes)) throw new DefinitionTrustException("描述签名校验失败。");
            if (digest is not null && !Convert.ToHexString(SHA256.HashData(bytes)).Equals(digest, StringComparison.OrdinalIgnoreCase))
                throw new DefinitionCandidateException("描述契约 digest 不匹配。");
            var json = new JsonSerializerOptions(descriptor.JsonOptions) { MaxDepth = options.MaxJsonDepth };
            var definition = JsonSerializer.Deserialize<BingDynamicApiDefinition>(bytes, json);
            if (definition is null || definition.SchemaVersion != 1 || !string.Equals(definition.ApiVersion, descriptor.ApiVersion, StringComparison.OrdinalIgnoreCase) ||
                definition.Services is null || definition.Services.Count > options.MaxServices ||
                definition.Services.Any(s => s is null || string.IsNullOrWhiteSpace(s.ServiceName) || string.IsNullOrWhiteSpace(s.ServiceTypeName) || s.Methods is null) ||
                definition.Services.Sum(s => (long)s.Methods.Count) > options.MaxMethods ||
                definition.Services.Select(s => s.ServiceTypeName).Distinct(StringComparer.Ordinal).Count() != definition.Services.Count ||
                definition.Services.SelectMany(s => s.Methods).Any(m => m is null || string.IsNullOrWhiteSpace(m.MethodName) ||
                    string.IsNullOrWhiteSpace(m.HttpMethod) || string.IsNullOrWhiteSpace(m.RouteTemplate) || m.RouteTemplate.StartsWith("//", StringComparison.Ordinal) ||
                    Uri.TryCreate(m.RouteTemplate, UriKind.Absolute, out _) || m.Parameters is null || m.Parameters.Count > 128 ||
                    m.Parameters.Any(p => p is null || string.IsNullOrWhiteSpace(p.Name) || p.Position < 0)) ||
                options.ValidateDefinition?.Invoke(definition) == false)
                throw new DefinitionCandidateException("描述结构或契约无效。");
            token.ThrowIfCancellationRequested();
            Record(endpoint.Id, "validated");
            return definition;
        }
        catch (DefinitionTrustException) { Record(endpoint.Id, "trust_rejected"); throw; }
        catch (DefinitionCandidateException) { Record(endpoint.Id, failureKind); throw; }
        catch (OperationCanceledException) { Record(endpoint.Id, "cancelled"); throw; }
        catch (HttpRequestException error) { Record(endpoint.Id, "transport"); throw new DefinitionCandidateException("描述网络故障。", error); }
        catch (JsonException error) { Record(endpoint.Id, "contract_invalid"); throw new DefinitionCandidateException("描述 JSON 无效。", error); }
        catch (InvalidDataException error) { Record(endpoint.Id, "contract_invalid"); throw new DefinitionCandidateException("描述压缩内容无效。", error); }
        catch (IOException error) { Record(endpoint.Id, "transport"); throw new DefinitionCandidateException("描述正文读取失败。", error); }
    }
    /// <summary>
    /// 记录远程定义请求分支的结果指标。
    /// </summary>
    /// <param name="endpoint">请求所用端点标识。</param>
    /// <param name="outcome">分支结果分类。</param>
    /// <remarks>
    /// 指标写入失败不会改变已确定的定义结果。
    /// </remarks>
    private void Record(string endpoint, string outcome)
    {
        try { Branches.Add(1, new KeyValuePair<string, object?>("client", _registration.TransportName),
            new KeyValuePair<string, object?>("endpoint", endpoint), new KeyValuePair<string, object?>("outcome", outcome)); }
        catch { /* 指标订阅不能改变已确定的定义结果。 */ }
    }
    /// <summary>
    /// 计算字符串 UTF-8 内容的 SHA-256 摘要。
    /// </summary>
    /// <param name="value">待计算摘要的文本。</param>
    /// <returns>UTF-8 内容的 SHA-256 大写十六进制摘要。</returns>
    /// <remarks>
    /// 摘要使用大写十六进制字符串表示。
    /// </remarks>
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    /// <summary>
    /// 判断两个 URI 是否具有相同的来源地址。
    /// </summary>
    /// <param name="first">第一个 URI。</param>
    /// <param name="second">第二个 URI。</param>
    /// <returns>两个 URI 的协议、主机和端口相同时返回 true，否则返回 false。</returns>
    /// <remarks>
    /// 比较协议、主机和端口，不区分大小写；相同来源时返回 true，否则返回 false。
    /// </remarks>
    private static bool SameOrigin(Uri first, Uri second) => string.Equals(first.GetLeftPart(UriPartial.Authority), second.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    /// <inheritdoc />
    /// <remarks>
    /// 标记描述源已释放，取消宿主拥有的共享下载并释放关闭令牌源。
    /// </remarks>
    public void Dispose()
    {
        Task[] active;
        lock (_gate) { if (_disposed) return; _disposed = true; active = _cache.Values.Select(x => x.Task).ToArray(); _cache.Clear(); }
        _shutdown.Cancel();
        _ = Task.WhenAll(active).ContinueWith(_ => _shutdown.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
    /// <inheritdoc />
    /// <remarks>
    /// 先发起同步释放，再等待已缓存的共享下载退出；下载已完成后的原始失败不在释放阶段重复抛出。
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        Task[] active;
        lock (_gate) active = _cache.Values.Select(x => x.Task).ToArray();
        Dispose();
        try { await Task.WhenAll(active).ConfigureAwait(false); }
        catch (Exception) when (active.All(x => x.IsCompleted)) { /* 调用者已收到原始失败，释放只等待资源退出。 */ }
    }
    /// <summary>
    /// 限制动态 API 定义响应体的读取长度。
    /// </summary>
    /// <param name="inner">包装或读取的内部对象。</param>
    /// <param name="limit">当前操作允许的上限。</param>
    private sealed class BoundedReadStream(Stream inner, long limit) : Stream
    {
        /// <summary>
        /// 记录已从内部流读取的字节数。
        /// </summary>
        private long _read;
        /// <inheritdoc />
        public override bool CanRead => inner.CanRead;
        /// <inheritdoc />
        public override bool CanSeek => false;
        /// <inheritdoc />
        public override bool CanWrite => false;
        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();
        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <summary>
        /// 累计包装流已读取的字节数并执行大小限制。
        /// </summary>
        /// <param name="count">本次读取的字节数。</param>
        /// <returns>本次读取的字节数；累计读取量超过限制时抛出候选数据异常。</returns>
        /// <remarks>
        /// 超过流内容上限时抛出 DefinitionCandidateException；未超限时返回本次读取量。
        /// </remarks>
        private int Count(int count) { _read += count; if (_read > limit) throw new DefinitionCandidateException("描述内容超过大小限制。"); return count; }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));
        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => Count(await inner.ReadAsync(buffer, token).ConfigureAwait(false));
        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
        /// <inheritdoc />
        public override void Flush() { }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        // 包装器不拥有底层流，由分支统一释放。
    }
}
/// <summary>
/// 记录并协调一次定义请求的并行恢复尝试。
/// </summary>
/// <param name="clock">用于读取当前时间的提供程序。</param>
internal sealed class DefinitionAttemptState(TimeProvider clock)
{
    /// <summary>
    /// 定义在弹性上下文中保存定义尝试状态的属性键。
    /// </summary>
    internal static readonly ResiliencePropertyKey<DefinitionAttemptState> Key = new("definition.state", x => x);
    /// <summary>
    /// 保存允许再次尝试定义恢复的最早时间。
    /// </summary>
    private long _notBefore;
    /// <summary>
    /// 按 Retry-After 响应头更新下一次尝试的等待截止时间。
    /// </summary>
    /// <param name="response">当前端点返回的 HTTP 响应。</param>
    /// <remarks>
    /// 忽略空或非正等待时间；多个响应并发更新时保留较晚的截止时间。
    /// </remarks>
    internal void Observe(HttpResponseMessage response)
    {
        var delay = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - clock.GetUtcNow());
        if (delay is null || delay <= TimeSpan.Zero) return;
        var now = clock.GetTimestamp(); var ticks = delay.Value.TotalSeconds * clock.TimestampFrequency;
        var deadline = ticks >= long.MaxValue - now ? long.MaxValue : now + (long)ticks;
        long current;
        do { current = Volatile.Read(ref _notBefore); if (current >= deadline) return; }
        while (Interlocked.CompareExchange(ref _notBefore, deadline, current) != current);
    }
    /// <summary>
    /// 等待 Retry-After 指定的下一次尝试时间并响应取消。
    /// </summary>
    /// <param name="token">用于取消等待的令牌。</param>
    /// <remarks>
    /// 等待期间持续检查取消；单次延迟最多分段为一天。
    /// </remarks>
    internal async ValueTask WaitAsync(CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var seconds = (Volatile.Read(ref _notBefore) - (double)clock.GetTimestamp()) / clock.TimestampFrequency;
            if (seconds <= 0) return;
            await ResilienceTime.DelayAsync(clock, TimeSpan.FromSeconds(Math.Min(seconds, 86400)), token).ConfigureAwait(false);
        }
    }
}
