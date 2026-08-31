namespace Bing.Tracing;

/// <summary>
/// 表示一次请求或调用链的跟踪标识上下文。
/// </summary>
public class TraceIdContext : IHasTraceId
{
    /// <summary>
    /// 获取或设置贯穿整个调用链的跟踪标识。
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// 获取或设置调用链根节点的标识。
    /// </summary>
    public string RootId { get; set; }

    /// <summary>
    /// 获取或设置当前节点的父节点标识。
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// 获取或设置当前子节点的标识。
    /// </summary>
    public string ChildId { get; set; }

    /// <summary>
    /// 使用指定跟踪标识初始化一个 <see cref="TraceIdContext"/> 实例；未提供时自动生成 GUID。
    /// </summary>
    /// <param name="traceId">跟踪标识；为空时自动生成一个 <see cref="Guid"/>。</param>
    public TraceIdContext(string traceId)
    {
        if (string.IsNullOrEmpty(traceId))
            traceId = Guid.NewGuid().ToString();
        TraceId = traceId;
    }

    /// <summary>
    /// 使用完整的调用链层级标识初始化一个 <see cref="TraceIdContext"/> 实例。
    /// </summary>
    /// <param name="traceId">贯穿整个调用链的跟踪标识。</param>
    /// <param name="rootId">调用链根节点标识。</param>
    /// <param name="parentId">当前节点的父节点标识。</param>
    /// <param name="childId">当前子节点标识。</param>
    public TraceIdContext(string traceId, string rootId, string parentId, string childId)
    {
        TraceId = traceId;
        RootId = rootId;
        ParentId = parentId;
        ChildId = childId;
    }

    /// <summary>
    /// 保存当前异步执行流中的跟踪标识上下文。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly AsyncLocal<TraceIdContext> _currentTraceIdContext = new();

    /// <summary>
    /// 获取或设置当前异步执行流中的跟踪标识上下文。
    /// </summary>
    public static TraceIdContext Current
    {
        get => _currentTraceIdContext.Value;
        set => _currentTraceIdContext.Value = value;
    }
}
