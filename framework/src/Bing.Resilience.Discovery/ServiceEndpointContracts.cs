using System.Collections.ObjectModel;

namespace Bing.Resilience.Discovery;

/// <summary>已验证的服务实例身份。Incarnation 在实例身份重用或 Origin 变更时必须变化。</summary>
public sealed record ServiceEndpoint(string Id, string Incarnation, Uri Origin, string ContractRevision,
    string ConsistencyGroup, string CredentialAudience, int Weight = 1, string? Region = null)
{
    public string StateKey => Uri.EscapeDataString(Id) + ":" + Uri.EscapeDataString(Incarnation);
}
/// <summary>一次执行固定的不可变候选集合；权威空集合不会复用旧实例。</summary>
public sealed class ServiceEndpointSnapshot
{
    public string ServiceName { get; }
    public string SourceVersion { get; }
    public DateTimeOffset FetchedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset MaxStaleUntil { get; }
    public IReadOnlyList<ServiceEndpoint> Endpoints { get; }
    public ServiceEndpointSnapshot(string serviceName, string sourceVersion, DateTimeOffset fetchedAt,
        DateTimeOffset expiresAt, DateTimeOffset maxStaleUntil, IEnumerable<ServiceEndpoint> endpoints)
    {
        if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(sourceVersion) ||
            expiresAt < fetchedAt || maxStaleUntil < expiresAt)
            throw new ResilienceConfigurationException("发现快照身份或有效期无效。");
        ServiceName = serviceName; SourceVersion = sourceVersion; FetchedAt = fetchedAt; ExpiresAt = expiresAt; MaxStaleUntil = maxStaleUntil;
        Endpoints = new ReadOnlyCollection<ServiceEndpoint>(endpoints.ToArray());
    }
}
public interface IServiceEndpointSource
{
    ValueTask<ServiceEndpointSnapshot> GetSnapshotAsync(string serviceName, CancellationToken cancellationToken = default);
    bool IsRevoked(string serviceName, ServiceEndpoint endpoint);
}
public interface IServiceEndpointSelector
{
    IReadOnlyList<ServiceEndpoint> Select(ServiceEndpointSnapshot snapshot);
}
/// <summary>调用内按权重优先和稳定 ID 排序，Hedging 优先不同实例。</summary>
public sealed class OrderedServiceEndpointSelector : IServiceEndpointSelector
{
    public IReadOnlyList<ServiceEndpoint> Select(ServiceEndpointSnapshot snapshot) =>
        Array.AsReadOnly(snapshot.Endpoints.OrderByDescending(x => x.Weight).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray());
}
/// <summary>宿主负责同时验证服务、TLS、目标 origin、契约和凭据 audience。</summary>
public interface IServiceEndpointTrustPolicy
{
    bool IsTrusted(string serviceName, ServiceEndpoint endpoint);
}
public sealed class DelegateServiceEndpointTrustPolicy(Func<string, ServiceEndpoint, bool> validate) : IServiceEndpointTrustPolicy
{
    public bool IsTrusted(string serviceName, ServiceEndpoint endpoint) => validate(serviceName, endpoint);
}
public sealed class ResilienceDiscoveryException(string serviceName, string message, Exception? inner = null) : Exception(message, inner)
{
    public string ServiceName { get; } = serviceName;
}
