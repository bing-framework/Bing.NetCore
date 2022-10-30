namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 变更值
/// </summary>
public class ChangedValueDescriptor
{
    /// <summary>
    /// 初始化一个<see cref="ChangedValueDescriptor"/>类型的实例
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
    /// 属性名
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 旧值
    /// </summary>
    public string OldValue { get; }

    /// <summary>
    /// 新值
    /// </summary>
    public string NewValue { get; }

    /// <summary>
    /// 输出变更信息
    /// </summary>
    public override string ToString() => $"{PropertyName}({Description}),旧值:{OldValue},新值:{NewValue}";
}