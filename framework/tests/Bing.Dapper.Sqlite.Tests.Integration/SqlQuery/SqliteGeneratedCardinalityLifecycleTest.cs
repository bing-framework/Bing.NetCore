using System.Collections.Generic;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Enums;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 验证生成式查询的终结操作在查询生命周期内执行基数校验。
/// </summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteGeneratedCardinalityLifecycleTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个 <see cref="SqliteGeneratedCardinalityLifecycleTest" /> 类型的实例。
    /// </summary>
    /// <param name="fixture">SQLite 集成测试数据库夹具。</param>
    public SqliteGeneratedCardinalityLifecycleTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 验证生成式同步和异步终结操作对 0、1、2 行结果遵循各自的基数规则。
    /// </summary>
    [Theory]
    [InlineData(false, "SELECT 1 WHERE 0", "First", 0, true)]
    [InlineData(false, "SELECT 1 WHERE 0", "FirstOrDefault", 0, false)]
    [InlineData(false, "SELECT 1 WHERE 0", "Single", 0, true)]
    [InlineData(false, "SELECT 1 WHERE 0", "SingleOrDefault", 0, false)]
    [InlineData(false, "SELECT 1", "First", 1, false)]
    [InlineData(false, "SELECT 1", "FirstOrDefault", 1, false)]
    [InlineData(false, "SELECT 1", "Single", 1, false)]
    [InlineData(false, "SELECT 1", "SingleOrDefault", 1, false)]
    [InlineData(false, "SELECT 1 UNION ALL SELECT 2", "First", 1, false)]
    [InlineData(false, "SELECT 1 UNION ALL SELECT 2", "FirstOrDefault", 1, false)]
    [InlineData(false, "SELECT 1 UNION ALL SELECT 2", "Single", 0, true)]
    [InlineData(false, "SELECT 1 UNION ALL SELECT 2", "SingleOrDefault", 0, true)]
    [InlineData(true, "SELECT 1 WHERE 0", "First", 0, true)]
    [InlineData(true, "SELECT 1 WHERE 0", "FirstOrDefault", 0, false)]
    [InlineData(true, "SELECT 1 WHERE 0", "Single", 0, true)]
    [InlineData(true, "SELECT 1 WHERE 0", "SingleOrDefault", 0, false)]
    [InlineData(true, "SELECT 1", "First", 1, false)]
    [InlineData(true, "SELECT 1", "FirstOrDefault", 1, false)]
    [InlineData(true, "SELECT 1", "Single", 1, false)]
    [InlineData(true, "SELECT 1", "SingleOrDefault", 1, false)]
    [InlineData(true, "SELECT 1 UNION ALL SELECT 2", "First", 1, false)]
    [InlineData(true, "SELECT 1 UNION ALL SELECT 2", "FirstOrDefault", 1, false)]
    [InlineData(true, "SELECT 1 UNION ALL SELECT 2", "Single", 0, true)]
    [InlineData(true, "SELECT 1 UNION ALL SELECT 2", "SingleOrDefault", 0, true)]
    public async Task GeneratedOnly_Terminals_ShouldHonorResultCardinality(bool asynchronous, string sql,
        string terminal, int expected, bool throws)
    {
        await using var provider = CreateProvider(SqlExecutionMode.GeneratedOnly);
        using var scope = provider.CreateScope();
        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("first");
        var command = query.Sql(sql);

        if (asynchronous)
        {
            if (throws)
                await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(command, terminal));
            else
                Assert.Equal(expected, await ExecuteAsync(command, terminal));
        }
        else if (throws)
        {
            Assert.Throws<InvalidOperationException>(() => Execute(command, terminal));
        }
        else
        {
            Assert.Equal(expected, Execute(command, terminal));
        }
    }

    /// <summary>
    /// 验证静态读取及 GeneratedPreferred 参数回退的基数异常都会使共享工作单元仅回滚。
    /// </summary>
    [Theory]
    [InlineData(false, SqlExecutionMode.GeneratedOnly)]
    [InlineData(true, SqlExecutionMode.GeneratedOnly)]
    [InlineData(false, SqlExecutionMode.GeneratedPreferred)]
    [InlineData(true, SqlExecutionMode.GeneratedPreferred)]
    public async Task DuplicateSingle_ShouldMarkUnitOfWorkRollbackOnly(bool asynchronous, SqlExecutionMode mode)
    {
        await using var provider = CreateProvider(mode);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var unitOfWork = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        var name = $"cardinality-{Guid.NewGuid():N}";
        await executor.ExecuteSqlAsync("INSERT INTO samples(Name) VALUES (@name)", new Dictionary<string, object>
        {
            ["name"] = name
        });

        var command = mode == SqlExecutionMode.GeneratedPreferred
            ? query.Sql("SELECT @Value UNION ALL SELECT @Value", new { Value = 1 })
            : query.Sql("SELECT 1 UNION ALL SELECT 2");

        if (asynchronous)
            await Assert.ThrowsAsync<InvalidOperationException>(() => command.SingleAsync<int>());
        else
            Assert.Throws<InvalidOperationException>(() => command.Single<int>());

        Assert.True(unitOfWork.IsRollbackOnly);
        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.CompleteAsync());
        Assert.DoesNotContain(name, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>枚举范围错误只能执行一次，并在同步/异步生成式读取中使共享事务仅回滚。</summary>
    [Theory]
    [InlineData(false, SqlExecutionMode.GeneratedOnly, 256)]
    [InlineData(true, SqlExecutionMode.GeneratedOnly, -1)]
    [InlineData(false, SqlExecutionMode.GeneratedPreferred, -1)]
    [InlineData(true, SqlExecutionMode.GeneratedPreferred, 256)]
    public async Task EnumOverflow_ShouldMarkUnitOfWorkRollbackOnly(bool asynchronous, SqlExecutionMode mode, int value)
    {
        await using var provider = CreateProvider(mode);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var unitOfWork = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        var name = $"enum-overflow-{Guid.NewGuid():N}";
        await executor.ExecuteSqlAsync("INSERT INTO samples(Name) VALUES (@name)", new Dictionary<string, object> { ["name"] = name });
        var command = query.Sql(value == 256 ? "SELECT 256" : "SELECT -1");
        if (asynchronous) await Assert.ThrowsAsync<OverflowException>(() => command.SingleAsync<ByteState>());
        else Assert.Throws<OverflowException>(() => command.Single<ByteState>());
        Assert.True(unitOfWork.IsRollbackOnly);
        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.CompleteAsync());
        Assert.DoesNotContain(name, await _fixture.ReadNamesAsync("first"));
    }

    public enum ByteState : byte { First = 1 }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    private static int Execute(SqlTextQuery command, string terminal) => terminal switch
    {
        "First" => command.First<int>(),
        "FirstOrDefault" => command.FirstOrDefault<int>(),
        "Single" => command.Single<int>(),
        "SingleOrDefault" => command.ToEntity<int>(),
        _ => throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null)
    };

    private static Task<int> ExecuteAsync(SqlTextQuery command, string terminal) => terminal switch
    {
        "First" => command.FirstAsync<int>(),
        "FirstOrDefault" => command.FirstOrDefaultAsync<int>(),
        "Single" => command.SingleAsync<int>(),
        "SingleOrDefault" => command.ToEntityAsync<int>(),
        _ => throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null)
    };

    private ServiceProvider CreateProvider(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        services.AddSqlDataSource("first", DatabaseType.Sqlite, _fixture.FirstConnectionString);
        services.AddSqliteProvider();
        services.AddUnitOfWorkScopes();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
