using Bing.Data.Filters;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 验证 scoped SQL 工厂创建的 typed Query 使用共享映射缓存并动态读取作用域过滤状态。
/// </summary>
public sealed class SqlFactoryCacheScopeContractTest
{
    private const string SharedDataSource = "cache-shared";
    private const string IsolatedDataSource = "cache-isolated";

    private const string SharedEnabledSql =
        "Select `cache_scope_shared`.`Id`,`cache_scope_shared`.`Name`,`cache_scope_shared`.`IsDeleted` \r\nFrom `cache_scope_shared` \r\nWhere `cache_scope_shared`.`IsDeleted`=@_p_0";

    private const string SharedDisabledSql =
        "Select `cache_scope_shared`.`Id`,`cache_scope_shared`.`Name`,`cache_scope_shared`.`IsDeleted` \r\nFrom `cache_scope_shared`";

    private const string IsolatedEnabledSql =
        "Select `cache_scope_isolated`.`Id`,`cache_scope_isolated`.`Name`,`cache_scope_isolated`.`IsDeleted` \r\nFrom `cache_scope_isolated` \r\nWhere `cache_scope_isolated`.`IsDeleted`=@_p_0";

    /// <summary>
    /// 测试 - typed factory 渲染应冷启动并跨 scope 复用 Singleton 映射缓存，同时保持过滤状态与路由独立。
    /// </summary>
    [Fact]
    public void TypedRendering_WhenScopesShareCache_ShouldReuseMappingsAndReadCurrentFilters()
    {
        var connectionString = $"Data Source=file:sql-factory-cache-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        using (var command = keeper.CreateCommand())
        {
            command.CommandText = "Create Table cache_scope_shared (Id Integer Primary Key, Name Text Not Null, IsDeleted Integer Not Null); " +
                                 "Insert Into cache_scope_shared Values (1, 'shared-active', 0), (2, 'shared-deleted', 1); " +
                                 "Create Table cache_scope_isolated (Id Integer Primary Key, Name Text Not Null, IsDeleted Integer Not Null); " +
                                 "Insert Into cache_scope_isolated Values (3, 'isolated-active', 0), (4, 'isolated-deleted', 1);";
            command.ExecuteNonQuery();
        }

        using var root = CreateServiceProvider(connectionString);
        var resolver = Assert.IsType<DefaultEntityMappingResolver>(
            root.GetRequiredService<IEntityMappingResolver>());
        var beforeColdRender = resolver.MappingCacheStatistics;
        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        var resolverA = scopeA.ServiceProvider.GetRequiredService<IEntityMappingResolver>();
        var resolverB = scopeB.ServiceProvider.GetRequiredService<IEntityMappingResolver>();
        Assert.Same(resolver, resolverA);
        Assert.Same(resolverA, resolverB);

        var factoryA = scopeA.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        var factoryB = scopeB.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        var filterA = scopeA.ServiceProvider.GetRequiredService<IDataFilter>();
        var filterB = scopeB.ServiceProvider.GetRequiredService<IDataFilter>();

        using var queryA = factoryA.Create(SharedDataSource);
        var sourceA = queryA.From<SqlFactoryCacheScopeProbeEntity>();
        AssertRendered(sourceA, SharedEnabledSql, expectSoftDeleteParameter: true);
        var afterColdRender = resolver.MappingCacheStatistics;
        Assert.True(afterColdRender.CacheMissCount > beforeColdRender.CacheMissCount);
        Assert.True(afterColdRender.EntryCount > beforeColdRender.EntryCount);

        var beforeWarmRender = resolver.MappingCacheStatistics;
        using var warmQueryA = factoryA.Create(SharedDataSource);
        var warmSourceA = warmQueryA.From<SqlFactoryCacheScopeProbeEntity>();
        AssertRendered(warmSourceA, SharedEnabledSql, expectSoftDeleteParameter: true);
        var afterWarmRender = resolver.MappingCacheStatistics;
        Assert.True(afterWarmRender.CacheHitCount > beforeWarmRender.CacheHitCount);
        Assert.Equal(afterColdRender.EntryCount, afterWarmRender.EntryCount);

        var beforeScopeBRender = resolver.MappingCacheStatistics;
        using var queryB = factoryB.Create(SharedDataSource);
        var sourceB = queryB.From<SqlFactoryCacheScopeProbeEntity>();
        AssertRendered(sourceB, SharedEnabledSql, expectSoftDeleteParameter: true);
        var afterScopeBRender = resolver.MappingCacheStatistics;
        Assert.True(afterScopeBRender.CacheHitCount > beforeScopeBRender.CacheHitCount);
        Assert.Equal(afterWarmRender.EntryCount, afterScopeBRender.EntryCount);

        var beforeFilterRenders = resolver.MappingCacheStatistics;
        using (filterA.Disable<ISoftDelete>())
        {
            AssertRendered(sourceA, SharedDisabledSql, expectSoftDeleteParameter: false);

            using (var disabledQueryA = factoryA.Create(SharedDataSource))
            {
                var disabledSourceA = disabledQueryA.From<SqlFactoryCacheScopeProbeEntity>();
                AssertRendered(disabledSourceA, SharedDisabledSql, expectSoftDeleteParameter: false);
                Assert.Equal(new[] { 1, 2 }, disabledSourceA.ToList<SqlFactoryCacheScopeProbeEntity>()
                    .Select(entity => entity.Id).OrderBy(id => id));
            }

            AssertRendered(sourceB, SharedEnabledSql, expectSoftDeleteParameter: true);
            using (var enabledQueryB = factoryB.Create(SharedDataSource))
            {
                var enabledSourceB = enabledQueryB.From<SqlFactoryCacheScopeProbeEntity>();
                AssertRendered(enabledSourceB, SharedEnabledSql, expectSoftDeleteParameter: true);
                Assert.Equal(new[] { 1 }, enabledSourceB.ToList<SqlFactoryCacheScopeProbeEntity>()
                    .Select(entity => entity.Id).OrderBy(id => id));
            }

            using (filterB.Disable<ISoftDelete>())
            {
                AssertRendered(sourceB, SharedDisabledSql, expectSoftDeleteParameter: false);
                AssertRendered(sourceA, SharedDisabledSql, expectSoftDeleteParameter: false);
                using var disabledQueryB = factoryB.Create(SharedDataSource);
                var disabledSourceB = disabledQueryB.From<SqlFactoryCacheScopeProbeEntity>();
                AssertRendered(disabledSourceB, SharedDisabledSql, expectSoftDeleteParameter: false);
                Assert.Equal(new[] { 1, 2 }, disabledSourceB.ToList<SqlFactoryCacheScopeProbeEntity>()
                    .Select(entity => entity.Id).OrderBy(id => id));
            }

            AssertRendered(sourceB, SharedEnabledSql, expectSoftDeleteParameter: true);
            AssertRendered(sourceA, SharedDisabledSql, expectSoftDeleteParameter: false);
        }

        for (var cycle = 0; cycle < 3; cycle++)
        {
            AssertRendered(sourceA, SharedEnabledSql, expectSoftDeleteParameter: true);
            using (filterA.Disable<ISoftDelete>())
                AssertRendered(sourceA, SharedDisabledSql, expectSoftDeleteParameter: false);
            AssertRendered(sourceA, SharedEnabledSql, expectSoftDeleteParameter: true);
        }

        var afterFilterRenders = resolver.MappingCacheStatistics;
        Assert.Equal(beforeFilterRenders.CacheMissCount, afterFilterRenders.CacheMissCount);
        Assert.Equal(beforeFilterRenders.EntryCount, afterFilterRenders.EntryCount);
        Assert.Equal(new[] { 1 }, sourceA.ToList<SqlFactoryCacheScopeProbeEntity>()
            .Select(entity => entity.Id).OrderBy(id => id));
        Assert.Equal(new[] { 1 }, sourceB.ToList<SqlFactoryCacheScopeProbeEntity>()
                .Select(entity => entity.Id).OrderBy(id => id));

        var beforeIsolatedRoute = resolver.MappingCacheStatistics;
        using var isolatedQuery = factoryB.Create(IsolatedDataSource);
        var isolatedSource = isolatedQuery.From<SqlFactoryCacheScopeProbeEntity>();
        AssertRendered(isolatedSource, IsolatedEnabledSql, expectSoftDeleteParameter: true);
        var afterIsolatedRoute = resolver.MappingCacheStatistics;
        Assert.True(afterIsolatedRoute.CacheMissCount > beforeIsolatedRoute.CacheMissCount);
        Assert.True(afterIsolatedRoute.EntryCount > beforeIsolatedRoute.EntryCount);
        Assert.Equal(new[] { 3 }, isolatedSource.ToList<SqlFactoryCacheScopeProbeEntity>()
            .Select(entity => entity.Id).OrderBy(id => id));

        var beforeIsolatedWarmRender = resolver.MappingCacheStatistics;
        using var isolatedWarmQuery = factoryB.Create(IsolatedDataSource);
        var isolatedWarmSource = isolatedWarmQuery.From<SqlFactoryCacheScopeProbeEntity>();
        AssertRendered(isolatedWarmSource, IsolatedEnabledSql, expectSoftDeleteParameter: true);
        var afterIsolatedWarmRender = resolver.MappingCacheStatistics;
        Assert.True(afterIsolatedWarmRender.CacheHitCount > beforeIsolatedWarmRender.CacheHitCount);
        Assert.Equal(afterIsolatedRoute.EntryCount, afterIsolatedWarmRender.EntryCount);
    }

    private static void AssertRendered(SqlLambdaQuery source, string expectedSql, bool expectSoftDeleteParameter)
    {
        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(
            ((ISqlQueryBuilderAccessor)source).GetSqlBuilder());
        Assert.Equal(expectedSql, snapshot.Sql);
        Assert.Equal(expectedSql, source.ToSql());

        if (expectSoftDeleteParameter)
        {
            var parameter = Assert.Single(snapshot.Parameters);
            Assert.Equal("@_p_0", parameter.Name);
            Assert.Equal(false, parameter.Value);
        }
        else
            Assert.Empty(snapshot.Parameters);
    }

    private static ServiceProvider CreateServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource(SharedDataSource, DatabaseType.Sqlite, connectionString,
            setupAction: dataSource => dataSource.MappingProfile = "cache-profile-shared");
        services.AddSqlDataSource(IsolatedDataSource, DatabaseType.Sqlite, connectionString,
            setupAction: dataSource => dataSource.MappingProfile = "cache-profile-isolated");
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(SqlFactoryCacheScopeProbeEntity),
                DbKey = SharedDataSource,
                MappingProfile = "cache-profile-shared",
                TableName = "cache_scope_shared"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(SqlFactoryCacheScopeProbeEntity),
                DbKey = IsolatedDataSource,
                MappingProfile = "cache-profile-isolated",
                TableName = "cache_scope_isolated"
            });
        });

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    public sealed class SqlFactoryCacheScopeProbeEntity : ISoftDelete
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public bool IsDeleted { get; set; }
    }
}
