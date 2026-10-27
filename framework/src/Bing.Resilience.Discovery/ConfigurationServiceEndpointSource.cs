namespace Bing.Resilience.Discovery;

/// <summary>版本 CAS 发布已审核配置；允许旧调用有限排空，安全撤销立即阻止尚未发送的分支。</summary>
public sealed class ConfigurationServiceEndpointSource(IServiceEndpointTrustPolicy trust, TimeProvider clock, int maximumEndpoints = 128) : IServiceEndpointSource
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ServiceEndpointSnapshot> _snapshots = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Service, string State), (Uri Origin, DateTimeOffset Until)> _identities = new();
    private readonly Dictionary<(string Service, string State), DateTimeOffset> _revocations = new();
    public void Publish(ServiceEndpointSnapshot snapshot, string? expectedVersion)
    {
        try { PublishCore(snapshot, expectedVersion); ResilienceDiagnostics.RecordLifecycle("discovery.refresh", "success"); }
        catch { ResilienceDiagnostics.RecordLifecycle("discovery.refresh", "failure"); throw; }
    }
    private void PublishCore(ServiceEndpointSnapshot snapshot, string? expectedVersion)
    {
        Validate(snapshot, trust, maximumEndpoints);
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            if (snapshot.FetchedAt > now || snapshot.MaxStaleUntil <= now) throw new ResilienceConfigurationException("不能发布未来或已过期快照。");
            _snapshots.TryGetValue(snapshot.ServiceName, out var current);
            if (!string.Equals(current?.SourceVersion, expectedVersion, StringComparison.Ordinal))
                throw new ResilienceConfigurationException("发现源发布版本冲突。");
            if (current is not null && current.SourceVersion == snapshot.SourceVersion)
                throw new ResilienceConfigurationException("刷新必须使用新的来源版本。");
            foreach (var key in _revocations.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) _revocations.Remove(key);
            foreach (var key in _identities.Where(x => x.Value.Until <= now && !_revocations.ContainsKey(x.Key)).Select(x => x.Key).ToArray()) _identities.Remove(key);
            foreach (var endpoint in snapshot.Endpoints)
            {
                if (_identities.TryGetValue((snapshot.ServiceName, endpoint.StateKey), out var identity) && identity.Origin != endpoint.Origin)
                    throw new ResilienceConfigurationException("实例 Origin 变更必须更换 Incarnation。");
            }
            var newCount = snapshot.Endpoints.Count(x => !_identities.ContainsKey((snapshot.ServiceName, x.StateKey)));
            if (_identities.Count + newCount > maximumEndpoints * 4) throw new ResilienceConfigurationException("发现源历史身份已达到上限。");
            foreach (var endpoint in snapshot.Endpoints)
                _identities[(snapshot.ServiceName, endpoint.StateKey)] = (endpoint.Origin, snapshot.MaxStaleUntil);
            _snapshots[snapshot.ServiceName] = snapshot;
        }
    }
    public void Revoke(string serviceName, string endpointId, string incarnation)
    {
        if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(endpointId) || string.IsNullOrWhiteSpace(incarnation) ||
            serviceName.Length > 128 || endpointId.Length > 128 || incarnation.Length > 128)
            throw new ResilienceConfigurationException("撤销端点身份无效。");
        lock (_gate)
        {
            var key = (serviceName, Uri.EscapeDataString(endpointId) + ":" + Uri.EscapeDataString(incarnation));
            if (_identities.TryGetValue(key, out var identity))
            {
                _revocations[key] = identity.Until;
                return;
            }
            if (!_snapshots.TryGetValue(serviceName, out var snapshot) || !snapshot.Endpoints.Any(x => x.StateKey == key.Item2))
                throw new ResilienceConfigurationException("撤销端点身份未在发现历史中登记。");
            _revocations[key] = snapshot.MaxStaleUntil;
        }
    }
    public bool IsRevoked(string serviceName, ServiceEndpoint endpoint)
    {
        lock (_gate)
        {
            var key = (serviceName, endpoint.StateKey);
            if (!_revocations.TryGetValue(key, out var until)) return false;
            if (until <= clock.GetUtcNow()) { _revocations.Remove(key); return false; }
            return true;
        }
    }
    public async ValueTask<ServiceEndpointSnapshot> GetSnapshotAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await GetSnapshotCoreAsync(serviceName, cancellationToken).ConfigureAwait(false);
            if (snapshot.ExpiresAt <= clock.GetUtcNow()) ResilienceDiagnostics.RecordLifecycle("discovery.stale", "served");
            return snapshot;
        }
        catch (OperationCanceledException) { ResilienceDiagnostics.RecordLifecycle("discovery.read", "cancelled"); throw; }
        catch { ResilienceDiagnostics.RecordLifecycle("discovery.read", "failure"); throw; }
    }
    private ValueTask<ServiceEndpointSnapshot> GetSnapshotCoreAsync(string serviceName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_snapshots.TryGetValue(serviceName, out var snapshot) || snapshot.MaxStaleUntil <= clock.GetUtcNow())
                throw new ResilienceDiscoveryException(serviceName, "没有有效的服务发现快照。");
            return new(snapshot);
        }
    }
    internal static void Validate(ServiceEndpointSnapshot snapshot, IServiceEndpointTrustPolicy trust, int maximum)
    {
        if (maximum < 1 || snapshot.Endpoints.Count > maximum) throw new ResilienceConfigurationException("发现端点数量超限。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in snapshot.Endpoints)
            if (e is null || string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id) || string.IsNullOrWhiteSpace(e.Incarnation) || e.Id.Length > 128 || e.Incarnation.Length > 128 ||
                string.IsNullOrWhiteSpace(e.ContractRevision) || string.IsNullOrWhiteSpace(e.ConsistencyGroup) || string.IsNullOrWhiteSpace(e.CredentialAudience) ||
                e.ContractRevision.Length > 128 || e.ConsistencyGroup.Length > 128 || e.CredentialAudience.Length > 128 || e.Weight <= 0 || e.Origin is null || !e.Origin.IsAbsoluteUri || e.Origin.Scheme is not ("http" or "https") ||
                e.Origin.AbsolutePath != "/" || e.Origin.Query.Length != 0 || e.Origin.Fragment.Length != 0 || e.Origin.UserInfo.Length != 0 ||
                !trust.IsTrusted(snapshot.ServiceName, e))
                throw new ResilienceConfigurationException("发现端点未通过身份、Origin、契约或信任校验。");
    }
}
