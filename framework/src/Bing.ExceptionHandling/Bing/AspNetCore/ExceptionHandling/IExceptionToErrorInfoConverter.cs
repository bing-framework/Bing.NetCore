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
    /// <param name="options">配置操作</param>
    RemoteServiceErrorInfo Convert(Exception exception, Action<BingExceptionHandlingOptions> options = null);
}
