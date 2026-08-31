using Bing.Logging;
using Microsoft.Extensions.Logging;

namespace System;

/// <summary>
/// 异常(<see cref="Exception"/>) 扩展
/// </summary>
public static class BingExceptionExtensions
{
    /// <summary>
    /// 获取日志级别
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="defaultLevel">默认日志级别</param>
    /// <returns>异常指定的日志级别；未指定时返回默认日志级别。</returns>
    public static LogLevel GetLogLevel(this Exception exception, LogLevel defaultLevel = LogLevel.Error) => (exception as IHasLogLevel)?.LogLevel ?? defaultLevel;
}
