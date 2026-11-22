using System.Collections.Concurrent;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Dapper.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>冻结 Fluent IR 的 SQLite Trace 集成测试。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteFrozenTraceIntegrationTest : IAsyncLifetime
{
    private const string VisibleSql = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`=@_p_0";
    private const string VisibleDebugSql = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`=4";
    private const string SensitiveSql = "Select Count(*) \r\nFrom samples \r\nWhere Name=@ApiToken";
    private const string SensitiveDebugSql = "Select Count(*) \r\nFrom samples \r\nWhere Name='<redacted>'";

    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>初始化冻结 Trace 集成测试。</summary>
    public SqliteFrozenTraceIntegrationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>同步与异步执行冻结 Fluent IR 时记录完整 SQL 和 Debug SQL。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrozenIr_WhenTraceIsEnabled_ShouldLogSqlAndDebugSql(bool asynchronous)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('visible', 4), ('hidden', 5)");

        using var loggerProvider = new TraceLoggerProvider();
        using var host = CreateHost(loggerProvider);
        using var root = host.GetRequiredService<ISqlQueryFactory>().Create();
        var executor = (ISqlQueryPlanExecutor)root;
        var builder = new SqliteBuilder().Select("Name").From("samples").Where("Amount", 4);
        var plan = SqlQueryPlan.Create(builder).Freeze();

        Assert.False(plan.IsBuilderPlan);
        var rows = asynchronous
            ? await executor.ToListAsync<string>(plan, null, default)
            : executor.ToList<string>(plan, null);

        Assert.Equal(new[] { "visible" }, rows);
        var log = Assert.Single(TraceMessages(loggerProvider));
        Assert.Equal(VisibleSql, log.Sql);
        Assert.Equal(VisibleDebugSql, log.DebugSql);
    }

    /// <summary>冻结 IR 的敏感值在日志中脱敏，但实际执行仍使用原参数值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrozenIr_WhenTraceContainsSensitiveParameter_ShouldRedactLogAndExecuteRealValue(
        bool asynchronous)
    {
        const string secret = "token-value-42";
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values (@ApiToken, 7)",
                new { ApiToken = secret });

        using var loggerProvider = new TraceLoggerProvider();
        using var host = CreateHost(loggerProvider);
        using var root = host.GetRequiredService<ISqlQueryFactory>().Create();
        var executor = (ISqlQueryPlanExecutor)root;
        var builder = new SqliteBuilder().AppendSelect("Count(*)").AppendFrom("samples")
            .AppendWhere("Name=@ApiToken").AddParam("ApiToken", secret);
        var plan = SqlQueryPlan.Create(builder).Freeze();

        var count = asynchronous
            ? await executor.ScalarAsync<int>(plan, null, default)
            : executor.Scalar<int>(plan, null);

        Assert.Equal(1, count);
        var log = Assert.Single(TraceMessages(loggerProvider));
        Assert.Equal(SensitiveSql, log.Sql);
        Assert.Equal(SensitiveDebugSql, log.DebugSql);
        Assert.Contains("@ApiToken : '<redacted>' : <redacted>", log.FormattedMessage);
        Assert.DoesNotContain(secret, log.FormattedMessage);
    }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    private ServiceProvider CreateHost(ILoggerProvider loggerProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.ClearProviders().SetMinimumLevel(LogLevel.Trace)
            .AddProvider(loggerProvider));
        services.AddSqlCore();
        services.AddSqlDataSource("default", DatabaseType.Sqlite, _fixture.FirstConnectionString);
        services.AddSqliteProvider();
        return services.BuildServiceProvider();
    }

    private static IReadOnlyList<TraceLogEntry> TraceMessages(TraceLoggerProvider provider) =>
        provider.Messages.Where(message => message.Sql != null).ToArray();

    private sealed class TraceLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<TraceLogEntry> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new TraceLogger(Messages);

        public void Dispose()
        {
        }
    }

    private sealed class TraceLogger(ConcurrentQueue<TraceLogEntry> messages) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Trace;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                var structuredState = state as IEnumerable<KeyValuePair<string, object>>;
                var sql = structuredState?.FirstOrDefault(item => item.Key == "Sql").Value as string;
                var debugSql = structuredState?.FirstOrDefault(item => item.Key == "DebugSql").Value as string;
                messages.Enqueue(new TraceLogEntry(sql, debugSql, formatter(state, exception)));
            }
        }
    }

    private sealed record TraceLogEntry(string Sql, string DebugSql, string FormattedMessage);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
