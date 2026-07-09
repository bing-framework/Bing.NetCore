using System.Reflection;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体描述信息
/// </summary>
public sealed class EntityDescriptor
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 实体属性集合
    /// </summary>
    public IReadOnlyList<PropertyInfo> Properties { get; set; }

    /// <summary>
    /// 主键属性集合
    /// </summary>
    public IReadOnlyList<PropertyInfo> KeyProperties { get; set; }
}
