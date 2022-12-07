using Serilog.Debugging;

namespace Bing.Logging.Serilog.Enrichers;

/// <summary>
/// 函数扩展属性
/// </summary>
internal class FunctionEnricher : ILogEventEnricher
{
    /// <summary>
    /// 键名
    /// </summary>
    private readonly string _key;

    /// <summary>
    /// 操作函数1
    /// </summary>
    private readonly Func<object, string> _func1;

    /// <summary>
    /// 操作函数0
    /// </summary>
    private readonly Func<string> _func0;

    /// <summary>
    /// 操作函数2
    /// </summary>
    private readonly Func<LogEvent, string> _func2;

    /// <summary>
    /// 参数
    /// </summary>
    private readonly object _parameter;

    /// <summary>
    /// 初始化一个<see cref="FunctionEnricher"/>类型的实例
    /// </summary>
    /// <param name="key">键名</param>
    /// <param name="func">操作函数</param>
    public FunctionEnricher(string key, Func<string> func)
    {
        _key = key;
        _func0 = func;
    }

    /// <summary>
    /// 初始化一个<see cref="FunctionEnricher"/>类型的实例
    /// </summary>
    /// <param name="key">键名</param>
    /// <param name="func">操作函数</param>
    /// <param name="parameter">参数</param>
    public FunctionEnricher(string key, Func<object, string> func, object parameter)
    {
        _key = key;
        _func1 = func;
        _parameter = parameter;
    }

    /// <summary>
    /// 初始化一个<see cref="FunctionEnricher"/>类型的实例
    /// </summary>
    /// <param name="key">键名</param>
    /// <param name="func">操作函数</param>
    public FunctionEnricher(string key, Func<LogEvent, string> func)
    {
        _key = key;
        _func2 = func;
    }

    /// <summary>
    /// 扩展属性
    /// </summary>
    /// <param name="logEvent">日志事件</param>
    /// <param name="propertyFactory">日志事件属性工厂</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        try
        {
            var result = string.Empty;

            if (_func0 != null)
                result = _func0();
            else if (_func1 != null)
                result = _func1(_parameter);
            else if (_func2 != null)
                result = _func2(logEvent);

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(_key, result));
        }
        catch (Exception e)
        {
            SelfLog.WriteLine(e.Message);
        }
    }
}
