using Newtonsoft.Json;

namespace Bing.Logging.Serilog.Enrichers;

/// <summary>
/// 键值对扩展属性
/// </summary>
internal class KeyValueEnricher : ILogEventEnricher
{
    /// <summary>
    /// 键值对
    /// </summary>
    private KeyValuePair<string, object> _keyValue;

    /// <summary>
    /// 初始化一个<see cref="KeyValueEnricher"/>类型的实例
    /// </summary>
    /// <param name="keyValue">键值对</param>
    public KeyValueEnricher(KeyValuePair<string, object> keyValue) => _keyValue = keyValue;

    /// <summary>
    /// 扩展属性
    /// </summary>
    /// <param name="logEvent">日志事件</param>
    /// <param name="propertyFactory">日志事件属性工厂</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(_keyValue.Key, JsonConvert.SerializeObject(_keyValue.Value)));
    }
}
