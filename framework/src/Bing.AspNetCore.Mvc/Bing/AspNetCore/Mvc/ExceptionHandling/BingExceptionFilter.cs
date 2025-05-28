using System.Text;
using Bing.AspNetCore.ExceptionHandling;
using Bing.Authorization;
using Bing.DependencyInjection;
using Bing.ExceptionHandling;
using Bing.Helpers;
using Bing.Http;
using Bing.Utils.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Mvc.ExceptionHandling;

/// <summary>
/// 异常过滤器
/// </summary>
public class BingExceptionFilter : IAsyncExceptionFilter, ITransientDependency
{
    /// <summary>
    /// 异常处理
    /// </summary>
    /// <param name="context">异常上下文</param>
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (!ShouldHandleException(context))
        {
            LogException(context, out _);
            return;
        }
        await HandleAndWrapException(context);
    }

    /// <summary>
    /// 是否应该处理异常
    /// </summary>
    /// <param name="context">异常上下文</param>
    protected virtual bool ShouldHandleException(ExceptionContext context)
    {
        if (context.ActionDescriptor.IsControllerAction() && context.ActionDescriptor.HasObjectResult())
            return true;
        if (context.HttpContext.Request.CanAccept(MimeTypes.Application.Json))
            return true;
        if (context.HttpContext.Request.IsAjax())
            return true;
        return false;
    }

    /// <summary>
    /// 处理并包装异常
    /// </summary>
    /// <param name="context">异常上下文</param>
    protected virtual async Task HandleAndWrapException(ExceptionContext context)
    {
        LogException(context, out var remoteServiceErrorInfo);

        await context.GetRequiredService<IExceptionNotifier>().NotifyAsync(new ExceptionNotificationContext(context.Exception));

        if (context.Exception is BingAuthorizationException)
        {
            await context.HttpContext.RequestServices.GetRequiredService<IBingAuthorizationExceptionHandler>()
                .HandleAsync(context.Exception.As<BingAuthorizationException>(), context.HttpContext);
        }
        else
        {
            context.HttpContext.Response.Headers.Add(BingHttpConst.BingErrorFormat, "true");
            context.HttpContext.Response.StatusCode = (int)context
                .GetRequiredService<IHttpExceptionStatusCodeFinder>()
                .GetStatusCode(context.HttpContext, context.Exception);

            context.Result = new ApiResult(Conv.ToInt(remoteServiceErrorInfo.Code), remoteServiceErrorInfo.Message);
        }

        context.Exception = null!; // Handled!
    }

    /// <summary>
    /// 记录异常信息并输出转换后的远程服务信息。
    /// </summary>
    /// <param name="context">异常上下文</param>
    /// <param name="remoteServiceErrorInfo">输出参数，转换后的远程服务错误信息。</param>
    protected virtual void LogException(ExceptionContext context, out RemoteServiceErrorInfo remoteServiceErrorInfo)
    {
        var exceptionHandlingOptions = context.GetRequiredService<IOptions<BingExceptionHandlingOptions>>().Value;
        var exceptionToErrorInfoConverter = context.GetRequiredService<IExceptionToErrorInfoConverter>();
        remoteServiceErrorInfo = exceptionToErrorInfoConverter.Convert(context.Exception, options =>
        {
            options.SendExceptionDetailsToClients = exceptionHandlingOptions.SendExceptionDetailsToClients;
            options.SendStackTraceToClients = exceptionHandlingOptions.SendStackTraceToClients;
        });

        var remoteServiceErrorInfoBuilder = new StringBuilder();
        remoteServiceErrorInfoBuilder.AppendLine($"---------- {nameof(RemoteServiceErrorInfo)} ----------");
        remoteServiceErrorInfoBuilder.AppendLine(JsonHelper.ToJson(remoteServiceErrorInfo, indented: true));

        var logger = context.GetService<ILogger<BingExceptionFilter>>(NullLogger<BingExceptionFilter>.Instance);
        var logLevel = context.Exception.GetLogLevel();
        logger.LogWithLevel(logLevel, remoteServiceErrorInfoBuilder.ToString());
        logger.LogException(context.Exception, logLevel);
    }
}
