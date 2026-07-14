namespace Bing.Logging.Serilog.Enrichers;

/// <summary>
/// 日志上下文扩展属性
/// </summary>
internal class LogContextEnricher : ILogEventEnricher
{
    /// <summary>
    /// 日志上下文快照访问函数
    /// </summary>
    private readonly Func<LogContextSnapshot> _snapshotAccessor;

    /// <summary>
    /// 初始化一个<see cref="LogContextEnricher"/>类型的实例
    /// </summary>
    public LogContextEnricher(ILogContextAccessor accessor)
    {
        if (accessor == null)
            throw new ArgumentNullException(nameof(accessor));
        _snapshotAccessor = () => accessor.Current;
    }

    /// <summary>
    /// 初始化一个<see cref="LogContextEnricher"/>类型的实例
    /// </summary>
    public LogContextEnricher(Func<LogContextSnapshot> snapshotAccessor) =>
        _snapshotAccessor = snapshotAccessor ?? throw new ArgumentNullException(nameof(snapshotAccessor));

    /// <summary>
    /// 扩展属性
    /// </summary>
    /// <param name="logEvent">日志事件</param>
    /// <param name="propertyFactory">日志事件属性工厂</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var context = _snapshotAccessor();
        if (context == null)
            return;
        AddProperty(logEvent, propertyFactory, "TraceId", context.TraceId);
        AddProperty(logEvent, propertyFactory, "SessionId", context.Identity.SessionId);
        AddProperty(logEvent, propertyFactory, "UserId", context.Identity.UserId);
        AddProperty(logEvent, propertyFactory, "TenantId", context.Identity.TenantId);
        AddProperty(logEvent, propertyFactory, "Application", context.Client.Application);
        AddProperty(logEvent, propertyFactory, "Environment", context.Client.Environment);
        AddProperty(logEvent, propertyFactory, "ClientIp", context.Client.Ip);
        AddProperty(logEvent, propertyFactory, "Host", context.Client.Host);
        AddProperty(logEvent, propertyFactory, "Browser", context.Client.Browser);
        AddProperty(logEvent, propertyFactory, "Url", context.Client.Url);
        AddProperty(logEvent, propertyFactory, "BusinessTraceId", context.Business.BusinessTraceId);
        AddExtraData(logEvent, propertyFactory, context.Business.Data);
        AddTags(logEvent, propertyFactory, context.Business.Tags);
    }

    /// <summary>
    /// 添加属性
    /// </summary>
    private static void AddProperty(LogEvent logEvent, ILogEventPropertyFactory propertyFactory, string name, object value)
    {
        if (value == null || value is string text && string.IsNullOrWhiteSpace(text))
            return;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(name, value));
    }

    /// <summary>
    /// 添加扩展数据
    /// </summary>
    private static void AddExtraData(
        LogEvent logEvent,
        ILogEventPropertyFactory propertyFactory,
        IReadOnlyDictionary<string, object> data)
    {
        if (data.Count == 0)
            return;
        foreach (var item in data)
            AddProperty(logEvent, propertyFactory, item.Key, item.Value);
    }

    /// <summary>
    /// 添加标签
    /// </summary>
    private static void AddTags(
        LogEvent logEvent,
        ILogEventPropertyFactory propertyFactory,
        IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
            return;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Tags", tags, true));
    }
}
