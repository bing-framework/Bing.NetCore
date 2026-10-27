namespace Bing.Http.Resilience;

public enum BingHttpResilienceMode { Retry, Hedging }

/// <summary>同服务端点：Origin 仅允许 scheme/host/port，不携带路径或凭据。</summary>
public sealed record BingHttpHedgingEndpoint(string Id, Uri Origin);

/// <summary>HTTP 并行恢复配置；请求头和 Options 复制采用显式许可。</summary>
public sealed class BingHttpHedgingOptions
{
    public int MaxHedgedAttempts { get; set; } = 1;
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(200);
    public int EndpointPermitLimit { get; set; } = 32;
    public int EndpointQueueLimit { get; set; }
    public IList<BingHttpHedgingEndpoint> Endpoints { get; } = new List<BingHttpHedgingEndpoint>();
    public ISet<string> ForwardHeaders { get; } = new HashSet<string>(new[] { "Accept", "Accept-Encoding", "Accept-Language", "User-Agent", "traceparent", "tracestate" }, StringComparer.OrdinalIgnoreCase);
    public ISet<string> SignatureHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>自定义 transport 必须显式验证不会重定向、共享 Cookie，且支持并发发送。</summary>
    public Func<HttpMessageHandler, bool>? ValidateCustomTransport { get; set; }
    private readonly Dictionary<string, Func<object?, object?>> _copiers = new(StringComparer.Ordinal);
    internal IReadOnlyDictionary<string, Func<object?, object?>> OptionCopiers => _copiers;

    public void RegisterOption<T>(HttpRequestOptionsKey<T> key, Func<T, T> copy)
    {
        ArgumentNullException.ThrowIfNull(copy);
        if (!_copiers.TryAdd(key.Key, value => copy((T)value!)))
            throw new ArgumentException($"Options '{key.Key}' 已登记复制器。");
    }
    internal void CopyTo(BingHttpHedgingOptions target)
    {
        target.MaxHedgedAttempts = MaxHedgedAttempts; target.Delay = Delay;
        target.EndpointPermitLimit = EndpointPermitLimit; target.EndpointQueueLimit = EndpointQueueLimit;
        target.ValidateCustomTransport = ValidateCustomTransport;
        target.Endpoints.Clear(); foreach (var endpoint in Endpoints) target.Endpoints.Add(endpoint);
        target.ForwardHeaders.Clear(); foreach (var header in ForwardHeaders) target.ForwardHeaders.Add(header);
        target.SignatureHeaders.Clear(); foreach (var header in SignatureHeaders) target.SignatureHeaders.Add(header);
        target._copiers.Clear(); foreach (var pair in _copiers) target._copiers.Add(pair.Key, pair.Value);
    }
    internal void Validate(bool allowEmpty = false)
    {
        if (MaxHedgedAttempts is < 1 or > 10 || Delay < TimeSpan.Zero || Delay > TimeSpan.FromDays(1) ||
            EndpointPermitLimit < 1 || EndpointQueueLimit < 0 || (!allowEmpty && Endpoints.Count == 0))
            throw new ArgumentException("Hedging 次数、延迟、端点并发或地址池无效。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in Endpoints)
        {
            if (endpoint is null || string.IsNullOrWhiteSpace(endpoint.Id) || !ids.Add(endpoint.Id) ||
                endpoint.Origin is null || !endpoint.Origin.IsAbsoluteUri || endpoint.Origin.Scheme is not ("http" or "https") ||
                endpoint.Origin.AbsolutePath != "/" || endpoint.Origin.Query.Length != 0 ||
                endpoint.Origin.Fragment.Length != 0 || endpoint.Origin.UserInfo.Length != 0)
                throw new ArgumentException("端点 ID 必须唯一且非空，Origin 必须是无路径、查询、片段和凭据的 HTTP(S) 地址。");
        }
        if (Endpoints.Count > 0 && Endpoints[0].Origin.Scheme == "https" && Endpoints.Any(e => e.Origin.Scheme != "https"))
            throw new ArgumentException("HTTPS 主端点的备用端点不能降级为 HTTP。");
        if (ForwardHeaders.Any(h => string.IsNullOrWhiteSpace(h) || ForbiddenHeader(h)))
            throw new ArgumentException("转发白名单包含禁止复制的认证、签名或 Host 请求头。");
    }
    internal bool ForbiddenHeader(string name) =>
        name.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) || SignatureHeaders.Contains(name);
}
