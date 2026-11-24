using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证 SQLite 对计算型 Returning 列的生成式执行和物化。</summary>
public sealed class SqliteComputedReturningTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Returning_WhenArithmeticProjectionIsExecuted_ShouldMaterializeTotal(bool asynchronous)
    {
        using var host = new TestHost();
        await using var transaction = await host.Services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = transaction.CreateExecutor();
        const int updatedCounter = 21;
        var command = executor.CreateUpdateBuilder().Update<ComputedRow>()
            .Set("Counter", updatedCounter).Where("Id", 3)
            .Returning<ComputedRow>(row => new { Total = (row.Id + row.Id) * row.Id })
            .BuildCommand();

        Assert.Equal("Update `computed_returning` Set `Counter` = @_p_0 Where `Id`=@_p_1 Returning ((`Id` + `Id`) * `Id`) As `Total`", command.Sql);
        Assert.Equal(new object[] { updatedCounter, 3 }, command.Parameters.Select(parameter => parameter.Value));

        var result = asynchronous
            ? Assert.Single(await executor.ExecuteReturningAsync<ComputedReturningResult>(command))
            : Assert.Single(executor.ExecuteReturning<ComputedReturningResult>(command));

        Assert.Equal(18, result.Total);
        await transaction.RollbackAsync();

        Assert.Equal(10, host.ReadCounter(3));
    }

    public sealed class ComputedRow
    {
        public int Id { get; set; }
        public int Counter { get; set; }
    }

    public sealed class ComputedReturningResult
    {
        public int Total { get; set; }
    }

    private static EntityModelMetadata CreateSourceModel() => new(typeof(ComputedRow), "computed_returning", null,
        new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(ComputedRow.Id), typeof(int), true, true, "Id",
                generatedGetter: static row => ((ComputedRow)row).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(ComputedRow.Counter), typeof(int), true, true, "Counter",
                generatedGetter: static row => ((ComputedRow)row).Counter)
        });

    private static EntityModelMetadata CreateResultModel() => new(typeof(ComputedReturningResult), "computed_returning", null,
        new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(ComputedReturningResult.Total), typeof(int), true, true, "Total",
                generatedSetter: static (row, value) => ((ComputedReturningResult)row).Total = (int)value)
        }, static () => new ComputedReturningResult());

    private sealed class TestHost : IDisposable
    {
        private readonly string _artifactDirectory;
        private readonly string _path;
        private readonly string _connectionString;

        internal ServiceProvider Services { get; }

        internal TestHost()
        {
            var workspaceRoot = GetWorkspaceRoot();
            _artifactDirectory = Path.Combine(workspaceRoot, "artifacts", "validation", "returning-math", "tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_artifactDirectory);
            _path = Path.Combine(_artifactDirectory, "computed-returning.db");
            _connectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlCore();
            services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
            services.AddSqlGeneratedModels(new[] { CreateResultModel(), CreateSourceModel() });
            services.AddSqlDataSource("default", DatabaseType.Sqlite, _connectionString);
            services.AddSqliteProvider();
            Services = services.BuildServiceProvider();

            using var executor = CreateExecutor();
            executor.ExecuteSql("CREATE TABLE computed_returning (Id INTEGER PRIMARY KEY, Counter INTEGER NOT NULL)");
            executor.ExecuteSql("INSERT INTO computed_returning (Id, Counter) VALUES (3, 10)");
        }

        private ISqlExecutor CreateExecutor() => Services.GetRequiredService<ISqlExecutorFactory>().Create();

        internal int ReadCounter(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Counter FROM computed_returning WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);
            return checked((int)(long)command.ExecuteScalar()!);
        }

        private static string GetWorkspaceRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
                directory = directory.Parent;
            return directory?.FullName ?? Directory.GetCurrentDirectory();
        }

        public void Dispose()
        {
            Services.Dispose();
            if (Directory.Exists(_artifactDirectory)) Directory.Delete(_artifactDirectory, recursive: true);
        }
    }
}
