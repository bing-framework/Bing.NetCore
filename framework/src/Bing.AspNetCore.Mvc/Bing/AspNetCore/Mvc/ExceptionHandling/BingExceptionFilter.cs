using System;
using System.Text;
using System.Threading.Tasks;
using Bing.AspNetCore.ExceptionHandling;
using Bing.DependencyInjection;
using Bing.ExceptionHandling;
using Bing.Helpers;
using Bing.Http;
using Bing.Utils.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
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
            return;
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
        context.HttpContext.Response.Headers.Add(BingHttpConst.BingErrorFormat, "true");
        context.HttpContext.Response.StatusCode = (int)context
            .GetRequiredService<IHttpExceptionStatusCodeFinder>()
            .GetStatusCode(context.HttpContext, context.Exception);

        var exceptionHandlingOptions = context.GetRequiredService<IOptions<BingExceptionHandlingOptions>>().Value;
        var exceptionToErrorInfoConverter = context.GetRequiredService<IExceptionToErrorInfoConverter>();
        var remoteServiceErrorInfo = exceptionToErrorInfoConverter.Convert(context.Exception, exceptionHandlingOptions.SendExceptionDetailsToClients);

        // TODO: 此处考虑是否还要抽象个对象存放信息
        context.Result = new ApiResult(Conv.ToInt(remoteServiceErrorInfo.Code), remoteServiceErrorInfo.Message);

        var logLevel = context.Exception.GetLogLevel();

        var remoteServiceErrorInfoBuilder = new StringBuilder();
        remoteServiceErrorInfoBuilder.AppendLine($"---------- {nameof(RemoteServiceErrorInfo)} ----------");
        remoteServiceErrorInfoBuilder.AppendLine(JsonHelper.ToJson(remoteServiceErrorInfo, indented: true));

        var logger = context.GetService<ILogger<BingExceptionFilter>>(NullLogger<BingExceptionFilter>.Instance);
        logger.LogWithLevel(logLevel,remoteServiceErrorInfoBuilder.ToString());
        logger.LogException(context.Exception,logLevel);

        await context.GetRequiredService<IExceptionNotifier>().NotifyAsync(new ExceptionNotificationContext(context.Exception));
        context.Exception = null;
    }
}