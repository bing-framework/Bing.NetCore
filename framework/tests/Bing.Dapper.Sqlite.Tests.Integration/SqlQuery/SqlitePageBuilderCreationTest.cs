using System.Diagnostics;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证分页执行仅在原生 SQL 回退时创建 Provider Builder。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqlitePageBuilderCreationTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>初始化 SQLite 分页 Builder 创建计数测试。</summary>
    public SqlitePageBuilderCreationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>纯 IR 已能派生页面时，同步和异步分页都不创建执行器 Builder。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FrozenIrPage_ShouldNotCreateIndependentBuilder(bool asynchronous, bool totalCountKnown)
    {
        await SeedSamplesAsync();
        using var query = CreateCountingQuery();
        var frozen = SqlQueryPlan.Create(new SqliteBuilder().Select("Name").From("samples")
            .OrderBy("Name").Skip(1).Take(1)).Freeze();
        var plan = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        const string sourceSql =
            "Select `Name` \r\nFrom `samples` \r\nOrder By `Name` \r\nLimit @_p_1 OFFSET @_p_0";
        AssertPagePlan(plan, sourceSql, 1, 1);
        var sourceParameters = ParameterValues(plan);
        var messages = new List<DiagnosticsMessage>();
        using var observer = new SqliteDiagnosticObserver(messages.Add);

        var page = await ExecutePageAsync(query, plan, CreatePager(totalCountKnown), asynchronous);

        Assert.Equal(0, query.IndependentBuilderCreateCount);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { "two" }, page.Data);
        AssertPagePlan(plan, sourceSql, 1, 1);
        Assert.Equal(sourceParameters, ParameterValues(plan));
        AssertPageMessages(messages, totalCountKnown,
            "Select Count(*) \r\nFrom `samples`", sourceSql);
    }

    /// <summary>冻结计划已有规划源时，派生排序和分页复用该来源，不创建执行器 Builder。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FrozenPlanWithPlanningSource_ShouldNotCreateIndependentBuilder(bool asynchronous,
        bool totalCountKnown)
    {
        await SeedSamplesAsync();
        using var query = CreateCountingQuery();
        var plan = SqlQueryPlan.Create(new SqliteBuilder().Select("Name").From("samples")).Freeze();
        const string sourceSql = "Select `Name` \r\nFrom `samples`";
        Assert.Equal(sourceSql, plan.CommandText);
        var sourceParameters = ParameterValues(plan);
        var messages = new List<DiagnosticsMessage>();
        using var observer = new SqliteDiagnosticObserver(messages.Add);

        var page = await ExecutePageAsync(query, plan, CreatePager(totalCountKnown), asynchronous);

        Assert.Equal(0, query.IndependentBuilderCreateCount);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { "two" }, page.Data);
        Assert.Equal(sourceSql, plan.CommandText);
        Assert.Equal(sourceParameters, ParameterValues(plan));
        AssertPageMessages(messages, totalCountKnown,
            "Select Count(*) \r\nFrom `samples`",
            "Select `Name` \r\nFrom `samples` \r\nOrder By `Name` \r\nLimit @_p_1 OFFSET @_p_0");
    }

    /// <summary>原生 SQL 没有结构化规划源时仍创建一次 Provider Builder 并执行安全分页。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RawSqlPage_ShouldCreateOneIndependentBuilder(bool asynchronous, bool totalCountKnown)
    {
        await SeedSamplesAsync();
        using var query = CreateCountingQuery();
        const string sourceSql = "Select Name From samples";
        var plan = SqlQueryPlan.Create(sourceSql, null);
        var messages = new List<DiagnosticsMessage>();
        using var observer = new SqliteDiagnosticObserver(messages.Add);

        var page = await ExecutePageAsync(query, plan, CreatePager(totalCountKnown), asynchronous);

        Assert.Equal(1, query.IndependentBuilderCreateCount);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { "two" }, page.Data);
        Assert.Equal(sourceSql, plan.CommandText);
        Assert.Null(plan.Parameters);
        AssertPageMessages(messages, totalCountKnown,
            "Select Count(*) From (Select Name From samples) As __bing_raw_count",
            "Select * \r\nFrom (Select Name From samples) __bing_raw_page \r\nOrder By `Name` Limit @_p_1 OFFSET @_p_0");
    }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    private CountingSqliteQuery CreateCountingQuery() => new(_fixture.ServiceProvider,
        new SqlOptions<SqliteSqlQuery>
        {
            DatabaseType = DatabaseType.Sqlite,
            ConnectionString = _fixture.FirstConnectionString
        });

    private async Task SeedSamplesAsync()
    {
        using var writer = _fixture.CreateExecutor();
        await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");
    }

    private static Pager CreatePager(bool totalCountKnown) =>
        new(3, 1, totalCountKnown ? 3 : 0, "Name");

    private static Task<PagerList<string>> ExecutePageAsync(CountingSqliteQuery query, SqlQueryPlan plan,
        Pager pager, bool asynchronous)
    {
        var executor = (ISqlQueryPlanExecutor)query;
        return asynchronous
            ? executor.ToPageAsync<string>(plan, pager, null, CancellationToken.None)
            : Task.FromResult(executor.ToPage<string>(plan, pager, null));
    }

    private static void AssertPagePlan(SqlQueryPlan plan, string sql, int offset, int limit)
    {
        Assert.Equal(sql, plan.CommandText);
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { offset, limit }, parameters.Select(parameter => parameter.Value));
    }

    private static object[] ParameterValues(SqlQueryPlan plan) =>
        plan.Parameters is SqlParam[] parameters
            ? parameters.Select(parameter => parameter.Value).ToArray()
            : Array.Empty<object>();

    private static void AssertPageMessages(IReadOnlyList<DiagnosticsMessage> messages, bool totalCountKnown,
        string expectedCountSql, string expectedPageSql)
    {
        if (totalCountKnown)
        {
            var dataMessage = Assert.Single(messages);
            Assert.Equal("Data", dataMessage.Phase);
            Assert.Equal(expectedPageSql, dataMessage.Sql);
            return;
        }

        Assert.Collection(messages,
            countMessage =>
            {
                Assert.Equal("Count", countMessage.Phase);
                Assert.Equal(expectedCountSql, countMessage.Sql);
            },
            dataMessage =>
            {
                Assert.Equal("Data", dataMessage.Phase);
                Assert.Equal(expectedPageSql, dataMessage.Sql);
            });
    }

    private sealed class CountingSqliteQuery : SqliteSqlQuery
    {
        internal int IndependentBuilderCreateCount { get; private set; }

        internal CountingSqliteQuery(IServiceProvider serviceProvider, SqlOptions<SqliteSqlQuery> options)
            : base(serviceProvider, options)
        {
        }

        protected override ISqlBuilder CreateIndependentSqlBuilder()
        {
            IndependentBuilderCreateCount++;
            return base.CreateIndependentSqlBuilder();
        }
    }

    private sealed class SqliteDiagnosticObserver : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object>>, IDisposable
    {
        private readonly Action<DiagnosticsMessage> _onMessage;
        private readonly IDisposable _allSubscription;
        private IDisposable _listenerSubscription;

        internal SqliteDiagnosticObserver(Action<DiagnosticsMessage> onMessage)
        {
            _onMessage = onMessage;
            _allSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == SqlQueryDiagnosticListenerNames.DiagnosticListenerName)
                _listenerSubscription = listener.Subscribe(this);
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key == SqlQueryDiagnosticListenerNames.BeforeExecute && value.Value is DiagnosticsMessage message)
                _onMessage(message);
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void Dispose()
        {
            _listenerSubscription?.Dispose();
            _allSubscription.Dispose();
        }
    }
}
