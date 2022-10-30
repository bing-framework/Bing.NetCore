using System;
using Bing.AspNetCore.Mvc;
using Bing.DependencyInjection;
using Bing.Exceptions;
using Bing.Http;

namespace Bing.AspNetCore.ExceptionHandling;

/// <summary>
/// 错误信息转换器
/// </summary>
public class DefaultExceptionToErrorInfoConverter : IExceptionToErrorInfoConverter, ITransientDependency
{
    /// <summary>
    /// 转换
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="includeSensitiveDetails">是否包含敏感信息</param>
    public virtual RemoteServiceErrorInfo Convert(Exception exception, bool includeSensitiveDetails)
    {
        if (exception is ConcurrencyException)
        {
            return new RemoteServiceErrorInfo {Code = "400001", Message = exception.GetPrompt()};
        }

        if (exception is Warning warning)
            return new RemoteServiceErrorInfo
            {
                Code = string.IsNullOrWhiteSpace(warning.Code) ? StatusCode.Fail.ToString() : warning.Code,
                Message = warning.GetPrompt()
            };

        return new RemoteServiceErrorInfo {Code = StatusCode.Fail.ToString(), Message = exception.GetPrompt()};
    }
}