namespace Bing.Exceptions.Prompts;

/// <summary>
/// 异常提示
/// </summary>
public interface IExceptionPrompt
{
    /// <summary>
    /// 获取异常提示
    /// </summary>
    /// <param name="exception">异常</param>
    /// <returns>针对异常生成的提示文本。</returns>
    string GetPrompt(Exception exception);

    /// <summary>
    /// 获取原始异常
    /// </summary>
    /// <param name="exception">异常</param>
    /// <returns>处理后的原始异常实例。</returns>
    Exception GetRawException(Exception exception);
}
