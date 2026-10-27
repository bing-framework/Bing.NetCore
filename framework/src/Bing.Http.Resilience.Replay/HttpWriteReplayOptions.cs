using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Http.Resilience.Replay;

/// <summary>可重开的不可变大载荷。宿主必须保证每次打开得到相同字节，且版本在整个重放期限内可用。</summary>
public interface IHttpReplayPayload
{
    long Length { get; }
    string Fingerprint { get; }
    ValueTask<Stream> OpenReadAsync(CancellationToken token);
}
/// <summary>接收端已部署的同键取回结果协议；保留期必须覆盖写入的最长恢复窗口。</summary>
public sealed record HttpIdempotencyContract(string Version, string Scope, TimeSpan Retention, bool SharedAcrossEndpoints);

public sealed class HttpWriteReplayOptions
{
    public bool Enabled { get; set; }
    public bool RequireReplayableBody { get; set; } = true;
    public int MaxBufferedBytes { get; set; } = 1024 * 1024;
    public long MaxPayloadBytes { get; set; } = 64 * 1024 * 1024;
    public TimeSpan ReplayWindow { get; set; } = TimeSpan.FromMinutes(5);
    public string IdempotencyHeader { get; set; } = "Idempotency-Key";
    /// <summary>宿主白名单，同时返回已部署且与操作一致的服务端契约。</summary>
    public Func<ResilienceOperationDescriptor, HttpIdempotencyContract?>? GetContract { get; set; }
    public Func<HttpRequestMessage, bool>? CanReplayWrite { get; set; }
    /// <summary>任意自定义 Content 仅在显式声明可以有界读取时缓冲；默认仅内存 Content。</summary>
    public Func<HttpRequestMessage, bool>? CanBufferContent { get; set; }
    public Func<HttpRequestMessage, IHttpReplayPayload?>? GetPayload { get; set; }
    public ISet<string> ForwardHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Accept", "Accept-Language", "Content-Type" };
    public ISet<string> SignatureHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, Func<object?, object?>> OptionCopiers { get; } = new Dictionary<string, Func<object?, object?>>(StringComparer.Ordinal);
    internal HttpWriteReplayOptions Freeze()
    {
        if (MaxBufferedBytes <= 0 || MaxPayloadBytes < MaxBufferedBytes || ReplayWindow <= TimeSpan.Zero ||
            ReplayWindow > TimeSpan.FromDays(30) || string.IsNullOrWhiteSpace(IdempotencyHeader) ||
            IdempotencyHeader.Any(x => !char.IsLetterOrDigit(x) && x != '-'))
            throw new ResilienceConfigurationException("写重放大小、时间或幂等头配置无效。");
        if (Forbidden(IdempotencyHeader)) throw new ResilienceConfigurationException("幂等头不能使用身份或签名头。");
        if (Enabled && (GetContract is null || CanReplayWrite is null)) throw new ResilienceConfigurationException("写重放需要独立许可和服务端幂等协议。");
        var copy = new HttpWriteReplayOptions { Enabled = Enabled, RequireReplayableBody = RequireReplayableBody,
            MaxBufferedBytes = MaxBufferedBytes, MaxPayloadBytes = MaxPayloadBytes, ReplayWindow = ReplayWindow,
            IdempotencyHeader = IdempotencyHeader, GetContract = GetContract, CanReplayWrite = CanReplayWrite,
            CanBufferContent = CanBufferContent, GetPayload = GetPayload };
        copy.ForwardHeaders.Clear(); foreach (var h in ForwardHeaders) copy.ForwardHeaders.Add(h);
        foreach (var h in SignatureHeaders) copy.SignatureHeaders.Add(h);
        foreach (var p in OptionCopiers) copy.OptionCopiers.Add(p.Key, p.Value);
        return copy;
    }
    internal bool Forbidden(string name) => name.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) || name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) || SignatureHeaders.Contains(name);
}
public static class HttpWriteReplayExtensions
{
    public static IHttpClientBuilder AddBingHttpWriteReplay(this IHttpClientBuilder builder, Action<HttpWriteReplayOptions> configure)
    {
        if (builder.Services.Any(x => x.ImplementationInstance is ReplayRegistration r && r.ClientName == builder.Name))
            throw new ResilienceConfigurationException("写重放策略重复登记。");
        var options = new HttpWriteReplayOptions(); configure(options);
        var registration = new ReplayRegistration(builder.Name, options.Freeze());
        builder.Services.AddSingleton(registration);
        builder.Services.AddSingleton<IBingHttpReplayStrategy>(sp => new HttpWriteReplayStrategy(registration.ClientName,
            registration.Options, sp.GetRequiredService<Bing.Resilience.Operations.ResilienceOperationCatalog>()));
        return builder;
    }
    private sealed record ReplayRegistration(string ClientName, HttpWriteReplayOptions Options);
}
