using Bing.DependencyInjection;
using Bing.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Threading;

/// <summary>
/// 基于当前HttpContext的异步任务取消令牌提供程序
/// </summary>
[Dependency(ServiceLifetime.Singleton, ReplaceExisting = true)]
public class HttpContextCancellationTokenProvider : ICancellationTokenProvider
{
    /// <summary>
    /// Http上下文访问器
    /// </summary>
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 初始化一个<see cref="HttpContextCancellationTokenProvider"/>类型的实例
    /// </summary>
    /// <param name="httpContextAccessor">Http上下文访问器</param>
    public HttpContextCancellationTokenProvider(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    /// <summary>
    /// 异步任务取消令牌
    /// </summary>
    public CancellationToken Token => _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
}
