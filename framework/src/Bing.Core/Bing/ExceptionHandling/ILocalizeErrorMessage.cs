using Bing.Localization;

namespace Bing.ExceptionHandling;

/// <summary>
/// 本地化错误消息
/// </summary>
public interface ILocalizeErrorMessage
{
    /// <summary>
    /// 获取本地化的错误消息。
    /// </summary>
    /// <param name="context">本地化上下文</param>
    /// <returns>返回与当前语言环境匹配的错误消息字符串。</returns>
    string LocalizeMessage(LocalizationContext context);
}
