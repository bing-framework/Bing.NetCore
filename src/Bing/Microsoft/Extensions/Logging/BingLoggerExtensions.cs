using System;
using System.Collections.Generic;
using Bing.ExceptionHandling;
using Bing.Logging;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// 日志(<see cref="ILogger"/>) 扩展
    /// </summary>
    public static partial class BingLoggerExtensions
    {
        #region LogWithLevel(记录日志级别)

        /// <summary>
        /// 记录日志级别
        /// </summary>
        /// <param name="logger">日志</param>
        /// <param name="logLevel">日志级别</param>
        /// <param name="message">日志消息</param>
        public static void LogWithLevel(this ILogger logger, LogLevel logLevel, string message)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    logger.LogCritical(message);
                    break;
                case LogLevel.Error:
                    logger.LogError(message);
                    break;
                case LogLevel.Warning:
                    logger.LogWarning(message);
                    break;
                case LogLevel.Information:
                    logger.LogInformation(message);
                    break;
                case LogLevel.Trace:
                    logger.LogTrace(message);
                    break;
                default: // LogLevel.Debug || LogLevel.None
                    logger.LogDebug(message);
                    break;
            }
        }

        /// <summary>
        /// 记录日志级别
        /// </summary>
        /// <param name="logger">日志</param>
        /// <param name="logLevel">日志级别</param>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常</param>
        public static void LogWithLevel(this ILogger logger, LogLevel logLevel, string message, Exception exception)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    logger.LogCritical(exception, message);
                    break;
                case LogLevel.Error:
                    logger.LogError(exception, message);
                    break;
                case LogLevel.Warning:
                    logger.LogWarning(exception, message);
                    break;
                case LogLevel.Information:
                    logger.LogInformation(exception, message);
                    break;
                case LogLevel.Trace:
                    logger.LogTrace(exception, message);
                    break;
                default: // LogLevel.Debug || LogLevel.None
                    logger.LogDebug(exception, message);
                    break;
            }
        }

        #endregion

        #region LogException(记录异常)

        /// <summary>
        /// 记录异常
        /// </summary>
        /// <param name="logger">日志</param>
        /// <param name="ex">异常</param>
        /// <param name="level">日志级别</param>
        public static void LogException(this ILogger logger, Exception ex, LogLevel? level = null)
        {
            var selectedLevel = level ?? ex.GetLogLevel();
            logger.LogWithLevel(selectedLevel, ex.Message, ex);
            LogKnownProperties(logger, ex, selectedLevel);
            LogSelfLogging(logger, ex);
            LogData(logger, ex, selectedLevel);
        }

        /// <summary>
        /// 记录已知的属性
        /// </summary>
        /// <param name="logger">日志</param>
        /// <param name="exception">异常</param>
        /// <param name="logLevel">日志级别</param>
        private static void LogKnownProperties(ILogger logger, Exception exception, LogLevel logLevel)
        {
            if (exception is IHasErrorCode exceptionWithErrorCode)
                logger.LogWithLevel(logLevel, $"Code:{exceptionWithErrorCode.Code}");
            if (exception is IHasErrorDetails exceptionWithErrorDetails)
                logger.LogWithLevel(logLevel, $"Details:{exceptionWithErrorDetails.Details}");
        }

        /// <summary>
        /// 记录数据
        /// </summary>
        /// <param name="logger">日志</param>
        /// <param name="exception">异常</param>
        /// <param name="logLevel">日志级别</param>
        private static void LogData(ILogger logger, Exception exception, LogLevel logLevel)
        {
            if (exception.Data == null || exception.Data.Count <= 0)
                return;
            logger.LogWithLevel(logLevel, "---------- Exception Data ----------");
            foreach (var key in exception.Data.Keys) logger.LogWithLevel(logLevel, $"{key} = {exception.Data[key]}");
        }

        /// <summary>
        /// 记录安全日志
        /// </summary>
        /// <param name="logger">日志</param>
        /// <param name="exception">异常</param>
        private static void LogSelfLogging(ILogger logger, Exception exception)
        {
            var loggingExceptions = new List<IExceptionWithSelfLogging>();
            if (exception is IExceptionWithSelfLogging)
                loggingExceptions.Add(exception as IExceptionWithSelfLogging);
            else if (exception is AggregateException && exception.InnerException != null)
            {
                var aggregateException = exception as AggregateException;
                if (aggregateException.InnerException is IExceptionWithSelfLogging)
                    loggingExceptions.Add(aggregateException.InnerExceptions as IExceptionWithSelfLogging);
                foreach (var innerException in aggregateException.InnerExceptions)
                {
                    if (innerException is IExceptionWithSelfLogging)
                        loggingExceptions.AddIfNotContains(innerException as IExceptionWithSelfLogging);
                }
            }

            foreach (var ex in loggingExceptions)
                ex.Log(logger);
        }

        #endregion
    }
}
