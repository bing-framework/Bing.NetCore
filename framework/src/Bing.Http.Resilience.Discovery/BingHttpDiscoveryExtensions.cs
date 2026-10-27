using Bing.Resilience;
using Bing.Resilience.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Http.Resilience.Discovery;

public sealed class BingHttpDiscoveryOptions
{
    public string ServiceName { get; set; } = "";
    public Uri LogicalOrigin { get; set; } = null!;
    public string ContractRevision { get; set; } = "";
    public string ConsistencyGroup { get; set; } = "";
    public string CredentialAudience { get; set; } = "";
    public int MaximumPartitions { get; set; } = 128;
    public TimeSpan StateRetention { get; set; } = TimeSpan.FromMinutes(5);
}
public static class BingHttpDiscoveryExtensions
{
    public static IHttpClientBuilder AddBingHttpDiscovery(this IHttpClientBuilder builder, Action<BingHttpDiscoveryOptions> configure)
    {
        var o = new BingHttpDiscoveryOptions(); configure(o);
        if (string.IsNullOrWhiteSpace(o.ServiceName) || string.IsNullOrWhiteSpace(o.ContractRevision) ||
            string.IsNullOrWhiteSpace(o.ConsistencyGroup) || string.IsNullOrWhiteSpace(o.CredentialAudience) ||
            o.LogicalOrigin is null || !o.LogicalOrigin.IsAbsoluteUri || o.LogicalOrigin.Scheme is not ("http" or "https") ||
            o.LogicalOrigin.AbsolutePath != "/" || o.LogicalOrigin.Query.Length != 0 || o.LogicalOrigin.UserInfo.Length != 0 ||
            o.MaximumPartitions < 1 || o.StateRetention < TimeSpan.Zero)
            throw new ResilienceConfigurationException("HTTP 发现绑定必须声明服务、逻辑 Origin、契约、一致性组和凭据 audience。");
        if (builder.Services.Any(x => x.ImplementationInstance is Binding b && b.ClientName == builder.Name))
            throw new ResilienceConfigurationException("该客户端已绑定动态发现。");
        var frozen = new Binding(builder.Name, o.ServiceName, o.LogicalOrigin, o.ContractRevision, o.ConsistencyGroup, o.CredentialAudience, o.MaximumPartitions, o.StateRetention);
        builder.Services.AddSingleton(frozen);
        builder.Services.TryAddSingleton<IServiceEndpointSelector>(sp => new WeightedRoundRobinServiceEndpointSelector(
            timeProvider: sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<IBingHttpRoutingStrategy>(sp => new Routing(frozen,
            sp.GetRequiredService<IServiceEndpointSource>(), sp.GetRequiredService<IServiceEndpointSelector>(),
            (IResiliencePartitionedPipelineProvider)sp.GetRequiredService<IResiliencePipelineProvider>(), sp.GetRequiredService<TimeProvider>()));
        return builder;
    }
    private sealed record Binding(string ClientName, string Service, Uri LogicalOrigin, string Revision, string Group, string Audience, int Maximum, TimeSpan Retention);
    private sealed class Routing(Binding binding, IServiceEndpointSource source, IServiceEndpointSelector selector,
        IResiliencePartitionedPipelineProvider partitions, TimeProvider clock) : IBingHttpRoutingStrategy
    {
        public string ClientName => binding.ClientName;
        public async ValueTask<IBingHttpRoutingLease> AcquireAsync(HttpRequestMessage request, ResilienceExecutionContext context, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null || !request.RequestUri.IsAbsoluteUri ||
                !string.Equals(request.RequestUri.GetLeftPart(UriPartial.Authority), binding.LogicalOrigin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                throw new ResilienceConfigurationException("动态路由原请求不属于登记的逻辑 Origin。");
            var snapshot = await source.GetSnapshotAsync(binding.Service, cancellationToken).ConfigureAwait(false);
            if (snapshot.ServiceName != binding.Service || snapshot.MaxStaleUntil <= clock.GetUtcNow())
                throw new ResilienceDiscoveryException(binding.Service, "来源快照身份或有效期无效。");
            var eligible = snapshot.Endpoints.Where(e => e.ContractRevision == binding.Revision &&
                e.ConsistencyGroup == binding.Group && e.CredentialAudience == binding.Audience && !source.IsRevoked(binding.Service, e)).ToArray();
            var selected = selector.Select(new(snapshot.ServiceName, snapshot.SourceVersion, snapshot.FetchedAt,
                snapshot.ExpiresAt, snapshot.MaxStaleUntil, eligible)).ToArray();
            if (selected.Any(e => !eligible.Contains(e) || (binding.LogicalOrigin.Scheme == "https" && e.Origin.Scheme != "https")))
                throw new ResilienceConfigurationException("选择器返回了快照外实例或降级 TLS 的端点。");
            if (selected.Length == 0) throw new ResilienceDiscoveryException(binding.Service, "没有满足服务契约与权限约束的端点。");
            var leases = new List<(ServiceEndpoint Endpoint, IResiliencePartitionLease<HttpResponseMessage> Lease)>();
            try
            {
                foreach (var endpoint in selected)
                    leases.Add((endpoint, partitions.AcquirePartition<HttpResponseMessage>(ClientName, "http.endpoint",
                        "dynamic:" + Uri.EscapeDataString(binding.Service) + ":" + endpoint.StateKey, binding.Maximum, binding.Retention)));
                return new RouteLease(binding.Service, snapshot, leases, source, clock);
            }
            catch { foreach (var lease in leases) lease.Lease.Dispose(); throw; }
        }
    }
    private sealed class RouteLease(string service, ServiceEndpointSnapshot snapshot,
        List<(ServiceEndpoint Endpoint, IResiliencePartitionLease<HttpResponseMessage> Lease)> leases,
        IServiceEndpointSource source, TimeProvider clock) : IBingHttpRoutingLease
    {
        private bool _disposed;
        public BingHttpRouteEndpoint Select(int attemptNumber)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RouteLease));
            var endpoint = leases[attemptNumber % leases.Count].Endpoint;
            return new(endpoint.StateKey, endpoint.Origin);
        }
        public void ValidateBeforeSend(BingHttpRouteEndpoint endpoint)
        {
            var entry = Find(endpoint);
            if (snapshot.MaxStaleUntil <= clock.GetUtcNow() || source.IsRevoked(service, entry.Endpoint))
                throw new ResilienceDiscoveryException(service, "端点快照已过期或已安全撤销。");
        }
        public IResiliencePipeline<HttpResponseMessage> GetPipeline(BingHttpRouteEndpoint endpoint) => Find(endpoint).Lease.Pipeline;
        private (ServiceEndpoint Endpoint, IResiliencePartitionLease<HttpResponseMessage> Lease) Find(BingHttpRouteEndpoint endpoint)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RouteLease));
            return leases.Single(x => x.Endpoint.StateKey == endpoint.Id && x.Endpoint.Origin == endpoint.Origin);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            foreach (var lease in leases) lease.Lease.Dispose();
        }
    }
}
