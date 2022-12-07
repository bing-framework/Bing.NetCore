﻿using System;
using Microsoft.Extensions.Logging;

namespace Bing.Datas.EntityFramework.Logs;

/// <summary>
/// 空日志记录器
/// </summary>
public class NullLogger : ILogger
{
    /// <summary>
    /// 空日志记录器实例
    /// </summary>
    public static readonly ILogger Instance = new NullLogger();

    /// <summary>
    /// 日志记录
    /// </summary>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
    }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    public bool IsEnabled(LogLevel logLevel) => false;

    /// <summary>
    /// 起始范围
    /// </summary>
    public IDisposable BeginScope<TState>(TState state) => null;
}