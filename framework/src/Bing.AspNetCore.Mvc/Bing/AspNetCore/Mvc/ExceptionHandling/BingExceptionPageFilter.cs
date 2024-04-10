using System.Text;
using Bing.AspNetCore.ExceptionHandling;
using Bing.DependencyInjection;
using Bing.Http;
using Bing.Utils.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Bing.ExceptionHandling;
using Bing.Authorization;
using Bing.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Mvc.ExceptionHandling;

/// <summary>
/// 异常页过滤器
/// </summary>
public class BingExceptionPageFilter : IAsyncPageFilter, ITransientDependency
{
    /// <inheritdoc />
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    /// <summary>
    /// Razor Page处理程序执行过程中拦截操作。
    /// </summary>
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (context.HandlerMethod == null || !ShouldHandleException(context))
        {
            await next();
            return;
        }

        var pageHandlerExecutedContext = await next();
        if (pageHandlerExecutedContext.Exception == null)
            return;
        await HandleAndWrapException(pageHandlerExecutedContext);
    }

    /// <summary>
    /// 是否应该处理异常
    /// </summary>
    /// <param name="context">页面处理器执行中上下文</param>
    protected virtual bool ShouldHandleException(PageHandlerExecutingContext context)
    {
        if (context.ActionDescriptor.IsPageAction() && ActionResultHelper.IsObjectResult(context.HandlerMethod!.MethodInfo.ReturnType, typeof(void)))
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
    /// <param name="context">页面处理器执行后上下文</param>
    protected virtual async Task HandleAndWrapException(PageHandlerExecutedContext context)
    {
        if (context.ExceptionHandled)
            return;
        var exceptionHandlingOptions = context.GetRequiredService<IOptions<BingExceptionHandlingOptions>>().Value;
        var exceptionToErrorInfoConverter = context.GetRequiredService<IExceptionToErrorInfoConverter>();
        var remoteServiceErrorInfo = exceptionToErrorInfoConverter.Convert(context.Exception, options =>
        {
            options.SendExceptionDetailsToClients = exceptionHandlingOptions.SendExceptionDetailsToClients;
            options.SendStackTraceToClients = exceptionHandlingOptions.SendStackTraceToClients;
        });

        var remoteServiceErrorInfoBuilder = new StringBuilder();
        remoteServiceErrorInfoBuilder.AppendLine($"---------- {nameof(RemoteServiceErrorInfo)} ----------");
        remoteServiceErrorInfoBuilder.AppendLine(JsonHelper.ToJson(remoteServiceErrorInfo, indented: true));

        var logger = context.GetService<ILogger<BingExceptionPageFilter>>(NullLogger<BingExceptionPageFilter>.Instance);
        var logLevel = context.Exception!.GetLogLevel();
        logger.LogWithLevel(logLevel, remoteServiceErrorInfoBuilder.ToString());
        logger.LogException(context.Exception!, logLevel);

        await context.GetRequiredService<IExceptionNotifier>().NotifyAsync(new ExceptionNotificationContext(context.Exception!));

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
                .GetStatusCode(context.HttpContext, context.Exception!);

            context.Result = new ApiResult(Conv.ToInt(remoteServiceErrorInfo.Code), remoteServiceErrorInfo.Message);
        }

        context.ExceptionHandled = true;
    }
}
