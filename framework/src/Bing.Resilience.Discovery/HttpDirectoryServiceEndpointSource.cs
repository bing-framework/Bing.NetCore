using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Bing.Resilience.Discovery;

/// <summary>受控目录源配置；地址、文档和端点分别校验。</summary>
public sealed class HttpEndpointDirectoryOptions
{
    public string ServiceName { get; set; } = "";
    public Uri DirectoryUri { get; set; } = null!;
    public Func<Uri, bool>? TrustDirectory { get; set; }
    public Func<HttpMessageHandler, bool>? ValidateCustomTransport { get; set; }
    public Func<HttpRequestMessage, CancellationToken, ValueTask>? PrepareRequestAsync { get; set; }
    public Func<ReadOnlyMemory<byte>, bool>? VerifyDocument { get; set; }
    public int MaximumBytes { get; set; } = 256 * 1024;
    public int MaximumEndpoints { get; set; } = 128;
    public TimeSpan RefreshPeriod { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan MaxStale { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan FailureBackoff { get; set; } = TimeSpan.FromSeconds(5);
}
public sealed record HttpEndpointDirectoryDocument(string ServiceName, string Version, ServiceEndpoint[] Endpoints);

/// <summary>ETag、后台刷新、失败退避与有界内容读取。外部等待取消不取消共享刷新。</summary>
public sealed class HttpDirectoryServiceEndpointSource : IServiceEndpointSource, IDisposable
{
    private readonly object _gate = new();
    private readonly HttpClient _client;
    private readonly TimeProvider _clock;
    private readonly IServiceEndpointTrustPolicy _trust;
    private readonly HttpEndpointDirectoryOptions _options;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ITimer _timer;
    private Task<ServiceEndpointSnapshot>? _refresh;
    private ServiceEndpointSnapshot? _snapshot;
    private EntityTagHeaderValue? _etag;
    private readonly Dictionary<string, DateTimeOffset> _revocations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Uri Origin, DateTimeOffset Until)> _identities = new(StringComparer.Ordinal);
    private DateTimeOffset _retryAt;
    private Exception? _failure;
    private bool _disposed;
    public HttpDirectoryServiceEndpointSource(HttpEndpointDirectoryOptions options, HttpMessageHandler transport,
        IServiceEndpointTrustPolicy trust, TimeProvider clock, bool disposeTransport = false)
    {
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(transport);
        if (string.IsNullOrWhiteSpace(options.ServiceName) || options.DirectoryUri is null || !options.DirectoryUri.IsAbsoluteUri ||
            options.DirectoryUri.Scheme is not ("https" or "http") || options.DirectoryUri.UserInfo.Length != 0 ||
            options.TrustDirectory?.Invoke(options.DirectoryUri) != true || options.MaximumBytes < 1 || options.MaximumEndpoints < 1 ||
            options.RefreshPeriod <= TimeSpan.Zero || options.RequestTimeout <= TimeSpan.Zero || options.TimeToLive <= TimeSpan.Zero ||
            options.MaxStale < TimeSpan.Zero || options.FailureBackoff <= TimeSpan.Zero)
            throw new ResilienceConfigurationException("HTTP 目录源配置或信任规则无效。");
        var valid = transport switch
        {
            SocketsHttpHandler sockets => !sockets.AllowAutoRedirect && !sockets.UseCookies,
            HttpClientHandler handler => !handler.AllowAutoRedirect && !handler.UseCookies,
            _ => options.ValidateCustomTransport?.Invoke(transport) == true
        };
        if (!valid) throw new ResilienceConfigurationException("目录传输必须关闭自动重定向和共享 Cookie。");
        _options = new()
        {
            ServiceName = options.ServiceName, DirectoryUri = options.DirectoryUri, PrepareRequestAsync = options.PrepareRequestAsync,
            VerifyDocument = options.VerifyDocument, MaximumBytes = options.MaximumBytes, MaximumEndpoints = options.MaximumEndpoints,
            RefreshPeriod = options.RefreshPeriod, RequestTimeout = options.RequestTimeout, TimeToLive = options.TimeToLive,
            MaxStale = options.MaxStale, FailureBackoff = options.FailureBackoff
        };
        _trust = trust; _clock = clock; _client = new(transport, disposeTransport) { Timeout = Timeout.InfiniteTimeSpan };
        _timer = clock.CreateTimer(_ => BackgroundRefresh(), null, options.RefreshPeriod, options.RefreshPeriod);
    }
    /// <summary>安全撤销独立于目录刷新，旧调用也必须在发送前检查。</summary>
    public void Revoke(string endpointId, string incarnation)
    {
        if (string.IsNullOrWhiteSpace(endpointId) || string.IsNullOrWhiteSpace(incarnation) ||
            endpointId.Length > 256 || incarnation.Length > 256)
            throw new ResilienceConfigurationException("撤销端点身份无效。");
        lock (_gate)
        {
            var key = Uri.EscapeDataString(endpointId) + ":" + Uri.EscapeDataString(incarnation);
            // 撤销操作可能发生在端点已经从当前快照退休之后；历史身份仍然必须可撤销，
            // 否则旧调用快照会绕过安全屏障。未知键拒绝记录，避免控制面输入造成无界墓碑增长。
            if (_identities.TryGetValue(key, out var identity))
            {
                _revocations[key] = identity.Until;
                return;
            }
            if (_snapshot is { } snapshot && snapshot.Endpoints.Any(x => x.StateKey == key))
            {
                _revocations[key] = snapshot.MaxStaleUntil;
                return;
            }
            throw new ResilienceConfigurationException("撤销端点身份未在目录历史中登记。");
        }
    }
    public bool IsRevoked(string serviceName, ServiceEndpoint endpoint)
    {
        lock (_gate)
        {
            if (serviceName != _options.ServiceName) return true;
            if (!_revocations.TryGetValue(endpoint.StateKey, out var until)) return false;
            if (until <= _clock.GetUtcNow()) { _revocations.Remove(endpoint.StateKey); return false; }
            return true;
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
        if (serviceName != _options.ServiceName) throw new ResilienceDiscoveryException(serviceName, "目录源未登记该服务。");
        cancellationToken.ThrowIfCancellationRequested();
        Task<ServiceEndpointSnapshot> refresh;
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HttpDirectoryServiceEndpointSource));
            if (_snapshot is { } fresh && fresh.ExpiresAt > _clock.GetUtcNow()) return fresh;
            if (_retryAt > _clock.GetUtcNow())
            {
                if (_snapshot is { } stale && stale.MaxStaleUntil > _clock.GetUtcNow()) return stale;
                throw new ResilienceDiscoveryException(serviceName, "目录源处于失败退避期。", _failure);
            }
            refresh = _refresh ??= Task.Run(RefreshAsync);
        }
        try { return await refresh.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            lock (_gate)
                if (_snapshot is { } stale && stale.MaxStaleUntil > _clock.GetUtcNow()) return stale;
            throw new ResilienceDiscoveryException(serviceName, "目录源刷新失败。", error);
        }
    }
    private void BackgroundRefresh()
    {
        lock (_gate)
        {
            if (_disposed || _snapshot is null || _snapshot.ExpiresAt > _clock.GetUtcNow() || _retryAt > _clock.GetUtcNow()) return;
        }
        _ = ObserveBackgroundAsync();
    }
    private async Task ObserveBackgroundAsync()
    {
        try { await GetSnapshotAsync(_options.ServiceName, _shutdown.Token).ConfigureAwait(false); }
        catch (Exception) { /* 失败已由刷新路径记入退避状态；下一次调用获取原始失败。 */ }
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
            using var request = new HttpRequestMessage(HttpMethod.Get, _options.DirectoryUri);
            if (_etag is not null) request.Headers.IfNoneMatch.Add(_etag);
            if (_options.PrepareRequestAsync is not null) await _options.PrepareRequestAsync(request, budget.Token).ConfigureAwait(false);
            if (request.RequestUri != _options.DirectoryUri || request.Method != HttpMethod.Get || request.Content is not null)
                throw new ResilienceConfigurationException("目录认证处理不能改变可信目标、方法或请求体。");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                lock (_gate) _snapshot = null;
                throw new ResilienceDiscoveryException(_options.ServiceName, "目录授权被拒绝。");
            }
            ServiceEndpointSnapshot snapshot;
            var now = _clock.GetUtcNow();
            foreach (var key in _revocations.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) _revocations.Remove(key);
            if (response.StatusCode == HttpStatusCode.NotModified && _snapshot is { } prior)
                snapshot = new(prior.ServiceName, prior.SourceVersion, now, now + _options.TimeToLive, now + _options.TimeToLive + _options.MaxStale, prior.Endpoints);
            else
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > _options.MaximumBytes) throw new ResilienceDiscoveryException(_options.ServiceName, "目录内容超限。");
                using var buffer = new MemoryStream();
                await using var stream = await response.Content.ReadAsStreamAsync(budget.Token).ConfigureAwait(false);
                var bytes = new byte[8192];
                while (true)
                {
                    var count = await stream.ReadAsync(bytes.AsMemory(), budget.Token).ConfigureAwait(false);
                    if (count == 0) break;
                    if (buffer.Length + count > _options.MaximumBytes) throw new ResilienceDiscoveryException(_options.ServiceName, "目录内容超限。");
                    buffer.Write(bytes, 0, count);
                }
                var documentBytes = buffer.ToArray();
                if (_options.VerifyDocument is not null && !_options.VerifyDocument(documentBytes))
                { lock (_gate) _snapshot = null; throw new ResilienceDiscoveryException(_options.ServiceName, "目录文档签名校验失败。"); }
                var document = JsonSerializer.Deserialize<HttpEndpointDirectoryDocument>(documentBytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new ResilienceDiscoveryException(_options.ServiceName, "目录文档为空。");
                if (document.ServiceName != _options.ServiceName || document.Endpoints is null) throw new ResilienceDiscoveryException(_options.ServiceName, "目录服务身份不匹配。");
                snapshot = new(document.ServiceName, document.Version, now, now + _options.TimeToLive, now + _options.TimeToLive + _options.MaxStale, document.Endpoints);
            }
            ConfigurationServiceEndpointSource.Validate(snapshot, _trust, _options.MaximumEndpoints);
            lock (_gate)
            {
                foreach (var key in _revocations.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) _revocations.Remove(key);
                foreach (var key in _identities.Where(x => x.Value.Until <= now && !_revocations.ContainsKey(x.Key)).Select(x => x.Key).ToArray()) _identities.Remove(key);
                foreach (var endpoint in snapshot.Endpoints)
                    if (_identities.TryGetValue(endpoint.StateKey, out var identity) && identity.Origin != endpoint.Origin)
                        throw new ResilienceConfigurationException("目录实例 Origin 变更必须更换 Incarnation。");
                if (_identities.Count + snapshot.Endpoints.Count(e => !_identities.ContainsKey(e.StateKey)) > _options.MaximumEndpoints * 4)
                    throw new ResilienceConfigurationException("目录历史身份达到上限，拒绝继续扩张。");
                foreach (var endpoint in snapshot.Endpoints) _identities[endpoint.StateKey] = (endpoint.Origin, snapshot.MaxStaleUntil);
                _snapshot = snapshot; _etag = response.Headers.ETag ?? _etag; _failure = null; _retryAt = default;
            }
            return snapshot;
        }
        catch (Exception error)
        {
            lock (_gate) { _failure = error; _retryAt = _clock.GetUtcNow() + _options.FailureBackoff; }
            throw;
        }
        finally { lock (_gate) _refresh = null; }
    }
    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        _timer.Dispose(); _shutdown.Cancel(); _client.Dispose();
        // 正在结束的刷新仍使用 shutdown.Token；CTS 由刷新完成后的延续释放。
        Task? pending; lock (_gate) pending = _refresh;
        if (pending is null) _shutdown.Dispose();
        else _ = pending.ContinueWith(_ => _shutdown.Dispose(), TaskScheduler.Default);
    }
}
