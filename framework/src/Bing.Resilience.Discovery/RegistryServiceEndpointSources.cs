using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bing.Resilience.Discovery;

/// <summary>基于受控 HTTP 协议的注册中心适配配置。适配器不引入任何厂商 SDK。</summary>
public class HttpRegistryEndpointOptions
{
    public string ServiceName { get; set; } = "";
    public Uri BaseAddress { get; set; } = null!;
    public Func<Uri, bool>? TrustBaseAddress { get; set; }
    public Func<HttpMessageHandler, bool>? ValidateCustomTransport { get; set; }
    public Func<HttpRequestMessage, CancellationToken, ValueTask>? PrepareRequestAsync { get; set; }
    public Func<ReadOnlyMemory<byte>, bool>? VerifyDocument { get; set; }
    public string Scheme { get; set; } = "http";
    public string ContractRevision { get; set; } = "v1";
    public string ConsistencyGroup { get; set; } = "default";
    public string CredentialAudience { get; set; } = "";
    public int MaximumBytes { get; set; } = 512 * 1024;
    public int MaximumEndpoints { get; set; } = 128;
    public TimeSpan RefreshPeriod { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan MaxStale { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan FailureBackoff { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>Consul 健康服务 API 适配配置。</summary>
public sealed class ConsulEndpointOptions : HttpRegistryEndpointOptions
{
    public string? Datacenter { get; set; }
    public bool PassingOnly { get; set; } = true;
}

/// <summary>Nacos 命名服务实例 API 适配配置。</summary>
public sealed class NacosEndpointOptions : HttpRegistryEndpointOptions
{
    public string GroupName { get; set; } = "DEFAULT_GROUP";
    public string NamespaceId { get; set; } = "";
    /// <summary>v1 兼容接口的上下文路径；实例须发布 metadata.incarnation。</summary>
    public string ContextPath { get; set; } = "/nacos";
    public bool HealthyOnly { get; set; } = true;
    public bool EnabledOnly { get; set; } = true;
}

/// <summary>Kubernetes EndpointSlice API 适配配置。</summary>
public sealed class KubernetesEndpointSliceOptions : HttpRegistryEndpointOptions
{
    public string Namespace { get; set; } = "default";
    public string? PortName { get; set; }
    public int? Port { get; set; }
    public string? Protocol { get; set; }
    public bool IncludeUnknownReady { get; set; }
    public int MaximumPages { get; set; } = 16;
}

/// <summary>HTTP 注册中心适配器的统一基础实现，提供快照、失败退避、撤销和身份回收。</summary>
public abstract class HttpRegistryServiceEndpointSource : IServiceEndpointSource, IDisposable
{
    private readonly object _gate = new();
    private readonly HttpClient _client;
    private readonly IServiceEndpointTrustPolicy _trust;
    private readonly HttpRegistryEndpointOptions _options;
    private readonly TimeProvider _clock;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ITimer _timer;
    private readonly Dictionary<string, (Uri Origin, DateTimeOffset Until)> _identities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _revocations = new(StringComparer.Ordinal);
    private Task<ServiceEndpointSnapshot>? _refresh;
    private ServiceEndpointSnapshot? _snapshot;
    private EntityTagHeaderValue? _etag;
    private DateTimeOffset _retryAt;
    private Exception? _failure;
    private bool _disposed;

    protected HttpRegistryServiceEndpointSource(HttpRegistryEndpointOptions options, HttpMessageHandler transport,
        IServiceEndpointTrustPolicy trust, TimeProvider clock, bool disposeTransport = false)
    {
        ValidateOptions(options, transport);
        _options = Clone(options);
        _trust = trust ?? throw new ArgumentNullException(nameof(trust));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _client = new HttpClient(transport, disposeTransport) { Timeout = Timeout.InfiniteTimeSpan };
        _timer = _clock.CreateTimer(_ => BackgroundRefresh(), null, _options.RefreshPeriod, _options.RefreshPeriod);
    }

    public bool IsRevoked(string serviceName, ServiceEndpoint endpoint)
    {
        lock (_gate)
        {
            if (!string.Equals(serviceName, _options.ServiceName, StringComparison.Ordinal)) return true;
            if (!_revocations.TryGetValue(endpoint.StateKey, out var until)) return false;
            if (until <= _clock.GetUtcNow()) { _revocations.Remove(endpoint.StateKey); return false; }
            return true;
        }
    }

    /// <summary>撤销当前或历史快照中的端点身份，阻止旧调用继续发送。</summary>
    public void Revoke(string endpointId, string incarnation)
    {
        if (string.IsNullOrWhiteSpace(endpointId) || string.IsNullOrWhiteSpace(incarnation) || endpointId.Length > 128 || incarnation.Length > 128)
            throw new ResilienceConfigurationException("撤销端点身份无效。");
        var key = Uri.EscapeDataString(endpointId) + ":" + Uri.EscapeDataString(incarnation);
        lock (_gate)
        {
            if (_identities.TryGetValue(key, out var identity)) { _revocations[key] = identity.Until; return; }
            if (_snapshot?.Endpoints.Any(x => x.StateKey == key) == true) { _revocations[key] = _snapshot.MaxStaleUntil; return; }
            throw new ResilienceConfigurationException("撤销端点身份未在适配器历史中登记。");
        }
    }

    public async ValueTask<ServiceEndpointSnapshot> GetSnapshotAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await GetSnapshotCoreAsync(serviceName, cancellationToken).ConfigureAwait(false);
            if (snapshot.ExpiresAt <= _clock.GetUtcNow()) ResilienceDiagnostics.RecordLifecycle("discovery.stale", "served");
            return snapshot;
        }
        catch (OperationCanceledException) { ResilienceDiagnostics.RecordLifecycle("discovery.read", "cancelled"); throw; }
        catch { ResilienceDiagnostics.RecordLifecycle("discovery.read", "failure"); throw; }
    }
    private async ValueTask<ServiceEndpointSnapshot> GetSnapshotCoreAsync(string serviceName, CancellationToken cancellationToken)
    {
        if (!string.Equals(serviceName, _options.ServiceName, StringComparison.Ordinal))
            throw new ResilienceDiscoveryException(serviceName, "注册中心适配器未登记该服务。");
        cancellationToken.ThrowIfCancellationRequested();
        Task<ServiceEndpointSnapshot> refresh;
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var now = _clock.GetUtcNow();
            if (_snapshot is { } fresh && fresh.ExpiresAt > now) return fresh;
            if (_retryAt > now)
            {
                if (_snapshot is { } stale && stale.MaxStaleUntil > now) return stale;
                throw new ResilienceDiscoveryException(_options.ServiceName, "注册中心处于失败退避期。", _failure);
            }
            refresh = _refresh ??= Task.Run(RefreshAsync);
        }
        try { return await refresh.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested && !_shutdown.IsCancellationRequested)
        {
            lock (_gate)
                if (_snapshot is { } stale && stale.MaxStaleUntil > _clock.GetUtcNow()) return stale;
            throw new ResilienceDiscoveryException(_options.ServiceName, "注册中心刷新失败。", error);
        }
    }

    protected abstract Uri BuildRequestUri(string serviceName);
    protected abstract ServiceEndpoint[] ParseEndpoints(string serviceName, JsonElement document, DateTimeOffset now);
    protected virtual string? GetContinuation(JsonElement document) => null;
    protected virtual int MaximumPages => 1;
    protected virtual string GetSourceVersion(JsonElement document, ReadOnlyMemory<byte> raw)
    {
        using var hash = SHA256.Create();
        return Convert.ToHexString(hash.ComputeHash(raw.ToArray())).ToLowerInvariant();
    }

    private async Task<ServiceEndpointSnapshot> RefreshAsync()
    {
        try
        {
            var result = await RefreshCoreAsync().ConfigureAwait(false);
            ResilienceDiagnostics.RecordLifecycle("discovery.refresh", "success");
            return result;
        }
        catch (OperationCanceledException) { ResilienceDiagnostics.RecordLifecycle("discovery.refresh", "cancelled"); throw; }
        catch { ResilienceDiagnostics.RecordLifecycle("discovery.refresh", "failure"); throw; }
    }
    private async Task<ServiceEndpointSnapshot> RefreshCoreAsync()
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        using var timer = _clock.CreateTimer(_ => { try { budget.Cancel(); } catch (ObjectDisposedException) { } }, null, _options.RequestTimeout, Timeout.InfiniteTimeSpan);
        try
        {
            var uri = BuildRequestUri(_options.ServiceName);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (_etag is not null) request.Headers.IfNoneMatch.Add(_etag);
            if (_options.PrepareRequestAsync is not null) await _options.PrepareRequestAsync(request, budget.Token).ConfigureAwait(false);
            if (request.RequestUri != uri || request.Method != HttpMethod.Get || request.Content is not null)
                throw new ResilienceConfigurationException("注册中心认证处理不能改变可信目标、方法或请求体。");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false);
            var now = _clock.GetUtcNow();
            ServiceEndpointSnapshot snapshot;
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ||
                response.Headers.TryGetValues("X-Consul-Results-Filtered-By-ACLs", out var filtered) && filtered.Contains("true"))
            {
                lock (_gate) { _snapshot = null; _etag = null; }
                throw new ResilienceDiscoveryException(_options.ServiceName, "注册中心拒绝授权或返回被权限截断的列表。");
            }
            if (response.StatusCode == HttpStatusCode.NotModified && _snapshot is { } prior)
            {
                snapshot = new(prior.ServiceName, prior.SourceVersion, now,
                    now + _options.TimeToLive, now + _options.TimeToLive + _options.MaxStale, prior.Endpoints);
            }
            else
            {
                var endpoints = new List<ServiceEndpoint>();
                var consumed = 0;
                string? version = null;
                var page = response;
                var pageCount = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    while (true)
                    {
                        if (!page.IsSuccessStatusCode && page.StatusCode is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests) && (int)page.StatusCode < 500)
                            throw new ResilienceConfigurationException("注册中心返回永久错误或不可接受的重定向状态。");
                        page.EnsureSuccessStatusCode();
                        if (page.Content.Headers.ContentLength > _options.MaximumBytes - consumed)
                            throw new ResilienceConfigurationException("注册中心响应超限。");
                        await using var stream = await page.Content.ReadAsStreamAsync(budget.Token).ConfigureAwait(false);
                        using var buffer = new MemoryStream();
                        var bytes = new byte[8192];
                        while (true)
                        {
                            var read = await stream.ReadAsync(bytes.AsMemory(), budget.Token).ConfigureAwait(false);
                            if (read == 0) break;
                            consumed += read;
                            if (consumed > _options.MaximumBytes) throw new ResilienceConfigurationException("注册中心响应超限。");
                            buffer.Write(bytes, 0, read);
                        }
                        var raw = buffer.ToArray();
                        if (_options.VerifyDocument is not null && !_options.VerifyDocument(raw))
                            throw new ResilienceConfigurationException("注册中心响应签名校验失败。");
                        var parsed = ParsePage(raw, now);
                        var currentVersion = parsed.Version;
                        if (version is not null && version != currentVersion)
                            throw new ResilienceConfigurationException("注册中心分页快照版本不一致。");
                        version = currentVersion;
                        endpoints.AddRange(parsed.Endpoints);
                        if (endpoints.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() > _options.MaximumEndpoints)
                            throw new ResilienceConfigurationException("注册中心端点数量超限。");
                        var next = parsed.Next;
                        if (string.IsNullOrEmpty(next)) break;
                        if (++pageCount >= MaximumPages || !seen.Add(next))
                            throw new ResilienceConfigurationException("注册中心分页超限或重复。");
                        var nextUri = new UriBuilder(uri) { Query = uri.Query.TrimStart('?') + "&continue=" + Uri.EscapeDataString(next) }.Uri;
                        using var nextRequest = new HttpRequestMessage(HttpMethod.Get, nextUri);
                        if (_options.PrepareRequestAsync is not null) await _options.PrepareRequestAsync(nextRequest, budget.Token).ConfigureAwait(false);
                        if (nextRequest.RequestUri != nextUri || nextRequest.Method != HttpMethod.Get || nextRequest.Content is not null)
                            throw new ResilienceConfigurationException("分页认证处理改变了可信请求。");
                        if (!ReferenceEquals(page, response)) page.Dispose();
                        page = await _client.SendAsync(nextRequest, HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false);
                        if (page.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                            throw new ResilienceConfigurationException("分页授权被拒绝。");
                    }
                }
                finally { if (!ReferenceEquals(page, response)) page.Dispose(); }
                // EndpointSlice 滚动更新时同一实例可跨 Slice 重复；冲突身份不能被静默合并。
                var unique = endpoints.GroupBy(e => e.Id, StringComparer.Ordinal).Select(group =>
                {
                    var first = group.First();
                    if (group.Any(e => e != first)) throw new ResilienceConfigurationException("注册中心返回冲突实例身份。");
                    return first;
                }).ToArray();
                snapshot = new(_options.ServiceName, version!, now, now + _options.TimeToLive,
                    now + _options.TimeToLive + _options.MaxStale, unique);
            }
            ConfigurationServiceEndpointSource.Validate(snapshot, _trust, _options.MaximumEndpoints);
            lock (_gate)
            {
                foreach (var key in _revocations.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) _revocations.Remove(key);
                foreach (var key in _identities.Where(x => x.Value.Until <= now && !_revocations.ContainsKey(x.Key)).Select(x => x.Key).ToArray()) _identities.Remove(key);
                if (_identities.Count + snapshot.Endpoints.Count(e => !_identities.ContainsKey(e.StateKey)) > _options.MaximumEndpoints * 4)
                    throw new ResilienceConfigurationException("注册中心历史身份达到上限。");
                foreach (var endpoint in snapshot.Endpoints)
                {
                    if (_identities.TryGetValue(endpoint.StateKey, out var identity) && identity.Origin != endpoint.Origin)
                        throw new ResilienceConfigurationException("注册中心实例 Origin 变更必须更换 Incarnation。");
                }
                foreach (var endpoint in snapshot.Endpoints)
                    _identities[endpoint.StateKey] = (endpoint.Origin, snapshot.MaxStaleUntil);
                _snapshot = snapshot;
                _etag = response.StatusCode == HttpStatusCode.NotModified ? response.Headers.ETag ?? _etag : response.Headers.ETag;
                _failure = null; _retryAt = default;
            }
            return snapshot;
        }
        catch (Exception error)
        {
            lock (_gate)
            {
                // 授权、签名、身份/信任校验失败不能借陈旧缓存继续路由。
                if (error is ResilienceConfigurationException) { _snapshot = null; _etag = null; }
                _failure = error; _retryAt = _clock.GetUtcNow() + _options.FailureBackoff;
            }
            throw;
        }
        finally { lock (_gate) _refresh = null; }
    }

    private void BackgroundRefresh()
    {
        lock (_gate)
            if (_disposed || _snapshot is null || _snapshot.ExpiresAt > _clock.GetUtcNow() || _retryAt > _clock.GetUtcNow()) return;
        _ = ObserveBackgroundAsync();
    }
    private (string Version, ServiceEndpoint[] Endpoints, string? Next) ParsePage(byte[] raw, DateTimeOffset now)
    {
        try
        {
            using var document = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 32 });
            return (GetSourceVersion(document.RootElement, raw), ParseEndpoints(_options.ServiceName, document.RootElement, now), GetContinuation(document.RootElement));
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new ResilienceConfigurationException("注册中心文档格式或字段类型无效。", error);
        }
    }
    private async Task ObserveBackgroundAsync() { try { await GetSnapshotAsync(_options.ServiceName, _shutdown.Token).ConfigureAwait(false); } catch { } }

    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        _timer.Dispose(); _shutdown.Cancel(); _client.Dispose();
        Task? pending; lock (_gate) pending = _refresh;
        if (pending is null) _shutdown.Dispose(); else _ = pending.ContinueWith(_ => _shutdown.Dispose(), TaskScheduler.Default);
    }

    private static void ValidateOptions(HttpRegistryEndpointOptions options, HttpMessageHandler transport)
    {
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(transport);
        if (string.IsNullOrWhiteSpace(options.ServiceName) || options.ServiceName.Length > 128 || options.BaseAddress is null ||
            !options.BaseAddress.IsAbsoluteUri || options.BaseAddress.UserInfo.Length != 0 || options.BaseAddress.AbsolutePath != "/" || options.BaseAddress.Query.Length != 0 || options.BaseAddress.Fragment.Length != 0 ||
            options.BaseAddress.Scheme is not ("http" or "https") || options.TrustBaseAddress?.Invoke(options.BaseAddress) != true ||
            options.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(options.ContractRevision) ||
            string.IsNullOrWhiteSpace(options.ConsistencyGroup) || string.IsNullOrWhiteSpace(options.CredentialAudience) ||
            options.MaximumBytes < 1 || options.MaximumEndpoints is < 1 or > 4096 || options.RefreshPeriod <= TimeSpan.Zero ||
            options.RequestTimeout <= TimeSpan.Zero || options.TimeToLive <= TimeSpan.Zero || options.MaxStale < TimeSpan.Zero || options.FailureBackoff <= TimeSpan.Zero || new[] { options.RequestTimeout, options.RefreshPeriod, options.TimeToLive, options.MaxStale, options.FailureBackoff }.Any(t => t > TimeSpan.FromDays(30)))
            throw new ResilienceConfigurationException("注册中心适配配置无效。");
        var validTransport = transport switch
        {
            SocketsHttpHandler sockets => !sockets.AllowAutoRedirect && !sockets.UseCookies,
            HttpClientHandler handler => !handler.AllowAutoRedirect && !handler.UseCookies,
            _ => options.ValidateCustomTransport?.Invoke(transport) == true
        };
        if (!validTransport) throw new ResilienceConfigurationException("注册中心传输必须关闭自动重定向和共享 Cookie。");
    }

    private static HttpRegistryEndpointOptions Clone(HttpRegistryEndpointOptions options) => new()
    {
        ServiceName = options.ServiceName, BaseAddress = options.BaseAddress, TrustBaseAddress = options.TrustBaseAddress,
        ValidateCustomTransport = options.ValidateCustomTransport, PrepareRequestAsync = options.PrepareRequestAsync,
        VerifyDocument = options.VerifyDocument, Scheme = options.Scheme, ContractRevision = options.ContractRevision,
        ConsistencyGroup = options.ConsistencyGroup, CredentialAudience = options.CredentialAudience, MaximumBytes = options.MaximumBytes,
        MaximumEndpoints = options.MaximumEndpoints, RefreshPeriod = options.RefreshPeriod, RequestTimeout = options.RequestTimeout,
        TimeToLive = options.TimeToLive, MaxStale = options.MaxStale, FailureBackoff = options.FailureBackoff
    };

    protected static Uri Origin(string scheme, string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535) throw new ResilienceConfigurationException("注册中心端点地址无效。");
        if (host.Contains(':') && !host.StartsWith("[", StringComparison.Ordinal)) host = "[" + host + "]";
        return new UriBuilder(scheme, host, port).Uri;
    }
    protected ServiceEndpoint Endpoint(string id, string incarnation, Uri origin, int weight = 1, string? region = null,
        string? contract = null, string? group = null, string? audience = null) =>
        new(id, incarnation, origin, contract ?? _options.ContractRevision, group ?? _options.ConsistencyGroup,
            audience ?? _options.CredentialAudience, weight, region);
    protected HttpRegistryEndpointOptions Options => _options;
    protected static string StableId(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    protected static string? Text(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    protected static JsonElement Member(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) ? v : default;
    protected static string Required(JsonElement e, string key) => !string.IsNullOrWhiteSpace(Text(e, key)) ? Text(e, key)! : throw new ResilienceConfigurationException("注册中心缺少 " + key);
    protected static bool Flag(JsonElement e, string key, bool fallback = false) => Member(e, key).ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => fallback };
}

/// <summary>读取 Consul 健康服务列表；节点 ID + 服务 ID 区分不同节点的同名实例。</summary>
public sealed class ConsulServiceEndpointSource : HttpRegistryServiceEndpointSource
{
    private readonly string? _datacenter;
    private readonly bool _passingOnly;
    public ConsulServiceEndpointSource(ConsulEndpointOptions options, HttpMessageHandler transport, IServiceEndpointTrustPolicy trust, TimeProvider clock, bool disposeTransport = false)
        : base(options, transport, trust, clock, disposeTransport) { _datacenter = options.Datacenter; _passingOnly = options.PassingOnly; }
    protected override Uri BuildRequestUri(string serviceName) => new UriBuilder(Options.BaseAddress)
    {
        Path = "/v1/health/service/" + Uri.EscapeDataString(serviceName),
        Query = "passing=" + (_passingOnly ? "true" : "false") + (string.IsNullOrWhiteSpace(_datacenter) ? "" : "&dc=" + Uri.EscapeDataString(_datacenter))
    }.Uri;
    protected override ServiceEndpoint[] ParseEndpoints(string serviceName, JsonElement document, DateTimeOffset now)
    {
        if (document.ValueKind != JsonValueKind.Array) throw new ResilienceConfigurationException("Consul 响应不是数组。");
        var result = new List<ServiceEndpoint>();
        foreach (var item in document.EnumerateArray())
        {
            var node = Member(item, "Node"); var service = Member(item, "Service");
            if (Required(service, "Service") != serviceName) throw new ResilienceConfigurationException("Consul 服务身份不匹配。");
            var checks = Member(item, "Checks");
            if (_passingOnly && (checks.ValueKind != JsonValueKind.Array || checks.EnumerateArray().Any(c => Text(c, "Status") != "passing"))) continue;
            var id = StableId(Required(node, "ID") + ":" + Required(service, "ID"));
            var address = Text(service, "Address");
            if (string.IsNullOrWhiteSpace(address)) address = Required(node, "Address");
            var port = Member(service, "Port").GetInt32();
            var metadata = Member(service, "Meta");
            var incarnation = Text(metadata, "incarnation");
            if (string.IsNullOrWhiteSpace(incarnation))
            {
                var index = Member(service, "CreateIndex");
                if (index.ValueKind != JsonValueKind.Number || !index.TryGetInt64(out var created) || created <= 0)
                    throw new ResilienceConfigurationException("Consul 实例需要 CreateIndex 或 metadata.incarnation。");
                incarnation = created.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            result.Add(Endpoint(id, incarnation!, Origin(Options.Scheme, address!, port), 1, Text(node, "Datacenter"),
                Text(metadata, "contractRevision"), Text(metadata, "consistencyGroup"), Text(metadata, "credentialAudience")));
        }
        return result.ToArray();
    }
}

/// <summary>Nacos 1.x/2.x v1 兼容 HTTP 命名接口；实例必须发布 incarnation 元数据以标识重建。</summary>
public sealed class NacosServiceEndpointSource : HttpRegistryServiceEndpointSource
{
    private readonly string _group, _namespace, _path;
    private readonly bool _healthy, _enabled;
    public NacosServiceEndpointSource(NacosEndpointOptions options, HttpMessageHandler transport, IServiceEndpointTrustPolicy trust, TimeProvider clock, bool disposeTransport = false)
        : base(options, transport, trust, clock, disposeTransport)
    {
        _group = options.GroupName; _namespace = options.NamespaceId; _healthy = options.HealthyOnly; _enabled = options.EnabledOnly;
        _path = options.ContextPath;
        if (string.IsNullOrWhiteSpace(_group) || _namespace is null || _path is null || !_path.StartsWith('/') || _path.Contains('?') || _path.Contains('#') || _path.Contains(".."))
        { Dispose(); throw new ResilienceConfigurationException("Nacos 组、命名空间或上下文路径无效。"); }
    }
    protected override Uri BuildRequestUri(string serviceName) => new UriBuilder(Options.BaseAddress)
    {
        Path = _path.TrimEnd('/') + "/v1/ns/instance/list",
        Query = "serviceName=" + Uri.EscapeDataString(serviceName) + "&groupName=" + Uri.EscapeDataString(_group) + "&namespaceId=" + Uri.EscapeDataString(_namespace) + "&healthyOnly=" + (_healthy ? "true" : "false")
    }.Uri;
    protected override ServiceEndpoint[] ParseEndpoints(string serviceName, JsonElement document, DateTimeOffset now)
    {
        var hosts = Member(document, "hosts");
        if (hosts.ValueKind != JsonValueKind.Array) throw new ResilienceConfigurationException("Nacos v1 响应缺少 hosts。");
        var name = Text(document, "name");
        if (name != serviceName && name != _group + "@@" + serviceName) throw new ResilienceConfigurationException("Nacos 服务身份不匹配。");
        var result = new List<ServiceEndpoint>();
        foreach (var host in hosts.EnumerateArray())
        {
            if (_healthy && !Flag(host, "healthy") || _enabled && !Flag(host, "enabled")) continue;
            var weight = Member(host, "weight");
            var number = weight.ValueKind == JsonValueKind.Number ? weight.GetDouble() : 1;
            if (number == 0) continue;
            if (!double.IsFinite(number) || number < 0 || number > 10000) throw new ResilienceConfigurationException("Nacos 权重无效。");
            var origin = Origin(Options.Scheme, Required(host, "ip"), Member(host, "port").GetInt32());
            var metadata = Member(host, "metadata");
            var id = Text(host, "instanceId");
            if (string.IsNullOrWhiteSpace(id)) id = (Text(host, "clusterName") ?? "DEFAULT") + ":" + origin;
            result.Add(Endpoint(StableId(id), Required(metadata, "incarnation"), origin, Math.Max(1, (int)Math.Round(number * 100)),
                Text(metadata, "region"), Text(metadata, "contractRevision"), Text(metadata, "consistencyGroup"), Text(metadata, "credentialAudience")));
        }
        return result.ToArray();
    }
}

/// <summary>EndpointSlice 完整分页列表；端点状态按 Pod UID 保持，不随 resourceVersion 改变。</summary>
public sealed class KubernetesEndpointSliceSource : HttpRegistryServiceEndpointSource
{
    private readonly string _namespace;
    private readonly string? _portName;
    private readonly int? _port;
    private readonly bool _unknownReady;
    private readonly int _pages;
    public KubernetesEndpointSliceSource(KubernetesEndpointSliceOptions options, HttpMessageHandler transport, IServiceEndpointTrustPolicy trust, TimeProvider clock, bool disposeTransport = false)
        : base(options, transport, trust, clock, disposeTransport)
    {
        _namespace = options.Namespace; _portName = options.PortName; _port = options.Port; _unknownReady = options.IncludeUnknownReady; _pages = options.MaximumPages;
        if (string.IsNullOrWhiteSpace(_namespace) || _namespace.Contains('/') || _namespace is "." or ".." || _pages is < 1 or > 128 ||
            _port is < 1 or > 65535 || options.Protocol is not (null or "TCP"))
        { Dispose(); throw new ResilienceConfigurationException("EndpointSlice 命名空间、端口、TCP 协议或页数配置无效。"); }
    }
    protected override int MaximumPages => _pages;
    protected override Uri BuildRequestUri(string serviceName) => new UriBuilder(Options.BaseAddress)
    {
        Path = "/apis/discovery.k8s.io/v1/namespaces/" + Uri.EscapeDataString(_namespace) + "/endpointslices",
        Query = "limit=100&labelSelector=" + Uri.EscapeDataString("kubernetes.io/service-name=" + serviceName)
    }.Uri;
    protected override string? GetContinuation(JsonElement document) => Text(Member(document, "metadata"), "continue");
    protected override string GetSourceVersion(JsonElement document, ReadOnlyMemory<byte> raw) => Required(Member(document, "metadata"), "resourceVersion");
    protected override ServiceEndpoint[] ParseEndpoints(string serviceName, JsonElement document, DateTimeOffset now)
    {
        var items = Member(document, "items");
        if (items.ValueKind != JsonValueKind.Array) throw new ResilienceConfigurationException("EndpointSlice 缺少 items。");
        var result = new List<ServiceEndpoint>();
        foreach (var slice in items.EnumerateArray())
        {
            var metadata = Member(slice, "metadata");
            if (Required(Member(metadata, "labels"), "kubernetes.io/service-name") != serviceName || Required(metadata, "namespace") != _namespace)
                throw new ResilienceConfigurationException("EndpointSlice 服务或命名空间不匹配。");
            var ports = Member(slice, "ports");
            if (ports.ValueKind != JsonValueKind.Array) continue;
            var matches = ports.EnumerateArray().Where(p => (Text(p, "protocol") is null or "TCP") && (_portName is null || Text(p, "name") == _portName))
                .Select(p => Member(p, "port")).Where(p => p.ValueKind == JsonValueKind.Number).Select(p => p.GetInt32())
                .Where(p => !_port.HasValue || p == _port).Distinct().ToArray();
            if (matches.Length == 0) continue;
            if (matches.Length != 1) throw new ResilienceConfigurationException("EndpointSlice 有多个 HTTP 候选端口，请配置 PortName 或 Port。");
            var endpoints = Member(slice, "endpoints");
            if (endpoints.ValueKind != JsonValueKind.Array) throw new ResilienceConfigurationException("EndpointSlice 缺少 endpoints。");
            foreach (var endpoint in endpoints.EnumerateArray())
            {
                var conditions = Member(endpoint, "conditions");
                if (Flag(conditions, "terminating") || !Flag(conditions, "ready", _unknownReady)) continue;
                var addresses = Member(endpoint, "addresses");
                if (addresses.ValueKind != JsonValueKind.Array) throw new ResilienceConfigurationException("EndpointSlice 缺少 addresses。");
                var incarnation = Text(Member(endpoint, "targetRef"), "uid") ?? Required(metadata, "uid");
                foreach (var address in addresses.EnumerateArray())
                {
                    var origin = Origin(Options.Scheme, address.GetString()!, matches[0]);
                    result.Add(Endpoint(StableId(serviceName + ":" + origin), incarnation, origin, 1, Text(endpoint, "zone")));
                }
            }
        }
        return result.ToArray();
    }
}
