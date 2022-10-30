using Bing.Logging.Serilog.Enrichers;
using Serilog.Configuration;

namespace Bing.Logging.Serilog;

/// <summary>
/// Serilog扩展属性配置(<see cref="LoggerEnrichmentConfiguration"/>) 扩展
/// </summary>
public static class LoggerEnrichmentConfigurationExtensions
{
    /// <summary>
    /// 添加日志上下文扩展属性
    /// </summary>
    /// <param name="source">日志扩展配置</param>
    public static LoggerConfiguration WithLogContext(this LoggerEnrichmentConfiguration source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.With<LogContextEnricher>();
    }

    /// <summary>
    /// 添加日志级别扩展属性
    /// </summary>
    /// <param name="source">日志扩展配置</param>
    public static LoggerConfiguration WithLogLevel(this LoggerEnrichmentConfiguration source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return source.With<LogLevelEnricher>();
    }

    /// <summary>
    /// 添加键值对扩展属性
    /// </summary>
    /// <param name="source">日志扩展配置</param>
    /// <param name="keyValue">键值对</param>
    public static LoggerConfiguration WithProperty(this LoggerEnrichmentConfiguration source, KeyValuePair<string, object> keyValue)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (keyValue.Equals(default(KeyValuePair<string, object>)))
            throw new ArgumentNullException(nameof(keyValue));
        return source.With(new KeyValueEnricher(keyValue));
    }

    /// <summary>
    /// 添加函数扩展属性
    /// </summary>
    /// <param name="source">日志扩展配置</param>
    /// <param name="key">键名</param>
    /// <param name="func">操作函数</param>
    public static LoggerConfiguration WithFunction(this LoggerEnrichmentConfiguration source, string key, Func<LogEvent, string> func)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));
        if (func == null)
            throw new ArgumentNullException(nameof(func));
        return source.With(new FunctionEnricher(key, func));
    }

    /// <summary>
    /// 添加函数扩展属性
    /// </summary>
    /// <param name="source">日志扩展配置</param>
    /// <param name="key">键名</param>
    /// <param name="func">操作函数</param>
    public static LoggerConfiguration WithFunction(this LoggerEnrichmentConfiguration source, string key, Func<string> func)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));
        if (func == null)
            throw new ArgumentNullException(nameof(func));
        return source.With(new FunctionEnricher(key, func));
    }

    /// <summary>
    /// 添加函数扩展属性
    /// </summary>
    /// <param name="source">日志扩展配置</param>
    /// <param name="key">键名</param>
    /// <param name="func">操作函数</param>
    /// <param name="parameter">参数</param>
    public static LoggerConfiguration WithFunction(this LoggerEnrichmentConfiguration source, string key, Func<object, string> func, object parameter)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));
        if (func == null)
            throw new ArgumentNullException(nameof(func));
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));
        return source.With(new FunctionEnricher(key, func, parameter));
    }

    /// <summary>
    /// 添加环境扩展属性
    /// </summary>
    /// <param name="source">日志扩展配置</param>
    /// <param name="environmentVariable">环境变量</param>
    public static LoggerConfiguration WithEnvironment(this LoggerEnrichmentConfiguration source, string environmentVariable)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(environmentVariable))
            throw new ArgumentNullException(nameof(environmentVariable));
        return source.With(new EnvironmentEnricher(environmentVariable));
    }
}
