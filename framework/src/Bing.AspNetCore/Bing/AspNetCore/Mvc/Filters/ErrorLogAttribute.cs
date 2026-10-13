using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.AspNetCore.Mvc.Filters;

/// <summary>
/// 错误日志过滤器
/// </summary>
public class ErrorLogAttribute : ExceptionFilterAttribute
{
    /// <inheritdoc />
    public override void OnException(ExceptionContext context)
    {
        if (context == null)
            return;
        //var log = Log.GetLog(context).Caption("WebApi全局异常");
        //context.Exception.Log(log);
    }
}
