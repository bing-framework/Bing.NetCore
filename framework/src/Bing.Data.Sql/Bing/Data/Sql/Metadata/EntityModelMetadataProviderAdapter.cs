namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 旧实体元数据兼容适配器。
/// </summary>
/// <remarks>
/// 将旧 <see cref="IEntityMetadata"/> 的原始映射结果投影为 <see cref="IEntityModelMetadataProvider"/>，避免新旧路径各自维护回退规则。
/// </remarks>
public sealed class EntityModelMetadataProviderAdapter : IEntityModelMetadataProvider
{
    /// <summary>
    /// 旧实体元数据服务。
    /// </summary>
    private readonly IEntityMetadata _metadata;

    /// <summary>
    /// 初始化一个<see cref="EntityModelMetadataProviderAdapter"/>类型的实例。
    /// </summary>
    /// <param name="metadata">旧实体元数据服务。</param>
    public EntityModelMetadataProviderAdapter(IEntityMetadata metadata) => _metadata = metadata ?? new DefaultEntityMetadata();

    /// <inheritdoc />
    public string GetTableName(Type entityType) => _metadata.GetTable(entityType);

    /// <inheritdoc />
    public string GetPhysicalSchema(Type entityType) => _metadata.GetSchema(entityType);

    /// <inheritdoc />
    public string GetLogicalSchema(Type entityType) => null;

    /// <inheritdoc />
    public string GetColumnName(Type entityType, string propertyName) => _metadata.GetColumn(entityType, propertyName);
}