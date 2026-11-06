using Microsoft.Extensions.Logging;

namespace Bing.Logging;

/// <summary>
/// 安全记录异常
/// </summary>
public interface IExceptionWithSelfLogging
{
    /// <summary>
    /// 将异常自身写入指定日志器。
    /// </summary>
    /// <param name="logger">日志</param>
    void Log(ILogger logger);
}
