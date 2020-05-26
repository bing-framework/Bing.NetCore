using Microsoft.Extensions.Logging;

namespace Bing.Logging
{
    /// <summary>
    /// 安全记录异常
    /// </summary>
    public interface IExceptionWithSelfLogging
    {
        /// <summary>
        /// 记录
        /// </summary>
        /// <param name="logger">日志</param>
        void Log(ILogger logger);
    }
}
