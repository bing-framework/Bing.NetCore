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

/// <summary>专用描述传输完整竞速；缓存及下载生命周期属于宿主，不属于首个等待者。</summary>
internal sealed class HedgedDefinitionSource : IBingDynamicApiDefinitionSource, IDisposable, IAsyncDisposable
{
    private static readonly Meter Meter = new("Bing.Resilience");
    private static readonly Counter<long> Branches = Meter.CreateCounter<long>("bing.resilience.definition.branches");
    private readonly DefinitionRecoveryRegistration _registration;
    private readonly IHttpClientFactory _clients;
    private readonly IResiliencePipelineProvider _pipelines;
    private readonly IServiceScopeFactory _scopes;
    private readonly IServiceEndpointSource? _discovery;
    private readonly IEndpointManifestSource? _manifests;
    private readonly TimeProvider _clock;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _cache = new(StringComparer.Ordinal);
    private bool _disposed;
    private sealed record Entry(Task<BingDynamicApiDefinition> Task, DateTimeOffset Expires);
    private sealed record Candidates(IReadOnlyList<BingDefinitionEndpoint> Endpoints, string Version, string? Digest,
        ServiceEndpointSnapshot? Discovery, EndpointManifest? Manifest);
    internal HedgedDefinitionSource(DefinitionRecoveryRegistration registration, IHttpClientFactory clients, IResiliencePipelineProvider pipelines,
        IServiceScopeFactory scopes, TimeProvider clock, IServiceEndpointSource? discovery, IEndpointManifestSource? manifests)
    {
        _registration = registration; _clients = clients; _pipelines = pipelines; _scopes = scopes; _clock = clock; _discovery = discovery; _manifests = manifests;
        if (registration.Options.UseDiscoveryBootstrap && discovery is null || registration.Options.UseManifestCandidates && manifests is null)
            throw new ResilienceConfigurationException("描述源缺少显式启用的 Discovery/Manifest 依赖。");
    }
    public string RemoteName => _registration.RemoteName;
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
    private void RemoveFailure(string key, Entry entry)
    {
        lock (_gate) if (_cache.TryGetValue(key, out var current) && ReferenceEquals(current, entry)) _cache.Remove(key);
    }
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
    private void Record(string endpoint, string outcome)
    {
        try { Branches.Add(1, new KeyValuePair<string, object?>("client", _registration.TransportName),
            new KeyValuePair<string, object?>("endpoint", endpoint), new KeyValuePair<string, object?>("outcome", outcome)); }
        catch { /* 指标订阅不能改变已确定的定义结果。 */ }
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool SameOrigin(Uri first, Uri second) => string.Equals(first.GetLeftPart(UriPartial.Authority), second.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    public void Dispose()
    {
        Task[] active;
        lock (_gate) { if (_disposed) return; _disposed = true; active = _cache.Values.Select(x => x.Task).ToArray(); _cache.Clear(); }
        _shutdown.Cancel();
        _ = Task.WhenAll(active).ContinueWith(_ => _shutdown.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
    public async ValueTask DisposeAsync()
    {
        Task[] active;
        lock (_gate) active = _cache.Values.Select(x => x.Task).ToArray();
        Dispose();
        try { await Task.WhenAll(active).ConfigureAwait(false); }
        catch (Exception) when (active.All(x => x.IsCompleted)) { /* 调用者已收到原始失败，释放只等待资源退出。 */ }
    }
    private sealed class BoundedReadStream(Stream inner, long limit) : Stream
    {
        private long _read;
        public override bool CanRead => inner.CanRead; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        private int Count(int count) { _read += count; if (_read > limit) throw new DefinitionCandidateException("描述内容超过大小限制。"); return count; }
        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => Count(await inner.ReadAsync(buffer, token).ConfigureAwait(false));
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        // 包装器不拥有底层流，由分支统一释放。
    }
}
internal sealed class DefinitionAttemptState(TimeProvider clock)
{
    internal static readonly ResiliencePropertyKey<DefinitionAttemptState> Key = new("definition.state", x => x);
    private long _notBefore;
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
