using System;

namespace Bing.Exceptions.Prompts
{
    /// <summary>
    /// 异常提示
    /// </summary>
    public interface IExceptionPrompt
    {
        /// <summary>
        /// 获取异常提示
        /// </summary>
        /// <param name="exception">异常</param>
        /// <returns></returns>
        string GetPrompt(Exception exception);
    }
}
