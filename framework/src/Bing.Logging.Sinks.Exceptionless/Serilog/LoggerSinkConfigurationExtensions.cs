using System;
using Exceptionless;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Serilog;

/// <summary>
/// 日志接收器配置(<see cref="LoggerSinkConfiguration"/>) 扩展
/// </summary>
public static partial class LoggerSinkConfigurationExtensions
{
    /// <summary>
    /// 创建一个基于Exceptionless的日志接收器
    /// </summary>
    /// <param name="loggerSinkConfiguration">日志接收器配置</param>
    /// <param name="apiKey">API密钥</param>
    /// <param name="additionalOperation">附加信息操作函数</param>
    /// <param name="includeProperties">是否包含属性列表</param>
    /// <param name="restrictedToMinimumLevel">最小日志事件级别</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static LoggerConfiguration Exceptionless(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        string apiKey, 
        Func<EventBuilder, EventBuilder> additionalOperation = null, 
        bool includeProperties = true,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
    {
        if (loggerSinkConfiguration == null)
            throw new ArgumentNullException(nameof(loggerSinkConfiguration));
        if (apiKey == null)
            throw new ArgumentNullException(nameof(apiKey));
        return loggerSinkConfiguration.Sink(new ExceptionlessSink(apiKey, null, null, additionalOperation, includeProperties), restrictedToMinimumLevel);
    }

    /// <summary>
    /// 创建一个基于Exceptionless的日志接收器
    /// </summary>
    /// <param name="loggerSinkConfiguration">日志接收器配置</param>
    /// <param name="serverUrl">Exceptionless服务器地址</param>
    /// <param name="apiKey">API密钥</param>
    /// <param name="defaultTags">默认标签数组</param>
    /// <param name="additionalOperation">附加信息操作函数</param>
    /// <param name="includeProperties">是否包含属性列表</param>
    /// <param name="restrictedToMinimumLevel">最小日志事件级别</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static LoggerConfiguration Exceptionless(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        string apiKey,
        string serverUrl = null,
        string[] defaultTags = null,
        Func<EventBuilder, EventBuilder> additionalOperation = null,
        bool includeProperties = true,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
    {
        if (loggerSinkConfiguration == null)
            throw new ArgumentNullException(nameof(loggerSinkConfiguration));
        if (apiKey == null)
            throw new ArgumentNullException(nameof(apiKey));
        return loggerSinkConfiguration.Sink(new ExceptionlessSink(apiKey, serverUrl, defaultTags, additionalOperation, includeProperties), restrictedToMinimumLevel);
    }

    /// <summary>
    /// 创建一个基于Exceptionless的日志接收器
    /// </summary>
    /// <param name="loggerSinkConfiguration">日志接收器配置</param>
    /// <param name="additionalOperation">附加信息操作函数</param>
    /// <param name="includeProperties">是否包含属性列表</param>
    /// <param name="restrictedToMinimumLevel">最小日志事件级别</param>
    /// <param name="client">Exceptionless客户端</param>
    public static LoggerConfiguration Exceptionless(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        Func<EventBuilder, EventBuilder> additionalOperation = null,
        bool includeProperties = true,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        ExceptionlessClient client = null)
    {
        if (loggerSinkConfiguration == null)
            throw new ArgumentNullException(nameof(loggerSinkConfiguration));
        return loggerSinkConfiguration.Sink(new ExceptionlessSink(additionalOperation, includeProperties, client));
    }
}
