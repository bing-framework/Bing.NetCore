using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Core.Tests.DependencyInjection;

/// <summary>
/// <see cref="DapperCoreServiceCollectionExtensions"/> 单元测试。
/// </summary>
public class DapperCoreServiceCollectionExtensionsTest
{
    /// <summary>
    /// 测试目的：重复注册核心服务时应保持各核心服务的单例注册。
    /// </summary>
    [Fact]
    public void AddSqlCore_WhenCalledRepeatedly_ShouldKeepSingletonRegistrationsIdempotent()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddSqlCore();
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.Same(provider.GetRequiredService<ISqlQueryFactory>(), provider.GetRequiredService<ISqlQueryFactory>());
        Assert.Same(provider.GetRequiredService<ISqlExecutorFactory>(), provider.GetRequiredService<ISqlExecutorFactory>());
        Assert.Same(provider.GetRequiredService<SqlMetadataOptions>(), provider.GetRequiredService<SqlMetadataOptions>());
    }

    /// <summary>
    /// 测试目的：自定义元数据提供器应作为组合提供器的前置层解析，且不得造成依赖循环。
    /// </summary>
    [Fact]
    public void AddEntityModelMetadataProvider_WhenRegisteredBeforeCore_ShouldPrecedeDefaultProviders()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEntityModelMetadataProvider<TestEntityModelMetadataProvider>();
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act
        var metadata = provider.GetRequiredService<IEntityModelMetadataProvider>().GetMetadata(typeof(TestEntity));

        // Assert
        Assert.Equal("custom_entities", metadata.TableName);
    }

    /// <summary>
    /// 测试目的：Provider 或创建委托为空以及空白 Key 时应拒绝注册。
    /// </summary>
    [Fact]
    public void AddSqlBuilderProvider_WhenArgumentsInvalid_ShouldThrowArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = new TestSqlProvider("test");

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => DapperCoreServiceCollectionExtensions.AddSqlBuilderProvider(null, provider, _ => null));
        Assert.Throws<ArgumentNullException>(() => services.AddSqlBuilderProvider(null, _ => null));
        Assert.Throws<ArgumentNullException>(() => services.AddSqlBuilderProvider(provider, null));
        Assert.Throws<ArgumentException>(() => services.AddSqlBuilderProvider(new TestSqlProvider(" "), _ => null));
    }

    /// <summary>
    /// 测试目的：同一 Provider 实例和创建委托重复注册应幂等，不同实例使用相同 Key 时应拒绝。
    /// </summary>
    [Fact]
    public void AddSqlBuilderProvider_WhenKeyRepeated_ShouldBeIdempotentOrThrowForDifferentInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = new TestSqlProvider(" custom ");

        // Act
        services.AddSqlBuilderProvider(provider, CreateTestBuilder);
        services.AddSqlBuilderProvider(provider, CreateTestBuilder);

        // Assert
        Assert.Single(services.Where(item => item.ServiceType == typeof(ISqlProvider)));
        Assert.Single(services.Where(item => item.ImplementationInstance is SqlBuilderFactoryRegistration));
        Assert.Throws<InvalidOperationException>(() =>
            services.AddSqlBuilderProvider(new TestSqlProvider("CUSTOM"), CreateTestBuilder));
    }

    /// <summary>
    /// 测试目的：同一 Provider 使用不同 Builder 创建委托时必须拒绝，不能因 Provider 已注册而忽略委托冲突。
    /// </summary>
    [Fact]
    public void AddSqlBuilderProvider_WhenCreatorDiffersForSameProvider_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = new TestSqlProvider("custom");
        services.AddSqlBuilderProvider(provider, CreateTestBuilder);

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() =>
            services.AddSqlBuilderProvider(provider, CreateAlternativeTestBuilder));
    }

    /// <summary>
    /// 测试目的：预先直接注册的同一 Provider 应可补充 Builder 创建委托，保证注册链完整。
    /// </summary>
    [Fact]
    public void AddSqlBuilderProvider_WhenProviderWasRegisteredDirectly_ShouldAddOnlyBuilderRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = new TestSqlProvider("custom");
        services.AddSingleton<ISqlProvider>(provider);

        // Act
        services.AddSqlBuilderProvider(provider, CreateTestBuilder);

        // Assert
        Assert.Single(services.Where(item => item.ServiceType == typeof(ISqlProvider)));
        Assert.Single(services.Where(item => item.ImplementationInstance is SqlBuilderFactoryRegistration));
    }

    /// <summary>
    /// 测试目的：多个元数据配置操作应按注册顺序应用。
    /// </summary>
    [Fact]
    public void ConfigureSqlMetadata_WhenMultipleActionsRegistered_ShouldApplyInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.BoolTrueValue = "first");
        services.ConfigureSqlMetadata(options => options.BoolTrueValue = "second");
        services.AddSqlCore();

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SqlMetadataOptions>();

        // Assert
        Assert.Equal("second", options.BoolTrueValue);
    }

    /// <summary>
    /// 测试目的：通过依赖注入配置的映射缓存容量应传递给单例解析器，并限制不同最终映射的缓存条目数。
    /// </summary>
    [Fact]
    public void ConfigureSqlMetadata_WhenMappingCacheCapacityIsConfigured_ShouldApplyToResolver()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappingCacheCapacity = 1;
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(TestEntity),
                MappingProfile = "primary",
                TableName = "primary_entities"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(TestEntity),
                MappingProfile = "reporting",
                TableName = "reporting_entities"
            });
        });
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();
        var resolver = Assert.IsType<DefaultEntityMappingResolver>(provider.GetRequiredService<IEntityMappingResolver>());

        // Act
        var primary = resolver.Resolve(typeof(TestEntity), new DatabaseContext { MappingProfile = "primary" });
        var reporting = resolver.Resolve(typeof(TestEntity), new DatabaseContext { MappingProfile = "reporting" });
        var cachedPrimary = resolver.Resolve(typeof(TestEntity), new DatabaseContext { MappingProfile = "primary" });
        var uncachedReporting = resolver.Resolve(typeof(TestEntity), new DatabaseContext { MappingProfile = "reporting" });

        // Assert
        Assert.Equal("primary_entities", primary.Table.TableName);
        Assert.Equal("reporting_entities", reporting.Table.TableName);
        Assert.Same(primary, cachedPrimary);
        Assert.NotSame(reporting, uncachedReporting);
    }

    /// <summary>
    /// 测试目的：空键数据源应使用默认 Key，Doris 数据源默认只读且关闭本地事务。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenKeyMissingOrDorisConfigured_ShouldUseDefaultKeyAndApplyReadOnlyDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlDataSource(null, DatabaseType.Doris, "Server=doris;Database=analytics;");
        services.AddSqlCore();

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SqlMetadataOptions>();
        var dataSource = options.DataSources.DataSources[options.DataSources.DefaultDataSourceKey];

        // Assert
        Assert.Equal("default", dataSource.Key);
        Assert.Equal(DatabaseType.Doris, dataSource.DatabaseType);
        Assert.True(dataSource.IsReadOnly);
        Assert.False(dataSource.SupportsTransactions);
        Assert.Equal("Server=doris;Database=analytics;", dataSource.ConnectionString);
    }

    /// <summary>
    /// 测试目的：默认数据源已绑定其他 Provider 时，无键重复注册不得覆盖其类型。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenDefaultProviderChanges_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlDataSource(null, DatabaseType.Sqlite, "Data Source=first.db");
        services.AddSqlDataSource(null, DatabaseType.SqlServer, "Server=second");
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<SqlMetadataOptions>());
    }

    /// <summary>
    /// 测试目的：相同具名数据源以不同连接配置重复注册时必须拒绝，不能由后注册项静默覆盖。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenNamedKeyIsRepeatedWithDifferentConfiguration_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlDataSource("reporting", DatabaseType.Sqlite, "Data Source=first.db");
        services.AddSqlDataSource(" REPORTING ", DatabaseType.Sqlite, "Data Source=second.db");
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act and Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<SqlMetadataOptions>());
        Assert.Contains("reporting", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("重复注册", exception.Message);
    }

    /// <summary>
    /// 测试目的：相同具名数据源重复注册应保持幂等，避免旧注册组合产生重复配置。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenNamedConfigurationIsIdentical_ShouldRemainIdempotent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlDataSource("reporting", DatabaseType.Sqlite, "Data Source=reporting.db");
        services.AddSqlDataSource(" REPORTING ", DatabaseType.Sqlite, "Data Source=reporting.db");
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<SqlMetadataOptions>();

        // Assert
        Assert.Single(options.DataSources.DataSources);
        Assert.Equal("reporting", options.DataSources.DataSources["reporting"].Key);
    }

    /// <summary>
    /// 测试目的：默认 Key 被显式清空时，首个具名数据源不得隐式成为默认源，解析器只能通过唯一数据源规则回退。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenDefaultKeyIsEmptyAndNamedSourceAdded_ShouldKeepDefaultKeyEmpty()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.DataSources.DefaultDataSourceKey = null);
        services.AddSqlDataSource("reporting", DatabaseType.Sqlite, "Data Source=reporting.db");
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<SqlMetadataOptions>();
        var dataSource = provider.GetRequiredService<ISqlDataSourceResolver>().Resolve();

        // Assert
        Assert.Null(options.DataSources.DefaultDataSourceKey);
        Assert.Equal("reporting", dataSource.Key);
    }

    /// <summary>
    /// 测试目的：未配置默认 Key 时，无键数据源注册必须明确失败，避免将空键写入数据源集合。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenDefaultKeyIsEmptyAndKeyMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.DataSources.DefaultDataSourceKey = null);
        services.AddSqlDataSource(null, DatabaseType.Sqlite, "Data Source=default.db");
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act and Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<SqlMetadataOptions>());
        Assert.Contains(nameof(SqlDataSourceOptions.DefaultDataSourceKey), exception.Message);
    }

    /// <summary>
    /// 仅用于 Provider Key 注册测试的 SQL Provider。
    /// </summary>
    private sealed class TestSqlProvider : ISqlProvider
    {
        /// <summary>
        /// 初始化一个 <see cref="TestSqlProvider"/> 类型的实例。
        /// </summary>
        /// <param name="key">Provider 标识。</param>
        public TestSqlProvider(string key) => Key = key;

        /// <inheritdoc />
        public string Key { get; }

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.Sqlite;

        /// <inheritdoc />
        public IDialect Dialect => null;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory => null;

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => null;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer => null;

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => null;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => null;
    }

    /// <summary>
    /// 创建用于验证同一委托幂等注册的占位 Builder 委托。
    /// </summary>
    /// <param name="services">传入创建委托的查询级共享服务。</param>
    /// <returns>始终返回 <see langword="null"/> 的测试占位值；本测试不创建实际 Builder。</returns>
    private static ISqlBuilder CreateTestBuilder(SqlBuilderServices services) => null;

    /// <summary>
    /// 创建用于验证不同委托冲突的备用占位 Builder 委托。
    /// </summary>
    /// <param name="services">传入创建委托的查询级共享服务。</param>
    /// <returns>始终返回 <see langword="null"/> 的测试占位值；本测试不创建实际 Builder。</returns>
    private static ISqlBuilder CreateAlternativeTestBuilder(SqlBuilderServices services) => null;

    /// <summary>
    /// 仅用于实体元数据注册测试的实体。
    /// </summary>
    private sealed class TestEntity
    {
        /// <summary>
        /// 实体标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 仅用于实体元数据注册测试的自定义提供器。
    /// </summary>
    private sealed class TestEntityModelMetadataProvider : IEntityModelMetadataProvider
    {
        /// <inheritdoc />
        public EntityModelMetadata GetMetadata(Type entityType)
        {
            if (entityType != typeof(TestEntity))
                return null;
            return new EntityModelMetadata(entityType, "custom_entities", string.Empty,
                new[] { new EntityPropertyMetadata(entityType.GetProperty(nameof(TestEntity.Id)), isKey: true) });
        }

        /// <inheritdoc />
        public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
    }
}