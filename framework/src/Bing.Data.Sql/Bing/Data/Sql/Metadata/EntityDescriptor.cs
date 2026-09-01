using System.Reflection;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 描述实体类型及其可映射属性。
/// </summary>
public sealed class EntityDescriptor
{
    /// <summary>
    /// 获取或设置实体类型。
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 获取或设置实体属性集合。
    /// </summary>
    public IReadOnlyList<PropertyInfo> Properties { get; set; }

    /// <summary>
    /// 获取或设置实体主键属性集合。
    /// </summary>
    public IReadOnlyList<PropertyInfo> KeyProperties { get; set; }
}
