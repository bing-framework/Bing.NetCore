using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Core.Tests.DependencyInjection;

/// <summary>
/// <see cref="ISqlProviderResolver"/> 单元测试。
/// </summary>
public class SqlProviderResolverTest
{
    /// <summary>
    /// 测试目的：Provider Key 应忽略大小写和首尾空白，且未知 Key 应返回明确异常。
    /// </summary>
    [Fact]
    public void Resolve_WhenProviderKeyUsesDifferentCaseOrIsUnknown_ShouldNormalizeOrThrow()
    {
        // Arrange
        var expected = new TestSqlProvider("custom.sqlite", DatabaseType.Sqlite);
        using var serviceProvider = CreateServiceProvider(expected);
        var resolver = serviceProvider.GetRequiredService<ISqlProviderResolver>();

        // Act
        var actual = resolver.Resolve("  CUSTOM.SQLITE  ");
        var exception = Assert.Throws<NotSupportedException>(() => resolver.Resolve("custom.missing"));

        // Assert
        Assert.Same(expected, actual);
        Assert.Contains("custom.missing", exception.Message);
    }

    /// <summary>
    /// 测试目的：同一数据库类型的多个 Provider 必须由数据源 Key 精确区分，不得依赖注册顺序。
    /// </summary>
    [Fact]
    public void Resolve_WhenDataSourceProviderKeyIsSpecified_ShouldSelectExactProvider()
    {
        // Arrange
        var first = new TestSqlProvider("custom.sqlite.first", DatabaseType.Sqlite);
        var second = new TestSqlProvider("custom.sqlite.second", DatabaseType.Sqlite);
        using var serviceProvider = CreateServiceProvider(first, second);
        var resolver = serviceProvider.GetRequiredService<ISqlProviderResolver>();
        var context = new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor
            {
                DatabaseType = DatabaseType.Sqlite,
                ProviderKey = "custom.sqlite.second"
            }
        };

        // Act
        var actual = resolver.Resolve(context);

        // Assert
        Assert.Same(second, actual);
    }

    /// <summary>
    /// 测试目的：数据源声明的数据库类型与显式 Provider 类型冲突时必须拒绝解析，避免方言和元数据规则混用。
    /// </summary>
    [Fact]
    public void Resolve_WhenDataSourceProviderTypeConflictsWithDatabaseType_ShouldThrow()
    {
        // Arrange
        var sqlite = new TestSqlProvider("custom.sqlite", DatabaseType.Sqlite);
        using var serviceProvider = CreateServiceProvider(sqlite);
        var resolver = serviceProvider.GetRequiredService<ISqlProviderResolver>();
        var context = new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor
            {
                DatabaseType = DatabaseType.SqlServer,
                ProviderKey = sqlite.Key
            }
        };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(context));

        // Assert
        Assert.Equal("数据源 DatabaseType SqlServer 与 Provider custom.sqlite 的数据库类型 Sqlite 不兼容。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Doris 数据源应继续复用官方 MySQL Provider，保持既有协议兼容行为。
    /// </summary>
    [Fact]
    public void Resolve_WhenDorisUsesOfficialMySqlProvider_ShouldAllowCompatibility()
    {
        // Arrange
        var mySql = new TestSqlProvider("bing.mysql", DatabaseType.MySql);
        using var serviceProvider = CreateServiceProvider(mySql);
        var resolver = serviceProvider.GetRequiredService<ISqlProviderResolver>();
        var context = new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor
            {
                DatabaseType = DatabaseType.Doris,
                ProviderKey = mySql.Key
            }
        };

        // Act
        var actual = resolver.Resolve(context);

        // Assert
        Assert.Same(mySql, actual);
    }

    /// <summary>
    /// 测试目的：数据源 Key、上下文 Key、显式 Provider 和官方数据库类型兼容映射应按既定优先级解析。
    /// </summary>
    [Fact]
    public void Resolve_WhenMultipleProviderSourcesAreAvailable_ShouldUseDefinedPrecedence()
    {
        // Arrange
        var official = new TestSqlProvider("bing.sqlite", DatabaseType.Sqlite);
        var dataSource = new TestSqlProvider("custom.datasource", DatabaseType.Sqlite);
        var contextProvider = new TestSqlProvider("custom.context", DatabaseType.Sqlite);
        var explicitProvider = new TestSqlProvider("custom.explicit", DatabaseType.Sqlite);
        using var serviceProvider = CreateServiceProvider(official, dataSource, contextProvider, explicitProvider);
        var resolver = serviceProvider.GetRequiredService<ISqlProviderResolver>();
        var context = new DatabaseContext
        {
            ProviderKey = contextProvider.Key,
            DataSource = new SqlDataSourceDescriptor
            {
                DatabaseType = DatabaseType.Sqlite,
                ProviderKey = dataSource.Key
            }
        };

        // Act
        var dataSourceResolved = resolver.Resolve(context, explicitProvider);
        context.DataSource.ProviderKey = null;
        var contextResolved = resolver.Resolve(context, explicitProvider);
        context.ProviderKey = null;
        var explicitResolved = resolver.Resolve(context, explicitProvider);
        var compatibilityResolved = resolver.Resolve(new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.Sqlite }
        });

        // Assert
        Assert.Same(dataSource, dataSourceResolved);
        Assert.Same(contextProvider, contextResolved);
        Assert.Same(explicitProvider, explicitResolved);
        Assert.Same(official, compatibilityResolved);
    }

    /// <summary>
    /// 测试目的：数据源注册应保留 Provider Key，并由核心服务解析同一个 Provider 实例。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenProviderKeyConfigured_ShouldResolveRegisteredProviderInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var expected = new TestSqlProvider("custom.sqlite", DatabaseType.Sqlite);
        services.AddSingleton<ISqlProvider>(expected);
        services.AddSqlDataSource("custom", DatabaseType.Sqlite, "Data Source=:memory:", providerKey: " CUSTOM.SQLITE ");
        services.AddSqlCore();
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var dataSource = serviceProvider.GetRequiredService<ISqlDataSourceResolver>().Resolve("custom");
        var actual = serviceProvider.GetRequiredService<ISqlProviderResolver>().Resolve(new DatabaseContext
        {
            DataSource = dataSource
        });

        // Assert
        Assert.Equal("CUSTOM.SQLITE", dataSource.ProviderKey.Trim(), StringComparer.OrdinalIgnoreCase);
        Assert.Same(expected, actual);
    }

    /// <summary>
    /// 通过公开服务注册创建 Provider 解析器。
    /// </summary>
    private static ServiceProvider CreateServiceProvider(params ISqlProvider[] providers)
    {
        var services = new ServiceCollection();
        foreach (var provider in providers)
            services.AddSingleton(provider);
        services.AddSqlCore();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 仅用于 Provider 解析测试的 SQL Provider。
    /// </summary>
    private sealed class TestSqlProvider : ISqlProvider
    {
        /// <summary>
        /// 初始化一个 <see cref="TestSqlProvider"/> 类型的实例。
        /// </summary>
        /// <param name="key">Provider 唯一标识。</param>
        /// <param name="databaseType">数据库类型。</param>
        public TestSqlProvider(string key, DatabaseType databaseType)
        {
            Key = key;
            DatabaseType = databaseType;
        }

        /// <inheritdoc />
        public string Key { get; }

        /// <inheritdoc />
        public DatabaseType DatabaseType { get; }

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
}