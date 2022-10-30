namespace Bing.Logging.ExtraSupports;

/// <summary>
/// 日志事件上下文数据项
/// </summary>
public class ContextDataItem
{
    /// <summary>
    /// 初始化一个<see cref="ContextDataItem"/>类型的实例
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="type">类型</param>
    /// <param name="value">值</param>
    /// <param name="output">是否输出</param>
    public ContextDataItem(string name, Type type, object value, bool output = true)
    {
        ItemType = type;
        Value = value;
        Name = name;
        Output = output;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 类型
    /// </summary>
    public Type ItemType { get; }

    /// <summary>
    /// 值
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// 是否输出
    /// </summary>
    public bool Output { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append($"\"Name\":\"{Name}\",");
        sb.Append($"\"Type\":\"{ItemType}\",");
        sb.Append($"\"Value\":\"{Value}\"");
        sb.Append("}");
        return sb.ToString();
    }
}
