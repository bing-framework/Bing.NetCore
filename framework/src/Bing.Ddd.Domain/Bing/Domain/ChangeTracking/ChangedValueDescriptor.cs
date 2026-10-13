namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 变更值描述符
/// </summary>
public record ChangedValueDescriptor
{
    /// <summary>
    /// 初始化一个 <see cref="ChangedValueDescriptor"/> 类型的实例。
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="description">描述</param>
    /// <param name="oldValue">旧值</param>
    /// <param name="newValue">新值</param>
    public ChangedValueDescriptor(string propertyName, string description, string oldValue, string newValue)
    {
        PropertyName = propertyName;
        Description = description;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>
    /// 获取属性名
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// 获取描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 获取旧值
    /// </summary>
    public string OldValue { get; }

    /// <summary>
    /// 获取新值
    /// </summary>
    public string NewValue { get; }

    /// <inheritdoc />
    /// <remarks>
    /// 文本包含属性、描述、旧值和新值。
    /// </remarks>
    public override string ToString() => $"{PropertyName}({Description}),旧值:{OldValue},新值:{NewValue}";
}
