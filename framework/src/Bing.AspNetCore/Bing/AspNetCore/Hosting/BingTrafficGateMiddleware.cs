using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Hosting;

/// <summary>
/// 处理就绪探针并阻止排空期间的新 HTTP 请求。
/// </summary>
public sealed class BingTrafficGateMiddleware
{
    /// <summary>
    /// 后续请求处理器。
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 流量闸门。
    /// </summary>
    private readonly IBingTrafficGate _gate;

    /// <summary>
    /// 闸门配置。
    /// </summary>
    private readonly BingTrafficGateOptions _options;

    /// <summary>
    /// 初始化一个 <see cref="BingTrafficGateMiddleware" /> 类型的实例。
    /// </summary>
    /// <param name="next">后续 HTTP 请求处理委托。</param>
    /// <param name="gate">用于判定 HTTP 请求是否可通过的流量闸门。</param>
    /// <param name="options">用于初始化实例的配置选项。</param>
    public BingTrafficGateMiddleware(RequestDelegate next, IBingTrafficGate gate,
        IOptions<BingTrafficGateOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.ReadinessPath))
            throw new ArgumentException("就绪探针路径不能为空。", nameof(options));
        if (_options.DrainTimeout < Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(options), "排空超时时间无效。");
        if (_options.UnavailableStatusCode < 100 || _options.UnavailableStatusCode > 599)
            throw new ArgumentOutOfRangeException(nameof(options), "排空状态码无效。");
    }

    /// <summary>
    /// 处理就绪探针或登记在途请求。
    /// </summary>
    /// <param name="context">当前 HTTP 请求上下文。</param>
    /// <remarks>就绪探针根据闸门状态返回成功或配置的不可用状态码；其他请求在闸门关闭时返回不可用状态，并设置一秒重试间隔。</remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        if (string.Equals(context.Request.Path.Value, _options.ReadinessPath,
            StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = _gate.IsReady ? StatusCodes.Status200OK : _options.UnavailableStatusCode;
            return;
        }

        if (!_gate.TryEnter(out var lease))
        {
            context.Response.StatusCode = _options.UnavailableStatusCode;
            context.Response.Headers["Retry-After"] = "1";
            return;
        }

        using (lease)
            await _next(context).ConfigureAwait(false);
    }
}
