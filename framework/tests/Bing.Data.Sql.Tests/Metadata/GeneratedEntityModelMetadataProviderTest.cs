using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// 编译期生成实体模型元数据提供器契约测试。
/// </summary>
public sealed class GeneratedEntityModelMetadataProviderTest
{
    /// <summary>
    /// 测试目的：生成属性描述应保留成员能力、CLR 类型、映射名及约束标记，而不依赖 PropertyInfo。
    /// </summary>
    [Fact]
    public void EntityPropertyMetadata_WhenGeneratedDescriptorIsUsed_ShouldRetainAllDeclaredFacts()
    {
        var ignoredKey = EntityPropertyMetadata.CreateGenerated(
            "NullableIdentity",
            typeof(int?),
            canRead: false,
            canWrite: true,
            columnName: "nullable_identity",
            isIgnored: true,
            isKey: true,
            databaseGeneratedOption: DatabaseGeneratedOption.Identity,
            isConcurrencyToken: true,
            isRequired: false,
            maxLength: 24,
            providerTypeName: "numeric(12,2)");
        var requiredValue = EntityPropertyMetadata.CreateGenerated(
            "RequiredValue",
            typeof(int),
            canRead: true,
            canWrite: false,
            isRequired: true);

        Assert.Null(ignoredKey.Property);
        Assert.Equal("NullableIdentity", ignoredKey.PropertyName);
        Assert.Equal("nullable_identity", ignoredKey.ColumnName);
        Assert.Equal(typeof(int?), ignoredKey.ClrType);
        Assert.False(ignoredKey.CanRead);
        Assert.True(ignoredKey.CanWrite);
        Assert.True(ignoredKey.IsIgnored);
        Assert.True(ignoredKey.IsKey);
        Assert.Equal(DatabaseGeneratedOption.Identity, ignoredKey.DatabaseGeneratedOption);
        Assert.True(ignoredKey.IsDatabaseGenerated);
        Assert.True(ignoredKey.IsConcurrencyToken);
        Assert.False(ignoredKey.IsRequired);
        Assert.Equal(24, ignoredKey.MaxLength);
        Assert.Equal("numeric(12,2)", ignoredKey.ProviderTypeName);
        Assert.True(ignoredKey.IsNullable);
        Assert.False(ignoredKey.CanInsert);
        Assert.False(ignoredKey.CanUpdate);

        Assert.Null(requiredValue.Property);
        Assert.Equal("RequiredValue", requiredValue.ColumnName);
        Assert.Equal(typeof(int), requiredValue.ClrType);
        Assert.True(requiredValue.CanRead);
        Assert.False(requiredValue.CanWrite);
        Assert.True(requiredValue.IsRequired);
        Assert.False(requiredValue.IsNullable);
        Assert.True(requiredValue.CanInsert);
        Assert.True(requiredValue.CanUpdate);
    }

    /// <summary>
    /// 测试目的：旧 PropertyInfo 构造方式应继续推导访问器能力并保留反射对象。
    /// </summary>
    [Fact]
    public void EntityPropertyMetadata_WhenReflectionPropertyIsUsed_ShouldPreserveLegacyContract()
    {
        var readOnlyProperty = typeof(ReflectionEntity).GetProperty(nameof(ReflectionEntity.ReadOnly));
        var writeOnlyProperty = typeof(ReflectionEntity).GetProperty(nameof(ReflectionEntity.WriteOnly));
        var privateSetterProperty = typeof(ReflectionEntity).GetProperty(nameof(ReflectionEntity.PrivateSetter));

        var readOnly = new EntityPropertyMetadata(readOnlyProperty, "read_only", isKey: true);
        var writeOnly = new EntityPropertyMetadata(writeOnlyProperty);
        var privateSetter = new EntityPropertyMetadata(privateSetterProperty);

        Assert.Same(readOnlyProperty, readOnly.Property);
        Assert.Equal(typeof(int), readOnly.ClrType);
        Assert.True(readOnly.CanRead);
        Assert.False(readOnly.CanWrite);
        Assert.True(readOnly.IsKey);
        Assert.Equal("read_only", readOnly.ColumnName);

        Assert.Same(writeOnlyProperty, writeOnly.Property);
        Assert.False(writeOnly.CanRead);
        Assert.True(writeOnly.CanWrite);

        Assert.Same(privateSetterProperty, privateSetter.Property);
        Assert.True(privateSetter.CanRead);
        Assert.False(privateSetter.CanWrite);
    }

    /// <summary>
    /// 测试目的：生成属性描述缺少必需身份信息时应拒绝构造。
    /// </summary>
    [Fact]
    public void EntityPropertyMetadata_WhenGeneratedIdentityIsMissing_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => EntityPropertyMetadata.CreateGenerated(" ", typeof(int), true, true));
        Assert.Throws<ArgumentNullException>(() => EntityPropertyMetadata.CreateGenerated("Value", null, true, true));
        Assert.Throws<ArgumentNullException>(() => new EntityPropertyMetadata((PropertyInfo)null));
    }

    /// <summary>
    /// 测试目的：生成提供器应返回已注册模型，并对未注册或空类型返回空值。
    /// </summary>
    [Fact]
    public void Register_WhenModelIsRegistered_ShouldReturnItAndLeaveMissingTypesUnresolved()
    {
        var provider = new GeneratedEntityModelMetadataProvider();
        var model = new EntityModelMetadata(
            typeof(RegisteredEntity),
            "generated_registered",
            "generated_schema",
            new[] { EntityPropertyMetadata.CreateGenerated(nameof(RegisteredEntity.Id), typeof(long), true, true, isKey: true) });

        Assert.Null(provider.GetMetadata(typeof(UnregisteredEntity)));
        Assert.Null(provider.GetMetadata((Type)null));

        GeneratedEntityModelMetadataProvider.Register(model);

        Assert.Same(model, provider.GetMetadata(typeof(RegisteredEntity)));
        Assert.Same(model, provider.GetMetadata<RegisteredEntity>());
        Assert.Throws<ArgumentNullException>(() => GeneratedEntityModelMetadataProvider.Register(null));
    }

    /// <summary>
    /// 测试目的：组合提供器应按自定义、生成、DataAnnotations、约定顺序选择首个匹配模型。
    /// </summary>
    [Fact]
    public void Composite_WhenAllMetadataSourcesAreAvailable_ShouldRespectSourcePriority()
    {
        var generatedAnnotated = new EntityModelMetadata(
            typeof(GeneratedAnnotatedEntity),
            "generated_table",
            "generated_schema",
            new[]
            {
                EntityPropertyMetadata.CreateGenerated(nameof(GeneratedAnnotatedEntity.Code), typeof(string), true, true,
                    columnName: "generated_code", isRequired: true)
            });
        var generatedCustom = new EntityModelMetadata(
            typeof(CustomOverGeneratedEntity),
            "generated_custom_table",
            "generated_custom_schema",
            new[]
            {
                EntityPropertyMetadata.CreateGenerated(nameof(CustomOverGeneratedEntity.Code), typeof(string), true, true,
                    columnName: "generated_custom_code")
            });
        GeneratedEntityModelMetadataProvider.Register(generatedAnnotated);
        GeneratedEntityModelMetadataProvider.Register(generatedCustom);

        var explicitModel = new EntityModelMetadata(
            typeof(CustomOverGeneratedEntity),
            "explicit_custom_table",
            "explicit_custom_schema",
            new[]
            {
                EntityPropertyMetadata.CreateGenerated(nameof(CustomOverGeneratedEntity.Code), typeof(string), true, true,
                    columnName: "explicit_custom_code")
            });
        var provider = new CompositeEntityModelMetadataProvider(new IEntityModelMetadataProvider[]
        {
            new FixedMetadataProvider(typeof(CustomOverGeneratedEntity), explicitModel)
        });

        var custom = provider.GetMetadata<CustomOverGeneratedEntity>();
        var generated = provider.GetMetadata<GeneratedAnnotatedEntity>();
        var annotations = provider.GetMetadata<AnnotationsFallbackEntity>();
        var convention = provider.GetMetadata<ConventionFallbackEntity>();

        Assert.Same(explicitModel, custom);
        Assert.Equal("explicit_custom_table", custom.TableName);
        Assert.Equal("explicit_custom_code", custom.Properties[nameof(CustomOverGeneratedEntity.Code)].ColumnName);

        Assert.Same(generatedAnnotated, generated);
        Assert.Equal("generated_table", generated.TableName);
        Assert.Equal("generated_code", generated.Properties[nameof(GeneratedAnnotatedEntity.Code)].ColumnName);

        Assert.Equal("annotations_fallback_table", annotations.TableName);
        Assert.Equal(nameof(ConventionFallbackEntity), convention.TableName);
    }

    /// <summary>
    /// 提供实体模型元数据来源测试使用的查询数据模型。
    /// </summary>
    private sealed class ReflectionEntity
    {
        /// <summary>
        /// 获取仅包含 getter 的元数据测试属性。
        /// </summary>
        public int ReadOnly => 1;

        /// <summary>
        /// 设置仅包含 setter 的元数据测试属性。
        /// </summary>
        public int WriteOnly { set { } }

        /// <summary>
        /// 获取包含私有 setter 的元数据测试属性。
        /// </summary>
        public int PrivateSetter { get; private set; }
    }

    /// <summary>
    /// 提供实体模型元数据来源测试使用的查询数据模型。
    /// </summary>
    private sealed class RegisteredEntity
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public long Id { get; set; }
    }

    /// <summary>
    /// 提供实体模型元数据来源测试使用的查询数据模型。
    /// </summary>
    private sealed class UnregisteredEntity
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 提供实体模型元数据来源测试使用的查询数据模型。
    /// </summary>
    [Table("annotations_generated_table")]
    private sealed class GeneratedAnnotatedEntity
    {
        /// <summary>
        /// 获取或设置业务编码。
        /// </summary>
        [Column("annotations_code")]
        public string Code { get; set; }
    }

    /// <summary>
    /// 提供实体模型元数据来源测试使用的查询数据模型。
    /// </summary>
    [Table("annotations_custom_table")]
    private sealed class CustomOverGeneratedEntity
    {
        /// <summary>
        /// 获取或设置业务编码。
        /// </summary>
        [Column("annotations_custom_code")]
        public string Code { get; set; }
    }

    /// <summary>
    /// 提供实体模型元数据来源测试使用的查询数据模型。
    /// </summary>
    [Table("annotations_fallback_table")]
    private sealed class AnnotationsFallbackEntity
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 提供实体模型元数据来源测试使用的查询数据模型。
    /// </summary>
    private sealed class ConventionFallbackEntity
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 为实体模型提供固定元数据的测试实现。
    /// </summary>
    private sealed class FixedMetadataProvider : IEntityModelMetadataProvider
    {
        /// <summary>
        /// 保存固定元数据提供程序返回的实体类型。
        /// </summary>
        private readonly Type _entityType;
        /// <summary>
        /// 保存固定元数据提供程序返回的实体模型。
        /// </summary>
        private readonly EntityModelMetadata _model;

        /// <summary>
        /// 初始化一个 <see cref="FixedMetadataProvider" /> 类型的实例。
        /// </summary>
        /// <param name="entityType">实体的 CLR 类型。</param>
        /// <param name="model">用于分析当前调用的语义模型。</param>
        public FixedMetadataProvider(Type entityType, EntityModelMetadata model)
        {
            _entityType = entityType;
            _model = model;
        }

        /// <inheritdoc />
        public EntityModelMetadata GetMetadata(Type entityType) => entityType == _entityType ? _model : null;

        /// <inheritdoc />
        public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
    }
}
