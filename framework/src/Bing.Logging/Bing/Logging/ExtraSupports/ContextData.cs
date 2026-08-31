namespace Bing.Logging.ExtraSupports;

/// <summary>
/// 日志事件上下文数据
/// </summary>
public class ContextData : Dictionary<string, ContextDataItem>
{
    /// <summary>
    /// 初始化一个<see cref="ContextData"/>类型的实例
    /// </summary>
    public ContextData() : base(StringComparer.OrdinalIgnoreCase) { }

    /// <summary>
    /// 初始化一个<see cref="ContextData"/>类型的实例
    /// </summary>
    /// <param name="ctx">字典</param>
    public ContextData(IDictionary<string, ContextDataItem> ctx) : base(ctx, StringComparer.OrdinalIgnoreCase) { }

    /// <summary>
    /// 添加项
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    /// <param name="output">是否输出</param>
    /// <exception cref="ArgumentNullException">名称为空时抛出。</exception>
    public void AddItem(string name, object value, bool output = true)
    {
        if (value is null)
            return;
        if (ContainsKey(name))
            throw new ArgumentException($"Key '{name}' has been added.", nameof(name));
        if (value is ContextDataItem item)
            Add(item.Name, item);
        else
            Add(name, new ContextDataItem(name, value.GetType(), value, output));
    }

    /// <summary>
    /// 添加或更新项
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    /// <param name="output">是否输出</param>
    public void AddOrUpdateItem(string name, object value, bool output = true)
    {
        if (value is null)
            return;
        if (string.IsNullOrWhiteSpace(name))
            return;
        if (value is ContextDataItem item)
            AddOrUpdateInternal(item.Name, item);
        else
            AddOrUpdateInternal(name, new ContextDataItem(name, value.GetType(), value, output));
    }

    /// <summary>
    /// 添加或更新
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="item">项</param>
    private void AddOrUpdateInternal(string name, ContextDataItem item)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        if (item == null)
            return;
        if (ContainsKey(name))
            this[name] = item;
        else
            Add(name, item);
    }

    /// <summary>
    /// 当前上游上下文数据的指针
    /// </summary>
    private ContextData CurrentUpstreamContextPointer { get; set; }

    /// <summary>
    /// 导入上游的上下文数据
    /// </summary>
    /// <param name="contextData">日志事件上下文数据</param>
    internal void ImportUpstreamContextData(ContextData contextData)
    {
        if (contextData == null)
            return;
        CurrentUpstreamContextPointer = contextData;
        foreach (var data in contextData)
        {
            if (ContainsKey(data.Key))
                continue;
            Add(data.Key, data.Value);
        }
    }

    /// <summary>
    /// 导出上游的上下文数据
    /// </summary>
    /// <returns>当前上下文记录的上游上下文数据；未导入上游数据时返回 <see langword="null"/>。</returns>
    internal ContextData ExportUpstreamContextData() => CurrentUpstreamContextPointer;

    /// <summary>
    /// 输出字符串
    /// </summary>
    /// <returns>包含可输出上下文项的 JSON 风格文本。</returns>
    public override string ToString()
    {
        if (Count == 0)
            return string.Empty;
        var sb = new StringBuilder();
        var fen = "";
        sb.Append("[");
        foreach (var item in this)
        {
            if (!item.Value.Output)
                continue;
            sb.Append(fen);
            fen = ",";
            sb.Append($"{{\"{item.Key}\":\"{item.Value}\"}}");
        }
        sb.Append("]");
        return sb.ToString();
    }

    /// <summary>
    /// 克隆
    /// </summary>
    /// <returns>当前上下文数据的浅复制实例。</returns>
    public ContextData Copy() => new(this);
}
