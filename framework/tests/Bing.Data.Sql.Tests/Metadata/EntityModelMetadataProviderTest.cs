using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Metadata;
using Shouldly;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// 纯核心实体模型元数据提供器测试。
/// </summary>
public class EntityModelMetadataProviderTest
{
    /// <summary>
    /// 测试目的：约定提供器应按实体名称生成表名，并识别 Id 与实体名 Id 主键。
    /// </summary>
    [Fact]
    public void GetMetadata_WhenConventionApplied_ShouldCreateTableAndKeys()
    {
        // Arrange
        var provider = new ConventionEntityModelMetadataProvider();

        // Act
        var idMetadata = provider.GetMetadata<ConventionIdEntity>();
        var namedMetadata = provider.GetMetadata(typeof(ConventionNamedEntity));

        // Assert
        idMetadata.TableName.ShouldBe(nameof(ConventionIdEntity));
        idMetadata.Schema.ShouldBeEmpty();
        idMetadata.Properties[nameof(ConventionIdEntity.Id)].IsKey.ShouldBeTrue();
        namedMetadata.Properties[nameof(ConventionNamedEntity.ConventionNamedEntityId)].IsKey.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：DataAnnotations 提供器应将全部声明转换为不可变的完整属性模型。
    /// </summary>
    [Fact]
    public void GetMetadata_WhenDataAnnotationsDeclared_ShouldMapCompletePropertyMetadata()
    {
        // Arrange
        var provider = new DataAnnotationsEntityModelMetadataProvider();

        // Act
        var metadata = provider.GetMetadata<AnnotatedEntity>();
        var code = metadata.Properties[nameof(AnnotatedEntity.Code)];
        var identity = metadata.Properties[nameof(AnnotatedEntity.Identity)];
        var computed = metadata.Properties[nameof(AnnotatedEntity.Computed)];
        var version = metadata.Properties[nameof(AnnotatedEntity.Version)];
        var ignored = metadata.Properties[nameof(AnnotatedEntity.Ignored)];

        // Assert
        metadata.TableName.ShouldBe("sales_orders");
        metadata.Schema.ShouldBe("sales");
        metadata.Properties[nameof(AnnotatedEntity.TenantId)].IsKey.ShouldBeTrue();
        metadata.Properties[nameof(AnnotatedEntity.OrderId)].IsKey.ShouldBeTrue();
        code.ColumnName.ShouldBe("order_code");
        code.ProviderTypeName.ShouldBe("varchar(32)");
        code.IsRequired.ShouldBeTrue();
        code.MaxLength.ShouldBe(20);
        metadata.Properties[nameof(AnnotatedEntity.Description)].MaxLength.ShouldBe(40);
        identity.IsDatabaseGenerated.ShouldBeTrue();
        identity.CanInsert.ShouldBeFalse();
        computed.IsDatabaseGenerated.ShouldBeTrue();
        computed.CanUpdate.ShouldBeFalse();
        version.IsConcurrencyToken.ShouldBeTrue();
        version.IsDatabaseGenerated.ShouldBeTrue();
        version.CanInsert.ShouldBeFalse();
        version.CanUpdate.ShouldBeFalse();
        metadata.Properties[nameof(AnnotatedEntity.ETag)].IsConcurrencyToken.ShouldBeTrue();
        ignored.IsIgnored.ShouldBeTrue();
        ignored.CanInsert.ShouldBeFalse();
        ignored.CanUpdate.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：未声明注解的类型应由 DataAnnotations 提供器明确交给后续 Provider 处理。
    /// </summary>
    [Fact]
    public void GetMetadata_WhenNoDataAnnotations_ShouldReturnNull()
    {
        // Arrange
        var provider = new DataAnnotationsEntityModelMetadataProvider();

        // Act
        var metadata = provider.GetMetadata<ConventionIdEntity>();

        // Assert
        metadata.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：组合提供器应优先使用前置自定义 Provider，并在其未处理时回退到 DataAnnotations 和约定。
    /// </summary>
    [Fact]
    public void GetMetadata_WhenCompositeProvidersConfigured_ShouldRespectPriorityAndFallback()
    {
        // Arrange
        var custom = new FixedEntityModelMetadataProvider(typeof(AnnotatedEntity), "custom_orders");
        var provider = new CompositeEntityModelMetadataProvider(new[] { custom });

        // Act
        var customMetadata = provider.GetMetadata<AnnotatedEntity>();
        var annotatedMetadata = provider.GetMetadata<AnnotatedFallbackEntity>();
        var conventionMetadata = provider.GetMetadata<ConventionIdEntity>();

        // Assert
        customMetadata.TableName.ShouldBe("custom_orders");
        annotatedMetadata.TableName.ShouldBe("annotated_fallback");
        conventionMetadata.TableName.ShouldBe(nameof(ConventionIdEntity));
    }

    /// <summary>
    /// 测试目的：实体和属性元数据公开集合应拒绝外部修改。
    /// </summary>
    [Fact]
    public void GetMetadata_WhenPropertiesExposed_ShouldBeImmutable()
    {
        // Arrange
        var metadata = new ConventionEntityModelMetadataProvider().GetMetadata<ConventionIdEntity>();
        var properties = (IDictionary<string, EntityPropertyMetadata>)metadata.Properties;

        // Act
        var action = () => properties.Add("Injected", metadata.Properties[nameof(ConventionIdEntity.Id)]);

        // Assert
        Should.Throw<NotSupportedException>(action);
        metadata.Properties.Count.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：映射解析器应消费完整模型字段、排除忽略属性并保留 Options 覆盖。
    /// </summary>
    [Fact]
    public void Resolve_WhenModelMetadataProvided_ShouldUseModelAndOptionsOverride()
    {
        // Arrange
        var options = new Configs.SqlMetadataOptions();
        options.EntityMappings.Add(new Configs.EntityMappingOptions
        {
            EntityType = typeof(AnnotatedEntity),
            TableName = "configured_orders",
            Schema = "configured",
            Columns =
            {
                [nameof(AnnotatedEntity.Code)] = new Configs.ColumnMappingOptions { ColumnName = "configured_code" }
            }
        });
        var resolver = new DefaultEntityMappingResolver(new DataAnnotationsEntityModelMetadataProvider(), null, options);

        // Act
        var mapping = resolver.Resolve(typeof(AnnotatedEntity), null);

        // Assert
        mapping.Model.TableName.ShouldBe("sales_orders");
        mapping.Table.TableName.ShouldBe("configured_orders");
        mapping.Table.Schema.ShouldBe("configured");
        mapping.Columns[nameof(AnnotatedEntity.Code)].ColumnName.ShouldBe("configured_code");
        mapping.Columns[nameof(AnnotatedEntity.TenantId)].IsKey.ShouldBeTrue();
        mapping.Columns[nameof(AnnotatedEntity.Version)].IsConcurrencyToken.ShouldBeTrue();
        mapping.Columns.ContainsKey(nameof(AnnotatedEntity.Ignored)).ShouldBeFalse();
    }

    /// <summary>
    /// 使用常见 Id 命名约定的测试实体。
    /// </summary>
    private sealed class ConventionIdEntity
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 使用实体类型名称加 Id 命名约定的测试实体。
    /// </summary>
    private sealed class ConventionNamedEntity
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int ConventionNamedEntityId { get; set; }
    }

    /// <summary>
    /// 使用数据注解配置表和列元数据的测试实体。
    /// </summary>
    [Table("sales_orders", Schema = "sales")]
    private sealed class AnnotatedEntity
    {
        /// <summary>
        /// 获取或设置租户键值。
        /// </summary>
        [Key]
        public int TenantId { get; set; }

        /// <summary>
        /// 获取或设置订单键值。
        /// </summary>
        [Key]
        public int OrderId { get; set; }

        /// <summary>
        /// 获取或设置订单代码。
        /// </summary>
        [Column("order_code", TypeName = "varchar(32)")]
        [Required]
        [StringLength(20)]
        public string Code { get; set; }

        /// <summary>
        /// 获取或设置订单说明。
        /// </summary>
        [MaxLength(40)]
        public string Description { get; set; }

        /// <summary>
        /// 获取或设置数据库生成的标识值。
        /// </summary>
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Identity { get; set; }

        /// <summary>
        /// 获取或设置数据库计算的数值。
        /// </summary>
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public decimal Computed { get; set; }

        /// <summary>
        /// 获取或设置时间戳并发令牌。
        /// </summary>
        [Timestamp]
        public byte[] Version { get; set; }

        /// <summary>
        /// 获取或设置字符串并发令牌。
        /// </summary>
        [ConcurrencyCheck]
        public string ETag { get; set; }

        /// <summary>
        /// 获取或设置不映射到数据库的说明。
        /// </summary>
        [NotMapped]
        public string Ignored { get; set; }
    }

    /// <summary>
    /// 具有表注解但使用约定列元数据的测试实体。
    /// </summary>
    [Table("annotated_fallback")]
    private sealed class AnnotatedFallbackEntity
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 返回固定表名并沿用约定属性元数据的 Provider 测试替身。
    /// </summary>
    private sealed class FixedEntityModelMetadataProvider : IEntityModelMetadataProvider
    {
        /// <summary>
        /// 该 Provider 支持的实体类型。
        /// </summary>
        private readonly Type _entityType;

        /// <summary>
        /// 该 Provider 返回的表名。
        /// </summary>
        private readonly string _tableName;

        /// <summary>
        /// 初始化一个 <see cref="FixedEntityModelMetadataProvider"/> 类型的实例。
        /// </summary>
        /// <param name="entityType">该 Provider 支持的实体类型。</param>
        /// <param name="tableName">该实体映射的表名。</param>
        public FixedEntityModelMetadataProvider(Type entityType, string tableName)
        {
            _entityType = entityType;
            _tableName = tableName;
        }

        /// <inheritdoc />
        public EntityModelMetadata GetMetadata(Type entityType)
        {
            if (entityType != _entityType)
                return null;
            var properties = new ConventionEntityModelMetadataProvider().GetMetadata(entityType).Properties.Values;
            return new EntityModelMetadata(entityType, _tableName, string.Empty, properties);
        }

        /// <inheritdoc />
        public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
    }
}
