using System.Reflection;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 按 CLR 命名约定提供实体模型元数据。
/// </summary>
public class ConventionEntityModelMetadataProvider : IEntityModelMetadataProvider
{
    /// <inheritdoc />
    public virtual EntityModelMetadata GetMetadata(Type entityType)
    {
        if (entityType == null)
            return null;
        return CreateMetadata(entityType);
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));

    /// <summary>
    /// 创建实体模型元数据。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>实体模型元数据。</returns>
    protected virtual EntityModelMetadata CreateMetadata(Type entityType) => new(entityType, entityType.Name, string.Empty,
        GetProperties(entityType).Select(property => CreatePropertyMetadata(entityType, property)));

    /// <summary>
    /// 获取可映射的 CLR 属性。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>CLR 属性集合。</returns>
    protected virtual IEnumerable<PropertyInfo> GetProperties(Type entityType) => entityType
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.CanRead && property.GetIndexParameters().Length == 0);

    /// <summary>
    /// 创建属性元数据。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="property">CLR 属性。</param>
    /// <returns>属性元数据。</returns>
    protected virtual EntityPropertyMetadata CreatePropertyMetadata(Type entityType, PropertyInfo property) => new(property,
        isKey: IsKeyProperty(entityType, property));

    /// <summary>
    /// 判断属性是否符合主键命名约定。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="property">CLR 属性。</param>
    /// <returns>是否为主键。</returns>
    protected virtual bool IsKeyProperty(Type entityType, PropertyInfo property) =>
        string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(property.Name, $"{entityType.Name}Id", StringComparison.OrdinalIgnoreCase);
}