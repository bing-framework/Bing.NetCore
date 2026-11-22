using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>结果数量验证在成功通知之前执行，失败后租约可以再次使用。</summary>
public sealed class SqliteGeneratedTerminalHookTest
{
    public static IEnumerable<object[]> Cases() =>
        from mode in new[] { SqlExecutionMode.GeneratedOnly, SqlExecutionMode.GeneratedPreferred }
        from async in new[] { false, true }
        from terminal in new[] { "First", "FirstOrDefault", "Single", "SingleOrDefault" }
        from count in new[] { 0, 1, 2 }
        select new object[] { mode, async, terminal, count };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Cardinality_ShouldReportFailureBeforeSuccessAndReleaseLease(SqlExecutionMode mode,
        bool asynchronous, string terminal, int count)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var query = new RecordingQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var text = count switch
        {
            0 => "SELECT 1 WHERE 0=1",
            1 => "SELECT 1",
            _ => "SELECT 1 UNION ALL SELECT 2"
        };
        // 未登记匿名参数迫使 GeneratedPreferred 在发送前选择兼容终结方法。
        var command = query.Sql(text, mode == SqlExecutionMode.GeneratedPreferred ? new { Unused = 7 } : null);
        var shouldFail = count == 0 && terminal is "First" or "Single" ||
            count == 2 && terminal is "Single" or "SingleOrDefault";
        async Task<int> Run() => asynchronous ? terminal switch
        {
            "First" => await command.FirstAsync<int>(),
            "FirstOrDefault" => await command.FirstOrDefaultAsync<int>(),
            "Single" => await command.SingleAsync<int>(),
            _ => await command.ToEntityAsync<int>()
        } : terminal switch
        {
            "First" => command.First<int>(),
            "FirstOrDefault" => command.FirstOrDefault<int>(),
            "Single" => command.Single<int>(),
            _ => command.ToEntity<int>()
        };

        if (shouldFail)
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(Run);
            Assert.Same(exception, Assert.Single(query.Errors));
            Assert.Equal(0, query.SuccessCount);
        }
        else
        {
            Assert.Equal(count == 0 ? 0 : 1, await Run());
            Assert.Empty(query.Errors);
            Assert.Equal(1, query.SuccessCount);
        }
        Assert.Equal(1, query.BeforeCount);
        Assert.Equal(1, query.CleanupCount);
        // 失败路径也归还 Root 租约，下一次查询不能误报并发执行。
        Assert.Equal(9, query.Sql("SELECT 9").Single<int>());
        Assert.Equal(2, query.BeforeCount);
    }

    private sealed class RecordingQuery : SqliteSqlQuery
    {
        public RecordingQuery(IServiceProvider provider, SqlOptions<SqliteSqlQuery> options) : base(provider, options) { }
        public int BeforeCount { get; private set; }
        public int SuccessCount { get; private set; }
        public int CleanupCount { get; private set; }
        public List<Exception> Errors { get; } = new();
        protected override bool ExecuteBefore() { BeforeCount++; return base.ExecuteBefore(); }
        protected override void ExecuteAfter(DiagnosticsMessage message) => SuccessCount++;
        protected override void ExecuteAfter(object result) { CleanupCount++; base.ExecuteAfter(result); }
        protected override void ExecuteError(DiagnosticsMessage message, Exception exception) => Errors.Add(exception);
    }
}
