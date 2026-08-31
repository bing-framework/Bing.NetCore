using Bing.Exceptions;
using Bing.Exceptions.Prompts;

// ReSharper disable once CheckNamespace
namespace Bing;

/// <summary>
/// 异常扩展
/// </summary>
public static partial class ExceptionExtensions
{
    /// <summary>
    /// 获取原始异常
    /// </summary>
    /// <param name="exception">异常</param>
    /// <returns>处理后的原始异常；输入为空时返回 <see langword="null"/>。</returns>
    public static Exception GetRawException(this Exception exception) => ExceptionPrompt.GetException(exception);

    /// <summary>
    /// 获取异常提示
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="isProduction">是否生产环境</param>
    /// <returns>根据异常和运行环境生成的提示文本。</returns>
    public static string GetPrompt(this Exception exception, bool isProduction = false) => ExceptionPrompt.GetPrompt(exception, isProduction);

    /// <summary>
    /// 获取Http状态码
    /// </summary>
    /// <param name="exception">异常</param>
    /// <returns>异常对应的 HTTP 状态码；无法解析时返回 200。</returns>
    public static int GetHttpStatusCode(this Exception exception)
    {
        if (exception is null)
            return 200;
        exception = exception.GetRawException();
        if (exception is Warning warning)
            return warning.HttpStatusCode;
        return 200;
    }

    /// <summary>
    /// 获取错误码
    /// </summary>
    /// <param name="exception">异常</param>
    /// <returns>异常对应的错误码；无法解析时返回 <see langword="null"/>。</returns>
    public static string GetErrorCode(this Exception exception)
    {
        if (exception is null)
            return null;
        exception = exception.GetRawException();
        if (exception is Warning warning)
            return warning.Code;
        return null;
    }
}
