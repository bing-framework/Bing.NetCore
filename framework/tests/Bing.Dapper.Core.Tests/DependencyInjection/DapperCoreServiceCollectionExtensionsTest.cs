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
    /// 测试目的：同一 Provider 实例重复注册应幂等，不同实例使用相同 Key 时应拒绝。
    /// </summary>
    [Fact]
    public void AddSqlBuilderProvider_WhenKeyRepeated_ShouldBeIdempotentOrThrowForDifferentInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = new TestSqlProvider(" custom ");

        // Act
        services.AddSqlBuilderProvider(provider, _ => null);
        services.AddSqlBuilderProvider(provider, _ => null);

        // Assert
        Assert.Single(services.Where(item => item.ServiceType == typeof(ISqlProvider)));
        Assert.Throws<InvalidOperationException>(() => services.AddSqlBuilderProvider(new TestSqlProvider("CUSTOM"), _ => null));
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
    /// 测试目的：空键数据源应使用默认 Key，Doris 数据源应关闭本地事务。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenKeyMissingOrDorisConfigured_ShouldUseDefaultKeyAndDisableTransactions()
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