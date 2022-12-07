using System;
using System.Net;
using Bing.DependencyInjection;
using Bing.ExceptionHandling;
using Bing.Exceptions;
using Bing.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.ExceptionHandling;

/// <summary>
/// Http异常状态码查找器
/// </summary>
public class DefaultHttpExceptionStatusCodeFinder : IHttpExceptionStatusCodeFinder, ITransientDependency
{
    /// <summary>
    /// 异常Http状态码选项配置
    /// </summary>
    protected BingExceptionHttpStatusCodeOptions Options { get; }

    /// <summary>
    /// 初始化一个<see cref="DefaultHttpExceptionStatusCodeFinder"/>类型的实例
    /// </summary>
    /// <param name="options">异常Http状态码选项配置</param>
    public DefaultHttpExceptionStatusCodeFinder(IOptions<BingExceptionHttpStatusCodeOptions> options)
    {
        Options = options.Value;
    }

    /// <summary>
    /// 获取状态码
    /// </summary>
    /// <param name="httpContext">Http上下文</param>
    /// <param name="exception">异常</param>
    public virtual HttpStatusCode GetStatusCode(HttpContext httpContext, Exception exception)
    {
        // 如果设置了Http状态码，则返回指定Http状态码
        if (exception is IHasHttpStatusCode exceptionWithHttpStatusCode && exceptionWithHttpStatusCode.HttpStatusCode > 0)
            return (HttpStatusCode)exceptionWithHttpStatusCode.HttpStatusCode;

        // 如果已配置字典【错误码 - Http状态码】，则返回指定Http状态码
        if (exception is IHasErrorCode exceptionWithErrorCode && !exceptionWithErrorCode.Code.IsNullOrWhiteSpace())
        {
            if (Options.ErrorCodeToHttpStatusCodeMappings.TryGetValue(exceptionWithErrorCode.Code, out var status))
                return status;
        }

        if (Options.GlobalHttpStatusCode200)
            return HttpStatusCode.OK;

        if (exception is ConcurrencyException)
            return HttpStatusCode.Conflict;

        if (exception is NotImplementedException)
            return HttpStatusCode.NotImplemented;

        if (exception is IBusinessException)
            return HttpStatusCode.Forbidden;

        return HttpStatusCode.InternalServerError;
    }
}