using Bing.Data.Enums;
using Bing.Data.Filters;
using Bing.Data.Sql;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// SQL 事务子对象与请求作用域过滤器的集成契约。
/// </summary>
public sealed class SqlTransactionScopeFilterContractTest
{
    private const string EnabledSql =
        "Select `scope_probe_data`.`Id`,`scope_probe_data`.`Name`,`scope_probe_data`.`IsDeleted` \r\nFrom `scope_probe_data` \r\nWhere `scope_probe_data`.`IsDeleted`=@_p_0";

    private const string DisabledSql =
        "Select `scope_probe_data`.`Id`,`scope_probe_data`.`Name`,`scope_probe_data`.`IsDeleted` \r\nFrom `scope_probe_data`";

    /// <summary>
    /// 测试目的：同步事务创建的 Query 和实体 Executor 应保留事务上下文并读取所属作用域过滤状态。
    /// </summary>
    [Fact]
    public void Begin_WhenScopesAreValidated_ShouldKeepTransactionChildrenAndFiltersInOwnerScope()
    {
        var firstConnectionString = CreateSharedDatabase("transaction-filter-first");
        var secondConnectionString = CreateSharedDatabase("transaction-filter-second");
        using var firstKeeper = OpenKeeper(firstConnectionString);
        using var secondKeeper = OpenKeeper(secondConnectionString);
        Seed(firstKeeper, "first");
        Seed(secondKeeper, "second");

        using var root = CreateServiceProvider(firstConnectionString, secondConnectionString);
        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        var filterA = scopeA.ServiceProvider.GetRequiredService<IDataFilter>();
        var filterB = scopeB.ServiceProvider.GetRequiredService<IDataFilter>();
        var databaseScopes = scopeA.ServiceProvider.GetRequiredService<IDatabaseScopeManager>();
        using var transaction = scopeA.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>().Begin("first");

        Assert.Equal("first", transaction.DbKey);
        using (databaseScopes.Use("second"))
        using (var query = transaction.CreateQuery())
        using (var executor = transaction.CreateExecutor())
        using (var queryB = scopeB.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("second"))
        {
            var sourceA = query.From<SqlFactoryScopeDataEntity>();
            var sourceB = queryB.From<SqlFactoryScopeDataEntity>();
            AssertSql(sourceA, enabled: true);
            AssertSql(sourceB, enabled: true);
            Assert.Equal(new[] { "first-active" }, sourceA.ToList<SqlFactoryScopeDataEntity>()
                .Select(item => item.Name));
            Assert.Equal(new[] { "second-active" }, sourceB.ToList<SqlFactoryScopeDataEntity>()
                .Select(item => item.Name));

            Assert.Equal(0, executor.Delete(new SqlFactoryScopeDataEntity
            {
                Id = 2,
                Name = "first-deleted",
                IsDeleted = true
            }));

            using (filterA.Disable<ISoftDelete>())
            {
                AssertSqlText(sourceA, enabled: false);
                AssertSqlText(sourceB, enabled: true);
                Assert.Equal(1, executor.Delete(new SqlFactoryScopeDataEntity
                {
                    Id = 2,
                    Name = "first-deleted",
                    IsDeleted = true
                }));
            }

            AssertSqlText(sourceA, enabled: true);
            AssertSqlText(sourceB, enabled: true);
            Assert.Same(filterA, scopeA.ServiceProvider.GetRequiredService<IDataFilter>());
            Assert.NotSame(filterA, filterB);
        }

        transaction.Rollback();

        using var queryAfterRollback = scopeB.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("first");
        var sourceAfterRollback = queryAfterRollback.From<SqlFactoryScopeDataEntity>();
        Assert.Equal(new[] { "first-active" }, sourceAfterRollback.ToList<SqlFactoryScopeDataEntity>()
            .Select(item => item.Name));
        using (filterB.Disable<ISoftDelete>())
        {
            Assert.Equal(new[] { "first-active", "first-deleted" },
                sourceAfterRollback.ToList<SqlFactoryScopeDataEntity>().Select(item => item.Name));
            using var querySecond = scopeB.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("second");
            Assert.Equal(new[] { "second-active", "second-deleted" },
                querySecond.From<SqlFactoryScopeDataEntity>().ToList<SqlFactoryScopeDataEntity>()
                    .Select(item => item.Name));
        }
        Assert.Equal(new[] { "first-active" }, sourceAfterRollback.ToList<SqlFactoryScopeDataEntity>()
            .Select(item => item.Name));
    }

    /// <summary>
    /// 测试目的：异步事务创建的 Query 和实体 Executor 应保留事务上下文并读取所属作用域过滤状态。
    /// </summary>
    [Fact]
    public async Task BeginAsync_WhenScopesAreValidated_ShouldKeepTransactionChildrenAndFiltersInOwnerScope()
    {
        var firstConnectionString = CreateSharedDatabase("transaction-async-filter-first");
        var secondConnectionString = CreateSharedDatabase("transaction-async-filter-second");
        using var firstKeeper = OpenKeeper(firstConnectionString);
        using var secondKeeper = OpenKeeper(secondConnectionString);
        Seed(firstKeeper, "first");
        Seed(secondKeeper, "second");

        using var root = CreateServiceProvider(firstConnectionString, secondConnectionString);
        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        var filterA = scopeA.ServiceProvider.GetRequiredService<IDataFilter>();
        var filterB = scopeB.ServiceProvider.GetRequiredService<IDataFilter>();
        var databaseScopes = scopeA.ServiceProvider.GetRequiredService<IDatabaseScopeManager>();
        await using var transaction = await scopeA.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>()
            .BeginAsync("first");

        Assert.Equal("first", transaction.DbKey);
        using (databaseScopes.Use("second"))
        using (var query = transaction.CreateQuery())
        using (var executor = transaction.CreateExecutor())
        using (var queryB = scopeB.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("second"))
        {
            var sourceA = query.From<SqlFactoryScopeDataEntity>();
            var sourceB = queryB.From<SqlFactoryScopeDataEntity>();
            AssertSql(sourceA, enabled: true);
            AssertSql(sourceB, enabled: true);
            Assert.Equal(new[] { "first-active" },
                (await sourceA.ToListAsync<SqlFactoryScopeDataEntity>()).Select(item => item.Name));
            Assert.Equal(new[] { "second-active" },
                (await sourceB.ToListAsync<SqlFactoryScopeDataEntity>()).Select(item => item.Name));

            Assert.Equal(0, await executor.DeleteAsync(new SqlFactoryScopeDataEntity
            {
                Id = 2,
                Name = "first-deleted",
                IsDeleted = true
            }));

            using (filterA.Disable<ISoftDelete>())
            {
                AssertSqlText(sourceA, enabled: false);
                AssertSqlText(sourceB, enabled: true);
                Assert.Equal(1, await executor.DeleteAsync(new SqlFactoryScopeDataEntity
                {
                    Id = 2,
                    Name = "first-deleted",
                    IsDeleted = true
                }));
            }

            AssertSqlText(sourceA, enabled: true);
            AssertSqlText(sourceB, enabled: true);
            Assert.Same(filterA, scopeA.ServiceProvider.GetRequiredService<IDataFilter>());
            Assert.NotSame(filterA, filterB);
        }

        await transaction.RollbackAsync();

        using var queryAfterRollback = scopeB.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("first");
        var sourceAfterRollback = queryAfterRollback.From<SqlFactoryScopeDataEntity>();
        Assert.Equal(new[] { "first-active" }, (await sourceAfterRollback.ToListAsync<SqlFactoryScopeDataEntity>())
            .Select(item => item.Name));
        using (filterB.Disable<ISoftDelete>())
        {
            Assert.Equal(new[] { "first-active", "first-deleted" },
                (await sourceAfterRollback.ToListAsync<SqlFactoryScopeDataEntity>()).Select(item => item.Name));
            using var querySecond = scopeB.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("second");
            Assert.Equal(new[] { "second-active", "second-deleted" },
                (await querySecond.From<SqlFactoryScopeDataEntity>()
                    .ToListAsync<SqlFactoryScopeDataEntity>()).Select(item => item.Name));
        }
        Assert.Equal(new[] { "first-active" }, (await sourceAfterRollback.ToListAsync<SqlFactoryScopeDataEntity>())
            .Select(item => item.Name));
    }

    private static void AssertSql(SqlLambdaQuery source, bool enabled)
    {
        var expectedSql = enabled ? EnabledSql : DisabledSql;
        Assert.Equal(expectedSql, source.ToSql());
        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(
            ((ISqlQueryBuilderAccessor)source).GetSqlBuilder());
        Assert.Equal(expectedSql, snapshot.Sql);
        if (enabled)
        {
            var parameter = Assert.Single(snapshot.Parameters);
            Assert.Equal("@_p_0", parameter.Name);
            Assert.Equal(false, parameter.Value);
        }
        else
        {
            Assert.Empty(snapshot.Parameters);
        }
    }

    private static void AssertSqlText(SqlLambdaQuery source, bool enabled) =>
        Assert.Equal(enabled ? EnabledSql : DisabledSql, source.ToSql());

    private static ServiceProvider CreateServiceProvider(string firstConnectionString,
        string secondConnectionString)
    {
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("first", DatabaseType.Sqlite, firstConnectionString);
        services.AddSqlDataSource("second", DatabaseType.Sqlite, secondConnectionString);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private static string CreateSharedDatabase(string name) =>
        $"Data Source=file:{name}-{Guid.NewGuid():N}?mode=memory&cache=shared";

    private static SqliteConnection OpenKeeper(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static void Seed(SqliteConnection connection, string dataSourceName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "Create Table scope_probe_data (Id Integer Primary Key, Name Text Not Null, IsDeleted Integer Not Null); " +
                              "Insert Into scope_probe_data Values (1, @active, 0), (2, @deleted, 1);";
        command.Parameters.AddWithValue("@active", $"{dataSourceName}-active");
        command.Parameters.AddWithValue("@deleted", $"{dataSourceName}-deleted");
        command.ExecuteNonQuery();
    }

    [Table("scope_probe_data")]
    public sealed class SqlFactoryScopeDataEntity : ISoftDelete
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public bool IsDeleted { get; set; }
    }
}
