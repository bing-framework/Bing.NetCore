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
    string GetPrompt(Exception exception);

    /// <summary>
    /// 获取原始异常
    /// </summary>
    /// <param name="exception">异常</param>
    Exception GetRawException(Exception exception);
}
