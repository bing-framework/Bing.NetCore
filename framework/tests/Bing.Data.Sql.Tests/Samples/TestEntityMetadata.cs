using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 测试实体元数据
/// </summary>
public class TestEntityMetadata : IEntityModelMetadataProvider
{
    /// <summary>
    /// 获取实体模型元数据。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>实体模型元数据。</returns>
    public EntityModelMetadata GetMetadata(Type entityType)
    {
        if (entityType == null)
            return null;
        var source = new DataAnnotationsEntityModelMetadataProvider().GetMetadata(entityType) ??
            new ConventionEntityModelMetadataProvider().GetMetadata(entityType);
        return new EntityModelMetadata(entityType, $"t_{entityType.Name}", $"as_{entityType.Name}",
            source.Properties.Values.Select(property => new EntityPropertyMetadata(property.Property,
                property.PropertyName == "DecimalValue" ? property.PropertyName : $"{entityType.Name}_{property.PropertyName}",
                property.IsIgnored, property.IsKey, property.DatabaseGeneratedOption, property.IsConcurrencyToken,
                property.IsRequired, property.MaxLength, property.ProviderTypeName)));
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
}
