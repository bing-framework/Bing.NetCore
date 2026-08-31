namespace Bing.Logging.ExtraSupports;

/// <summary>
/// 表示日志事件上下文中的一项结构化数据。
/// </summary>
public class ContextDataItem
{
    /// <summary>
    /// 使用名称、类型、值和输出标志初始化一个 <see cref="ContextDataItem"/> 实例。
    /// </summary>
    /// <param name="name">上下文数据项名称。</param>
    /// <param name="type">上下文数据项的运行时类型。</param>
    /// <param name="value">上下文数据项的值。</param>
    /// <param name="output">是否将该数据项输出到日志。</param>
    public ContextDataItem(string name, Type type, object value, bool output = true)
    {
        ItemType = type;
        Value = value;
        Name = name;
        Output = output;
    }

    /// <summary>
    /// 获取上下文数据项名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取上下文数据项的运行时类型。
    /// </summary>
    public Type ItemType { get; }

    /// <summary>
    /// 获取上下文数据项的值。
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// 获取是否将该数据项输出到日志。
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
