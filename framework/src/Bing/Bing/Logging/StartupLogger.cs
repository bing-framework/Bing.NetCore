using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.Logging;

/// <summary>
/// 系统启动日志
/// </summary>
public class StartupLogger
{
    /// <summary>
    /// 日志信息列表
    /// </summary>
    public IList<LogInfo> LogInfos { get; } = new List<LogInfo>();

    /// <summary>
    /// 记录信息日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="logName">日志名称</param>
    public void LogInformation(string message, string logName) => Log(LogLevel.Information, message, logName);

    /// <summary>
    /// 记录调试日志
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="logName">日志名称</param>
    public void LogDebug(string message, string logName) => Log(LogLevel.Debug, message, logName);

    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <param name="message">消息</param>
    /// <param name="logName">日志名称</param>
    /// <param name="exception">异常</param>
    public void Log(LogLevel logLevel, string message, string logName, Exception exception = null)
    {
        var log = new LogInfo { LogLevel = logLevel, Message = message, LogName = logName, Exception = exception, CreatedTime = DateTime.Now };
        LogInfos.Add(log);
    }

    /// <summary>
    /// 输出
    /// </summary>
    /// <param name="provider">服务提供程序</param>
    public void Output(IServiceProvider provider)
    {
        IDictionary<string, ILogger> dict = new Dictionary<string, ILogger>();
        foreach (var info in LogInfos.OrderBy(m => m.CreatedTime))
        {
            if (!dict.TryGetValue(info.LogName, out var logger))
            {
                logger = provider.GetLogger(info.LogName);
                dict[info.LogName] = logger;
            }
            switch (info.LogLevel)
            {
                case LogLevel.Trace:
                    logger.LogTrace(info.Message);
                    break;
                case LogLevel.Debug:
                    logger.LogDebug(info.Message);
                    break;
                case LogLevel.Information:
                    logger.LogInformation(info.Message);
                    break;
                case LogLevel.Warning:
                    logger.LogWarning(info.Message);
                    break;
                case LogLevel.Error:
                    logger.LogError(info.Exception, info.Message);
                    break;
                case LogLevel.Critical:
                    logger.LogCritical(info.Exception, info.Message);
                    break;
            }
        }
    }

    /// <summary>
    /// 日志信息
    /// </summary>
    public class LogInfo
    {
        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 异常
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 日志名
        /// </summary>
        public string LogName { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; }
    }
}
