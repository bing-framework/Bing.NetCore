using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bing.AspNetCore.Logs;

/// <summary>
/// 系统日志提供程序
/// </summary>
internal class SysLoggerProvider : ILoggerProvider
{
    /// <summary>
    /// 日志字典
    /// </summary>
    private readonly ConcurrentDictionary<string, SysLogger> _loggers = new ConcurrentDictionary<string, SysLogger>();

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose() => _loggers.Clear();

    /// <summary>
    /// 初始化系统日志提供器
    /// </summary>
    /// <param name="categoryName">日志分类</param>
    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, CreateLoggerImplementation);

    /// <summary>
    /// 创建日志实现类
    /// </summary>
    /// <param name="name">名称</param>
    private SysLogger CreateLoggerImplementation(string name)
    {
        Debug.WriteLine($"创建【{name}】系统日志记录器");
        return new SysLogger(name);
    }
}