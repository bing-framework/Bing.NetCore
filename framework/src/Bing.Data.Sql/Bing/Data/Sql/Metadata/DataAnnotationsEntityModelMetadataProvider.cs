using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 按 DataAnnotations 提供实体模型元数据。
/// </summary>
public sealed class DataAnnotationsEntityModelMetadataProvider : ConventionEntityModelMetadataProvider
{
    /// <inheritdoc />
    public override EntityModelMetadata GetMetadata(Type entityType)
    {
        if (entityType == null || HasDataAnnotations(entityType) == false)
            return null;
        var table = entityType.GetCustomAttribute<TableAttribute>();
        return new EntityModelMetadata(entityType, table?.Name ?? entityType.Name, table?.Schema ?? string.Empty,
            GetProperties(entityType).Select(property => CreatePropertyMetadata(entityType, property)));
    }

    /// <inheritdoc />
    protected override EntityPropertyMetadata CreatePropertyMetadata(Type entityType, PropertyInfo property)
    {
        var column = property.GetCustomAttribute<ColumnAttribute>();
        var timestamp = property.GetCustomAttribute<TimestampAttribute>() != null;
        var generated = timestamp
            ? DatabaseGeneratedOption.Computed
            : property.GetCustomAttribute<DatabaseGeneratedAttribute>()?.DatabaseGeneratedOption ??
              DatabaseGeneratedOption.None;
        return new EntityPropertyMetadata(property, column?.Name, property.GetCustomAttribute<NotMappedAttribute>() != null,
            property.GetCustomAttribute<KeyAttribute>() != null || IsKeyProperty(entityType, property), generated,
            timestamp || property.GetCustomAttribute<ConcurrencyCheckAttribute>() != null,
            property.GetCustomAttribute<RequiredAttribute>() != null, GetMaxLength(property), column?.TypeName);
    }

    /// <summary>
    /// 判断类型或其属性是否声明 DataAnnotations 映射元数据。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>是否声明映射元数据。</returns>
    private static bool HasDataAnnotations(Type entityType)
    {
        if (entityType.GetCustomAttribute<TableAttribute>() != null)
            return true;
        return entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public).Any(property =>
            property.GetCustomAttribute<ColumnAttribute>() != null ||
            property.GetCustomAttribute<KeyAttribute>() != null ||
            property.GetCustomAttribute<NotMappedAttribute>() != null ||
            property.GetCustomAttribute<DatabaseGeneratedAttribute>() != null ||
            property.GetCustomAttribute<TimestampAttribute>() != null ||
            property.GetCustomAttribute<ConcurrencyCheckAttribute>() != null ||
            property.GetCustomAttribute<RequiredAttribute>() != null ||
            property.GetCustomAttribute<MaxLengthAttribute>() != null ||
            property.GetCustomAttribute<StringLengthAttribute>() != null);
    }

    /// <summary>
    /// 获取属性最大长度。
    /// </summary>
    /// <param name="property">CLR 属性。</param>
    /// <returns>最大长度；未声明时返回 <see langword="null"/>。</returns>
    private static int? GetMaxLength(PropertyInfo property)
    {
        var maxLength = property.GetCustomAttribute<MaxLengthAttribute>()?.Length;
        if (maxLength > 0)
            return maxLength;
        var stringLength = property.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;
        return stringLength > 0 ? stringLength : null;
    }
}