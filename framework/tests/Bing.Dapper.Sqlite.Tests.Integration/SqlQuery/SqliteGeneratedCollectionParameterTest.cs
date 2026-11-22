using System.Data;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Dapper;
using Bing.Data.Sql.Diagnostics;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>登记模型不能改变 Raw SQL 列表展开；路径在发送命令前决定。</summary>
public sealed class SqliteGeneratedCollectionParameterTest
{
    [Theory]
    [InlineData(SqlExecutionMode.Compatible, 0)]
    [InlineData(SqlExecutionMode.Compatible, 1)]
    [InlineData(SqlExecutionMode.Compatible, 3)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, 0)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, 1)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, 3)]
    public async Task RegisteredModel_ShouldExpandListsWithDapperAndExecuteOnce(SqlExecutionMode mode, int count)
    {
        using var host = Host(mode);
        var source = new Filter { Ids = Enumerable.Range(1, count).ToArray() };
        var bound = host.GetRequiredService<ISqlParameterBinder>().Bind(source);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM (SELECT 1 AS Id UNION ALL SELECT 2 UNION ALL SELECT 3) WHERE Id IN @Ids";
        Assert.IsAssignableFrom<SqlMapper.IDynamicParameters>(bound).AddParameters(command, null);

        var replacement = count == 0 ? "(SELECT @Ids WHERE 1 = 0)" :
            "(" + string.Join(",", Enumerable.Range(1, count).Select(index => "@Ids" + index)) + ")";
        Assert.Equal("SELECT COUNT(*) FROM (SELECT 1 AS Id UNION ALL SELECT 2 UNION ALL SELECT 3) WHERE Id IN " + replacement,
            command.CommandText);
        Assert.Equal((count == 0 ? new object[] { DBNull.Value } : source.Ids.Cast<object>()).Append(DBNull.Value),
            command.Parameters.Cast<SqliteParameter>().Select(parameter => parameter.Value));
        await connection.OpenAsync();
        Assert.Equal((long)count, await command.ExecuteScalarAsync());

        using var query = host.GetRequiredService<ISqlQueryFactory>().Create();
        Assert.Equal(count, await query.Sql("SELECT COUNT(*) FROM (SELECT 1 AS Id UNION ALL SELECT 2 UNION ALL SELECT 3) WHERE Id IN @Ids", source).SingleAsync<int>());
    }

    [Fact]
    public void StrictModel_ShouldRejectExpansionBeforeOpeningConnection()
    {
        using var host = Host(SqlExecutionMode.GeneratedOnly);
        var exception = Assert.Throws<NotSupportedException>(() =>
            host.GetRequiredService<ISqlParameterBinder>().Bind(new Filter { Ids = new[] { 1, 2 } }));
        Assert.Contains("Ids", exception.Message);
        Assert.Contains("列表展开", exception.Message);
    }

    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedPreferred)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void BinaryAndNull_ShouldRemainSingleValues(SqlExecutionMode mode)
    {
        using var host = Host(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT length(@Data), @Ids";
        var bound = host.GetRequiredService<ISqlParameterBinder>().Bind(new Filter { Data = new byte[] { 1, 2 }, Ids = null });
        Assert.IsAssignableFrom<SqlMapper.IDynamicParameters>(bound).AddParameters(command, null);
        Assert.Equal("SELECT length(@Data), @Ids", command.CommandText);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal(DBNull.Value, command.Parameters["Ids"].Value);
        Assert.Equal(new byte[] { 1, 2 }, command.Parameters["Data"].Value);
        connection.Open();
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.True(reader.IsDBNull(1));
    }

    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedPreferred)]
    public async Task ProviderParameterLimit_ShouldFailOnceWithoutResending(SqlExecutionMode mode)
    {
        using var host = Host(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var query = new RecordingQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var source = new Filter { Ids = Enumerable.Range(1, 32767).ToArray() };

        var exception = await Assert.ThrowsAsync<SqliteException>(() =>
            query.Sql("SELECT 1 WHERE 1 IN @Ids", source).SingleAsync<int>());

        Assert.Equal(1, exception.SqliteErrorCode);
        Assert.Contains("too many SQL variables", exception.Message);
        Assert.Equal(1, query.BeforeCount);
        Assert.Equal(1, query.ErrorCount);
        Assert.Equal(9, query.Sql("SELECT 9").Single<int>());
    }

    private sealed class RecordingQuery : SqliteSqlQuery
    {
        public RecordingQuery(IServiceProvider services, SqlOptions<SqliteSqlQuery> options) : base(services, options) { }
        public int BeforeCount { get; private set; }
        public int ErrorCount { get; private set; }
        protected override bool ExecuteBefore() { BeforeCount++; return base.ExecuteBefore(); }
        protected override void ExecuteError(DiagnosticsMessage message, Exception exception) => ErrorCount++;
    }

    private static ServiceProvider Host(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        services.AddSqlGeneratedModels(new[] { new EntityModelMetadata(typeof(Filter), "Filter", "", new[]
        {
            EntityPropertyMetadata.CreateGenerated("Ids", typeof(int[]), true, false, generatedGetter: source => ((Filter)source).Ids),
            EntityPropertyMetadata.CreateGenerated("Data", typeof(byte[]), true, false, generatedGetter: source => ((Filter)source).Data)
        }) });
        services.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=:memory:");
        services.AddSqliteProvider();
        return services.BuildServiceProvider();
    }

    public sealed class Filter
    {
        public int[] Ids { get; set; }
        public byte[] Data { get; set; }
    }
}
