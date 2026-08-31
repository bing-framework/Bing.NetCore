namespace Bing.Auditing;

/// <summary>
/// 表示单个实体属性的原始值和新值。
/// </summary>
[Serializable]
public class EntityPropertyChangeInfo
{
    /// <summary>
    /// 获取或设置属性变更后的值；实体删除时为 <c>null</c>。
    /// </summary>
    /// <remarks>属性的新值，如果实体已被删除则为<c>null</c>。</remarks>
    public virtual string NewValue { get; set; }

    /// <summary>
    /// 获取或设置属性变更前的值；实体新建时为 <c>null</c>。
    /// </summary>
    /// <remarks>变更前旧/初始值，如果实体是新创建则为<c>null</c>。</remarks>
    public virtual string OriginalValue { get; set; }

    /// <summary>
    /// 获取或设置发生变更的实体属性名称。
    /// </summary>
    /// <remarks>实体类的属性名称。</remarks>
    public virtual string PropertyName { get; set; }

    /// <summary>
    /// 获取或设置发生变更的实体属性类型全名。
    /// </summary>
    /// <remarks>属性类型的完整命名空间名称。</remarks>
    public virtual string PropertyTypeFullName { get; set; }
}
