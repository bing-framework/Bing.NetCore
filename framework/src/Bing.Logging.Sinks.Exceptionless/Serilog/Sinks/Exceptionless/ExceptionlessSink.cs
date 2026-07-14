using Bing.Logging.Sinks.Exceptionless.Internals;
using Exceptionless;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.Exceptionless;

/// <summary>
/// Exceptionless 接收器
/// </summary>
public class ExceptionlessSink : ILogEventSink, IDisposable
{
    /// <summary>
    /// 默认标签数组
    /// </summary>
    private readonly string[] _defaultTags;

    /// <summary>
    /// 附加信息操作函数
    /// </summary>
    private readonly Func<EventBuilder, EventBuilder> _additionalOperation;

    /// <summary>
    /// 是否包含属性列表
    /// </summary>
    private readonly bool _includeProperties;

    /// <summary>
    /// Exceptionless 客户端
    /// </summary>
    private readonly ExceptionlessClient _client;

    /// <summary>
    /// 日志事件映射器
    /// </summary>
    private readonly ExceptionlessLogEventMapper _mapper;

    /// <summary>
    /// 初始化一个<see cref="ExceptionlessSink"/>类型的实例
    /// </summary>
    /// <param name="apiKey">API密钥</param>
    /// <param name="serverUrl">Exceptionless服务器地址</param>
    /// <param name="defaultTags">默认标签数组</param>
    /// <param name="additionalOperation">附加信息操作函数</param>
    /// <param name="includeProperties">是否包含属性列表</param>
    /// <param name="restrictedToMinimumLevel">将事件写入接收器所需的最低日志事件级别</param>
    /// <param name="mapperOptions">日志事件映射配置</param>
    public ExceptionlessSink(
        string apiKey, 
        string serverUrl = null, 
        string[] defaultTags = null, 
        Func<EventBuilder, EventBuilder> additionalOperation = null, 
        bool includeProperties = true,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        ExceptionlessLogEventMapperOptions mapperOptions = null)
    {
        if (apiKey == null)
            throw new ArgumentNullException(nameof(apiKey));
        _client = new ExceptionlessClient(config =>
        {
            if (!string.IsNullOrEmpty(apiKey) && apiKey != "API_KEY_HERE")
                config.ApiKey = apiKey;
            if (!string.IsNullOrEmpty(serverUrl))
                config.ServerUrl = serverUrl;
            config.UseInMemoryStorage();
            config.UseLogger(new SelfLogLogger());
            config.SetDefaultMinLogLevel(LogLevelSwitcher.Switch(restrictedToMinimumLevel));
        });
        _defaultTags = defaultTags;
        _additionalOperation = additionalOperation;
        _includeProperties = includeProperties;
        _mapper = new ExceptionlessLogEventMapper(mapperOptions);
    }

    /// <summary>
    /// 初始化一个<see cref="ExceptionlessSink"/>类型的实例
    /// </summary>
    /// <param name="additionalOperation">附加信息操作函数</param>
    /// <param name="includeProperties">是否包含属性列表</param>
    /// <param name="client">Exceptionless客户端</param>
    /// <param name="restrictedToMinimumLevel">将事件写入接收器所需的最低日志事件级别</param>
    /// <param name="mapperOptions">日志事件映射配置</param>
    public ExceptionlessSink(
        Func<EventBuilder, EventBuilder> additionalOperation = null,
        bool includeProperties = true,
        ExceptionlessClient client = null,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        ExceptionlessLogEventMapperOptions mapperOptions = null)
    {
        _additionalOperation = additionalOperation;
        _includeProperties = includeProperties;
        _mapper = new ExceptionlessLogEventMapper(mapperOptions);
        _client = client ?? ExceptionlessClient.Default;
        if (_client.Configuration.Resolver.HasDefaultRegistration<IExceptionlessLog, NullExceptionlessLog>())
            _client.Configuration.UseLogger(new SelfLogLogger());
        _client.Configuration.SetDefaultMinLogLevel(LogLevelSwitcher.Switch(restrictedToMinimumLevel));
    }

    /// <summary>
    /// 提交
    /// </summary>
    /// <param name="logEvent">日志事件</param>
    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null || !_client.Configuration.IsValid)
            return;
        var minLogLevel = _client.Configuration.Settings.GetMinLogLevel(logEvent.GetSource());
        if (LogLevelSwitcher.Switch(logEvent.Level) < minLogLevel)
            return;

        var builder = _mapper.Map(_client, logEvent, _includeProperties, _defaultTags);

        _additionalOperation?.Invoke(builder);
        builder.Submit();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    void IDisposable.Dispose() => _client?.ProcessQueueAsync().GetAwaiter().GetResult();
}
