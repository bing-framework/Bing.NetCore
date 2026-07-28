using System.ComponentModel.DataAnnotations.Schema;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认实体模型元数据提供器。
/// </summary>
public sealed class DefaultEntityModelMetadataProvider : IEntityModelMetadataProvider
{
    /// <inheritdoc />
    public string GetTableName(Type entityType) => entityType?.GetCustomAttributes(typeof(TableAttribute), false)
        .OfType<TableAttribute>()
        .FirstOrDefault()?
        .Name ?? entityType?.Name;

    /// <inheritdoc />
    public string GetSchema(Type entityType) => entityType?.GetCustomAttributes(typeof(TableAttribute), false)
        .OfType<TableAttribute>()
        .FirstOrDefault()?
        .Schema ?? string.Empty;

    /// <inheritdoc />
    public string GetColumnName(Type entityType, string propertyName) => propertyName;
}