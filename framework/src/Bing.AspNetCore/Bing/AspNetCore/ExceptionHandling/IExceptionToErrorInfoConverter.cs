using System;
using Bing.Http;

namespace Bing.AspNetCore.ExceptionHandling;

/// <summary>
/// 错误信息转换器
/// </summary>
public interface IExceptionToErrorInfoConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="includeSensitiveDetails">是否包含敏感信息</param>
    RemoteServiceErrorInfo Convert(Exception exception, bool includeSensitiveDetails);
}