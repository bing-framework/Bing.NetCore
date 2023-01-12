﻿using System.Diagnostics;
using Bing.Logs.Abstractions;

namespace Bing.Logs.Core;

/// <summary>
/// 空日志上下文
/// </summary>
public class NullLogContext : ILogContext
{
    /// <summary>
    /// 日志标识
    /// </summary>
    public string LogId => string.Empty;

    /// <summary>
    /// 跟踪号
    /// </summary>
    public string TraceId => string.Empty;

    /// <summary>
    /// 计时器
    /// </summary>
    public Stopwatch Stopwatch => new Stopwatch();

    /// <summary>
    /// IP
    /// </summary>
    public string Ip => string.Empty;

    /// <summary>
    /// 主机
    /// </summary>
    public string Host => string.Empty;

    /// <summary>
    /// 浏览器
    /// </summary>
    public string Browser => string.Empty;

    /// <summary>
    /// 请求地址
    /// </summary>
    public string Url => string.Empty;

    /// <summary>
    /// 初始化日志标识
    /// </summary>
    public void InitLogId() { }

    /// <summary>
    /// 空日志上下文实例
    /// </summary>
    public static ILogContext Instance { get; } = new NullLogContext();

    /// <summary>
    /// 初始化一个<see cref="NullLogContext"/>类型的实例
    /// </summary>
    private NullLogContext() { }
}
