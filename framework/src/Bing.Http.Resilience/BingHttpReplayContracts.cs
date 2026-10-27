using Bing.Resilience;

namespace Bing.Http.Resilience;

/// <summary>可靠写入的中性元数据；元数据本身不授予权限，许可仍由宿主目录验证。</summary>
public static class BingHttpOperationMetadata
{
    public static HttpRequestOptionsKey<ResilienceOperationDescriptor> Operation { get; } = new("Bing.Resilience.Operation");
    public static HttpRequestOptionsKey<string> IdempotencyKey { get; } = new("Bing.Resilience.IdempotencyKey");
    public static HttpRequestOptionsKey<string> AccessScope { get; } = new("Bing.Resilience.AccessScope");
    public static HttpRequestOptionsKey<bool> IsStreamResult { get; } = new("Bing.Resilience.IsStreamResult");
    public static HttpRequestOptionsKey<bool> IsDefinition { get; } = new("Bing.Resilience.IsDefinition");
    /// <summary>持久 Worker 已拥有尝试账本，内层 HTTP 不得再调度重放。</summary>
    public static HttpRequestOptionsKey<bool> DurableAttempt { get; } = new("Bing.Resilience.DurableAttempt");
}
public interface IBingHttpReplayStrategy
{
    string ClientName { get; }
    ValueTask<IBingHttpReplaySnapshot?> CaptureAsync(HttpRequestMessage request, CancellationToken token);
}
public interface IBingHttpReplaySnapshot : IDisposable
{
    string BodyFingerprint { get; }
    ValueTask<HttpRequestMessage> CreateRequestAsync(Uri? origin, CancellationToken token);
}
