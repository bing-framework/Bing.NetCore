namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认实体模型元数据提供器。
/// </summary>
public sealed class DefaultEntityModelMetadataProvider : IEntityModelMetadataProvider
{
    /// <inheritdoc />
    public string GetTableName(Type entityType) => entityType?.Name;

    /// <inheritdoc />
    public string GetSchema(Type entityType) => string.Empty;

    /// <inheritdoc />
    public string GetColumnName(Type entityType, string propertyName) => propertyName;
}