using System.Collections.Concurrent;
using Bing.Data.Sql.Diagnostics;
using Bing.Dapper.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证独立查询/写入资源与显式共享事务的 SQLite 行为。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteResourceIsolationTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    public SqliteResourceIsolationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>普通无事务查询失败并释放后，同一依赖注入作用域的执行器仍能写入。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryFailureAndDispose_ShouldNotPreventExecutorExecution(bool asynchronous)
    {
        await using var serviceScope = _fixture.ServiceProvider.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        var messages = new ConcurrentQueue<DiagnosticsMessage>();
        using var observer = new SqliteDiagnosticObserver(messages.Enqueue);
        using var executor = services.GetRequiredService<ISqlExecutorFactory>().Create("first");

        const string querySql = "Select 271828 As Value";
        var query = services.GetRequiredService<ISqlQueryFactory>().Create("first");
        if (asynchronous)
        {
            Assert.Equal(271828, await query.Sql(querySql).SingleAsync<int>());
            await Assert.ThrowsAsync<SqliteException>(() =>
                query.Sql("Select Name From missing_resource_isolation_table").SingleAsync<string>());
        }
        else
        {
            Assert.Equal(271828, query.Sql(querySql).Single<int>());
            Assert.Throws<SqliteException>(() =>
                query.Sql("Select Name From missing_resource_isolation_table").Single<string>());
        }
        query.Dispose();

        var execution = Assert.Single(messages, message => message.Sql == querySql);
        Assert.NotNull(execution.Transaction);
        Assert.False(execution.Transaction.HasTransaction);

        const string name = "executor-after-query-failure";
        const string insertSql = "Insert Into samples(Name) Values (@name)";
        var affected = asynchronous
            ? await executor.ExecuteSqlAsync(insertSql, new { name })
            : executor.ExecuteSql(insertSql, new { name });

        Assert.Equal(1, affected);
        Assert.Equal(new[] { name }, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>执行器释放后查询仍能读取共享事务中的未提交数据，回滚后数据不落库。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedTransaction_WhenExecutorIsDisposed_ShouldKeepQueryUsableUntilRollback(bool asynchronous)
    {
        await using var serviceScope = _fixture.ServiceProvider.CreateAsyncScope();
        var transactionFactory = serviceScope.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();
        await using var transaction = asynchronous
            ? await transactionFactory.BeginAsync("first")
            : transactionFactory.Begin("first");

        var query = transaction.CreateQuery();
        try
        {
            using (var executor = transaction.CreateExecutor())
            {
                const string insertSql = "Insert Into samples(Name) Values (@name)";
                const string countSql = "Select Count(*) From samples Where Name = @name";
                const string name = "query-after-executor-dispose";
                var affected = asynchronous
                    ? await executor.ExecuteSqlAsync(insertSql, new { name })
                    : executor.ExecuteSql(insertSql, new { name });
                Assert.Equal(1, affected);

                var count = asynchronous
                    ? await query.Sql(countSql, new { name }).SingleAsync<int>()
                    : query.Sql(countSql, new { name }).Single<int>();
                Assert.Equal(1, count);
            }

            const string selectSql = "Select Name From samples Where Name = @name";
            var visibleName = asynchronous
                ? await query.Sql(selectSql, new { name = "query-after-executor-dispose" }).SingleAsync<string>()
                : query.Sql(selectSql, new { name = "query-after-executor-dispose" }).Single<string>();
            Assert.Equal("query-after-executor-dispose", visibleName);

            if (asynchronous)
                await transaction.RollbackAsync();
            else
                transaction.Rollback();
        }
        finally
        {
            query.Dispose();
        }

        Assert.Equal(0, await _fixture.CountAsync("first"));
        Assert.Empty(await _fixture.ReadNamesAsync("first"));
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

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
