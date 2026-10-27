using Bing.Resilience;

namespace Bing.Http.Resilience;

/// <summary>可选路由扩展。基础 HTTP 包不依赖任何服务发现 SDK。</summary>
public interface IBingHttpRoutingStrategy
{
    string ClientName { get; }
    ValueTask<IBingHttpRoutingLease> AcquireAsync(HttpRequestMessage request, ResilienceExecutionContext context, CancellationToken cancellationToken);
}
public sealed record BingHttpRouteEndpoint(string Id, Uri Origin);
public interface IBingHttpRoutingLease : IDisposable
{
    BingHttpRouteEndpoint Select(int attemptNumber);
    void ValidateBeforeSend(BingHttpRouteEndpoint endpoint);
    IResiliencePipeline<HttpResponseMessage> GetPipeline(BingHttpRouteEndpoint endpoint);
}
