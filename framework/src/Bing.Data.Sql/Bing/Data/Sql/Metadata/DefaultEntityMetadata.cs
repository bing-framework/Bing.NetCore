namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体元数据
/// </summary>
public class DefaultEntityMetadata : IEntityMetadata, IEntityModelMetadataProvider
{
    /// <summary>
    /// 获取表名
    /// </summary>
    /// <param name="type">实体类型</param>
    public string GetTable(Type type) => type?.Name;

    /// <summary>
    /// 获取架构
    /// </summary>
    /// <param name="type">实体类型</param>
    public string GetSchema(Type type) => string.Empty;

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <param name="type">实体类型</param>
    /// <param name="property">属性名</param>
    public string GetColumn(Type type, string property) => property;

    /// <inheritdoc />
    public string GetTableName(Type entityType) => GetTable(entityType);

    /// <inheritdoc />
    public string GetPhysicalSchema(Type entityType) => GetSchema(entityType);

    /// <inheritdoc />
    public string GetLogicalSchema(Type entityType) => null;

    /// <inheritdoc />
    public string GetColumnName(Type entityType, string propertyName) => GetColumn(entityType, propertyName);
}
