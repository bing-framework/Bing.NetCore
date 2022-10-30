using Microsoft.Extensions.Logging;

namespace Bing.Logging;

/// <summary>
/// 日志(<see cref="ILogger"/>) 扩展
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// 基于日志级别输出日志
    /// </summary>
    /// <param name="logger">日志</param>
    /// <param name="logLevel">日志级别</param>
    /// <param name="message">消息</param>
    /// <param name="args">参数</param>
    public static void LogWithLevel(this ILogger logger, LogLevel logLevel, string message, params object[] args)
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
                logger.LogTrace(message, args);
                break;
            case LogLevel.Debug:
                logger.LogDebug(message, args);
                break;
            case LogLevel.Information:
                logger.LogInformation(message, args);
                break;
            case LogLevel.Warning:
                logger.LogWarning(message, args);
                break;
            case LogLevel.Error:
                logger.LogError(message, args);
                break;
            case LogLevel.Critical:
                logger.LogCritical(message, args);
                break;
            default:
                logger.LogDebug(message, args);
                break;
        }
    }

    /// <summary>
    /// 基于日志级别输出日志
    /// </summary>
    /// <param name="logger">日志</param>
    /// <param name="logLevel">日志级别</param>
    /// <param name="message">消息</param>
    /// <param name="exception">异常</param>
    public static void LogWithLevel(this ILogger logger, LogLevel logLevel, string message, Exception exception)
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
                logger.LogTrace(exception, message);
                break;
            case LogLevel.Debug:
                logger.LogDebug(exception, message);
                break;
            case LogLevel.Information:
                logger.LogInformation(exception, message);
                break;
            case LogLevel.Warning:
                logger.LogWarning(exception, message);
                break;
            case LogLevel.Error:
                logger.LogError(exception, message);
                break;
            case LogLevel.Critical:
                logger.LogCritical(exception, message);
                break;
            default:
                logger.LogDebug(exception, message);
                break;
        }
    }
}
