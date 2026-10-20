using Microsoft.Extensions.Logging;

namespace Bing.Logging;

/// <summary>
/// 日志级别
/// </summary>
public interface IHasLogLevel
{
    /// <summary>
    /// 获取或设置日志级别
    /// </summary>
    LogLevel LogLevel { get; set; }
}
