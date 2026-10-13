namespace Bing.Data.Attributes;

/// <summary>
/// 标记实体属性是否启用最大长度约束。
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class HasMaxLengthAttribute : Attribute
{
    /// <summary>
    /// 获取或设置最大长度标记的启用状态。
    /// </summary>
    /// <remarks>默认值为 <see langword="true"/>。</remarks>
    public bool HasMaxLength { get; set; } = true;
}
