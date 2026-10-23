using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Filters;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// SQL 工厂与 Scoped 过滤状态的集成契约。
/// </summary>
public sealed class SqlFactoryScopeContractTest
{
    private const string EnabledSql =
        "Select `scope_probe`.`Id`,`scope_probe`.`IsDeleted` \r\nFrom `scope_probe` \r\nWhere `scope_probe`.`IsDeleted`=@_p_0";

    private const string DisabledSql = "Select `scope_probe`.`Id`,`scope_probe`.`IsDeleted` \r\nFrom `scope_probe`";

    /// <summary>
    /// 测试 - 开启 scope 验证时，Query 首次渲染应从创建工厂的子作用域解析过滤器。
    /// </summary>
    [Fact]
    public void Create_WhenScopesAreValidated_ShouldRenderFromRequestScope()
    {
        using var root = CreateServiceProvider(validateScopes: true);
        using var scope = root.CreateScope();
        var filter = scope.ServiceProvider.GetRequiredService<IDataFilter>();

        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
        var source = query.From<SqlFactoryScopeProbeEntity>();

        Assert.Equal(EnabledSql, source.ToSql());
        using (filter.Disable<ISoftDelete>())
            Assert.Equal(DisabledSql, source.ToSql());
        Assert.Equal(EnabledSql, source.ToSql());
    }

    /// <summary>
    /// 测试 - Query 与 typed Builder 在过滤禁用时创建，恢复过滤后再次渲染应读取最新状态。
    /// </summary>
    [Fact]
    public void Query_WhenCreatedWhileFilterIsDisabled_ShouldRenderCurrentFilterState()
    {
        using var root = CreateServiceProvider(validateScopes: true);
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        var filter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
        ISqlQuery query;
        SqlLambdaQuery source;

        using (filter.Disable<ISoftDelete>())
        {
            query = factory.Create();
            source = query.From<SqlFactoryScopeProbeEntity>();
            Assert.Equal(DisabledSql, source.ToSql());
        }

        using (query)
            Assert.Equal(EnabledSql, source.ToSql());
    }

    /// <summary>
    /// 测试 - 多次启停软删除过滤器不应在 Builder 重渲染中累积谓词或参数。
    /// </summary>
    [Fact]
    public void Query_WhenRerenderedAcrossFilterCycles_ShouldNotDuplicatePredicatesOrParameters()
    {
        using var root = CreateServiceProvider(validateScopes: true);
        using var scope = root.CreateScope();
        var filter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
        var source = query.From<SqlFactoryScopeProbeEntity>();

        for (var cycle = 0; cycle < 3; cycle++)
        {
            Assert.Equal(EnabledSql, source.ToSql());
            var enabledSnapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(
                ((ISqlQueryBuilderAccessor)source).GetSqlBuilder());
            Assert.Equal(EnabledSql, enabledSnapshot.Sql);
            Assert.Equal("@_p_0", Assert.Single(enabledSnapshot.Parameters).Name);
            Assert.Equal(false, Assert.Single(enabledSnapshot.Parameters).Value);

            using (filter.Disable<ISoftDelete>())
            {
                Assert.Equal(DisabledSql, source.ToSql());
                var disabledSnapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(
                    ((ISqlQueryBuilderAccessor)source).GetSqlBuilder());
                Assert.Equal(DisabledSql, disabledSnapshot.Sql);
                Assert.Empty(disabledSnapshot.Parameters);
            }
        }
    }

    /// <summary>
    /// 测试 - 未开启 scope 验证时，请求作用域中的过滤器禁用状态也应影响完整 SQL。
    /// </summary>
    [Fact]
    public void Create_WhenScopesAreNotValidated_ShouldUseRequestScopeFilterState()
    {
        using var root = CreateServiceProvider(validateScopes: false);
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        var filter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
        using var query = factory.Create();
        var source = query.From<SqlFactoryScopeProbeEntity>();

        var enabled = source.ToSql();
        string disabled;
        using (filter.Disable<ISoftDelete>())
            disabled = source.ToSql();
        var restored = source.ToSql();

        Assert.Equal(EnabledSql, enabled);
        Assert.Equal(DisabledSql, disabled);
        Assert.Equal(EnabledSql, restored);
    }

    /// <summary>
    /// 测试 - 直接从请求作用域解析 transient runtime 时应沿用该作用域的核心工厂。
    /// </summary>
    [Fact]
    public void RuntimeServices_WhenResolvedFromRequestScope_ShouldCreateWithoutRootResolution()
    {
        using var root = CreateServiceProvider(validateScopes: true);
        using var scope = root.CreateScope();

        using var query = scope.ServiceProvider.GetRequiredService<ISqlQuery>();
        using var executor = scope.ServiceProvider.GetRequiredService<ISqlExecutor>();
        using var multiple = scope.ServiceProvider.GetRequiredService<ISqlMultipleQueryExecutor>();

        Assert.Equal(EnabledSql, query.From<SqlFactoryScopeProbeEntity>().ToSql());
    }

    /// <summary>
    /// 测试 - SQLite 工厂收到未配置的具名数据源时应明确失败，不能进入 SQL 执行路径。
    /// </summary>
    [Fact]
    public void QueryFactory_WhenNamedDataSourceIsMissing_ShouldThrowBeforeExecution()
    {
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=:memory:");
        var connectionResolver = new CountingConnectionResolver();
        services.AddSingleton<ISqlDbConnectionFactoryResolver>(connectionResolver);
        using var root = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Create("missing"));

        Assert.Contains("missing", exception.Message);
        Assert.Equal(0, connectionResolver.CallCount);
    }

    /// <summary>
    /// 测试 - 嵌套 Disable/Enable、异常退出及跨 await 均应恢复并保留作用域状态。
    /// </summary>
    [Fact]
    public async Task Filter_WhenNestedThrownOrAwaited_ShouldKeepScopeStateAndRestore()
    {
        using var root = CreateServiceProvider(validateScopes: true);
        using var scope = root.CreateScope();
        var filter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
        var source = query.From<SqlFactoryScopeProbeEntity>();

        using (filter.Disable<ISoftDelete>())
        {
            Assert.Equal(DisabledSql, source.ToSql());
            using (filter.Enable<ISoftDelete>())
                Assert.Equal(EnabledSql, source.ToSql());
            Assert.Equal(DisabledSql, source.ToSql());

            await Task.Yield();
            Assert.Equal(DisabledSql, source.ToSql());
        }

        Assert.Equal(EnabledSql, source.ToSql());
        Assert.Throws<InvalidOperationException>(() => DisableAndThrow(filter));
        Assert.Equal(EnabledSql, source.ToSql());
    }

    private static void DisableAndThrow(IDataFilter filter)
    {
        using (filter.Disable<ISoftDelete>())
            throw new InvalidOperationException("scope unwind");
    }

    /// <summary>
    /// 测试 - 并发独立作用域的过滤状态不得相互影响。
    /// </summary>
    [Fact]
    public async Task Filter_WhenScopesRenderConcurrently_ShouldRemainIsolated()
    {
        using var root = CreateServiceProvider(validateScopes: true);

        async Task<string> RenderAsync(bool disableFilter)
        {
            using var scope = root.CreateScope();
            var filter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
            using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
            var source = query.From<SqlFactoryScopeProbeEntity>();
            await Task.Yield();
            if (disableFilter)
            {
                using (filter.Disable<ISoftDelete>())
                {
                    await Task.Yield();
                    return source.ToSql();
                }
            }
            return source.ToSql();
        }

        var results = await Task.WhenAll(RenderAsync(true), RenderAsync(false));

        Assert.Equal(DisabledSql, results[0]);
        Assert.Equal(EnabledSql, results[1]);
    }

    /// <summary>
    /// 测试 - 已从 A 作用域解析的工厂在 B 作用域调用时仍使用 A 的过滤器实例。
    /// </summary>
    [Fact]
    public void Factory_WhenInvokedAcrossScopes_ShouldStayBoundToResolutionScope()
    {
        using var root = CreateServiceProvider(validateScopes: true);
        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        var factoryA = scopeA.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        var filterA = scopeA.ServiceProvider.GetRequiredService<IDataFilter>();
        var filterB = scopeB.ServiceProvider.GetRequiredService<IDataFilter>();
        using var queryA = factoryA.Create();
        using var queryB = scopeB.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
        var sourceA = queryA.From<SqlFactoryScopeProbeEntity>();
        var sourceB = queryB.From<SqlFactoryScopeProbeEntity>();

        using (filterB.Disable<ISoftDelete>())
        {
            Assert.Equal(EnabledSql, sourceA.ToSql());
            Assert.Equal(DisabledSql, sourceB.ToSql());
        }

        using (filterA.Disable<ISoftDelete>())
            Assert.Equal(DisabledSql, sourceA.ToSql());
        Assert.Equal(EnabledSql, sourceB.ToSql());
    }

    /// <summary>
    /// 测试 - SQLite 实际查询、Clone、分页及过滤器状态应只影响当前请求作用域。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Query_WhenFilterChangesInOneScope_ShouldReturnOnlyThatScopesRows(bool validateScopes)
    {
        var connectionString = $"Data Source=file:sql-factory-scope-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        using (var command = keeper.CreateCommand())
        {
            command.CommandText = "Create Table scope_probe_data (Id Integer Primary Key, Name Text Not Null, IsDeleted Integer Not Null); Insert Into scope_probe_data Values (1, 'active', 0), (2, 'deleted', 1);";
            command.ExecuteNonQuery();
        }

        using var root = CreateServiceProvider(validateScopes, connectionString);
        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        var factoryA = scopeA.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        var filterA = scopeA.ServiceProvider.GetRequiredService<IDataFilter>();
        using var queryA = factoryA.Create();
        using var queryB = scopeB.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
        var source = queryA.From<SqlFactoryScopeDataEntity>()
            .OrderBy<SqlFactoryScopeDataEntity>(item => new object[] { item.Id });
        var clone = source.Clone();

        Assert.Equal(new[] { "active" }, source.ToList<SqlFactoryScopeDataEntity>().Select(item => item.Name));
        Assert.Equal(1, clone.ToPage<SqlFactoryScopeDataEntity>(new Pager(1, 10)).TotalCount);
        using (filterA.Disable<ISoftDelete>())
        {
            Assert.Equal(new[] { "active", "deleted" }, source.ToList<SqlFactoryScopeDataEntity>().Select(item => item.Name));
            Assert.Equal(2, clone.ToPage<SqlFactoryScopeDataEntity>(new Pager(1, 10)).TotalCount);
            Assert.Equal(new[] { "active" }, queryB.From<SqlFactoryScopeDataEntity>().ToList<SqlFactoryScopeDataEntity>().Select(item => item.Name));
        }
        Assert.Equal(new[] { "active" }, source.ToList<SqlFactoryScopeDataEntity>().Select(item => item.Name));
        Assert.Equal(1, clone.ToPage<SqlFactoryScopeDataEntity>(new Pager(1, 10)).TotalCount);
    }

    /// <summary>
    /// 测试 - Executor 的软删除 Mutation 使用解析工厂作用域的过滤状态，另一作用域和原始 SQL 保持独立语义。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mutation_WhenFilterChangesInOneScope_ShouldKeepOtherScopeAndRawSqlIndependent(bool validateScopes)
    {
        var connectionString = $"Data Source=file:sql-factory-mutation-scope-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        using (var command = keeper.CreateCommand())
        {
            command.CommandText = "Create Table scope_probe_data (Id Integer Primary Key, Name Text Not Null, IsDeleted Integer Not Null); Insert Into scope_probe_data Values (1, 'active', 0), (2, 'deleted', 1);";
            command.ExecuteNonQuery();
        }

        using var root = CreateServiceProvider(validateScopes, connectionString);
        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        var filterA = scopeA.ServiceProvider.GetRequiredService<IDataFilter>();
        using var executorA = scopeA.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create();
        using var executorB = scopeB.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create();
        var deletedEntity = new SqlFactoryScopeDataEntity { Id = 2, Name = "deleted", IsDeleted = true };

        Assert.Equal(0, executorA.Delete(deletedEntity));
        using (filterA.Disable<ISoftDelete>())
            Assert.Equal(1, executorA.Delete(deletedEntity));
        Assert.Equal(0, executorB.Delete(deletedEntity));

        using var rawQuery = scopeB.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
        Assert.Equal(new[] { "active", "deleted" },
            rawQuery.Sql("Select Name From scope_probe_data Order By Id").ToList<string>());
    }

    private static ServiceProvider CreateServiceProvider(bool validateScopes,
        string connectionString = "Data Source=:memory:")
    {
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = validateScopes,
            ValidateOnBuild = true
        });
    }

    private sealed class CountingConnectionResolver : ISqlDbConnectionFactoryResolver
    {
        public int CallCount { get; private set; }

        public System.Data.IDbConnection Create(string providerKey, string connectionString)
        {
            CallCount++;
            throw new InvalidOperationException("The connection resolver should not be called.");
        }
    }

    [Table("scope_probe")]
    public sealed class SqlFactoryScopeProbeEntity : ISoftDelete
    {
        public int Id { get; set; }

        public bool IsDeleted { get; set; }
    }

    [Table("scope_probe_data")]
    public sealed class SqlFactoryScopeDataEntity : ISoftDelete
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public bool IsDeleted { get; set; }
    }
}
