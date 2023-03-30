using Bing.Aspects;
using Bing.DependencyInjection;
using Bing.Helpers;
using Bing.Logging;
using Bing.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// Bing抽象控制器基类
/// </summary>
public abstract class BingControllerBase : ControllerBase
{
    /// <summary>
    /// Lazy延迟加载服务提供程序
    /// </summary>
    [Autowired]
    public virtual ILazyServiceProvider LazyServiceProvider { get; set; }

    /// <summary>
    /// 当前用户
    /// </summary>
    protected ICurrentUser CurrentUser => LazyServiceProvider.LazyGetRequiredService<ICurrentUser>();

    /// <summary>
    /// 日志工厂
    /// </summary>
    protected ILogFactory LogFactory => LazyServiceProvider.LazyGetRequiredService<ILogFactory>();

    /// <summary>
    /// 日志
    /// </summary>
    protected ILog Log => LazyServiceProvider.LazyGetService<ILog>(provider => LogFactory?.CreateLog(GetType().FullName) ?? NullLog.Instance);

    /// <summary>
    /// 返回成功消息
    /// </summary>
    /// <param name="data">数据</param>
    /// <param name="message">消息</param>
    /// <param name="statusCode">Http状态码</param>
    protected virtual IActionResult Success(dynamic data = null, string message = null, int? statusCode = 200)
    {
        message ??= Bing.Properties.R.Success;
        return GetResult(Mvc.StatusCode.Ok.ToString("d"), message, data, statusCode);
    }

    /// <summary>
    /// 返回失败消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="statusCode">Http状态码</param>
    protected virtual IActionResult Fail(string message, int? statusCode = 200)
    {
        return GetResult(Bing.AspNetCore.Mvc.StatusCode.Fail.ToString("d"), message, null, statusCode);
    }

    /// <summary>
    /// 获取结果
    /// </summary>
    /// <param name="code">业务状态码</param>
    /// <param name="message">消息</param>
    /// <param name="data">数据</param>
    /// <param name="httpStatusCode">Http状态码</param>
    private IActionResult GetResult(string code, string message, dynamic data, int? httpStatusCode)
    {
        var factory = HttpContext.RequestServices.GetService<IResultFactory>();
        if (factory == null)
            return new ApiResult(Conv.ToInt(code), message, data, httpStatusCode);
        return factory.Create(code, message, data, httpStatusCode);
    }
}
