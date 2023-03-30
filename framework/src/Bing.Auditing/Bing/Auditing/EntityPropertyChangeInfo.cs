namespace Bing.Auditing;

/// <summary>
/// 实体属性变更信息
/// </summary>
/// <remarks>表示一个实体的属性的变更。</remarks>
[Serializable]
public class EntityPropertyChangeInfo
{
    /// <summary>
    /// 新值
    /// </summary>
    /// <remarks>属性的新值，如果实体已被删除则为<c>null</c>。</remarks>
    public virtual string NewValue { get; set; }

    /// <summary>
    /// 原始值
    /// </summary>
    /// <remarks>变更前旧/初始值，如果实体是新创建则为<c>null</c>。</remarks>
    public virtual string OriginalValue { get; set; }

    /// <summary>
    /// 属性名称
    /// </summary>
    /// <remarks>实体类的属性名称。</remarks>
    public virtual string PropertyName { get; set; }

    /// <summary>
    /// 属性类型全名
    /// </summary>
    /// <remarks>属性类型的完整命名空间名称。</remarks>
    public virtual string PropertyTypeFullName { get; set; }
}
