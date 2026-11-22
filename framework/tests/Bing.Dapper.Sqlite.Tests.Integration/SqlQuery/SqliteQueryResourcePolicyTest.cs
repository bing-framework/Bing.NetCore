using System.Collections.Concurrent;
using Bing.Data.Sql.Diagnostics;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 验证 SQLite 查询资源对 Primary 短事务策略的执行边界。
/// </summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteQueryResourcePolicyTest : IAsyncLifetime
{
    private const string DataSourceKey = "query-resource-policy";

    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个 <see cref="SqliteQueryResourcePolicyTest"/> 实例。
    /// </summary>
    /// <param name="fixture">SQLite 集成测试数据库固定装置。</param>
    public SqliteQueryResourcePolicyTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Primary 短事务查询可重复执行、分页，并在 SQL 失败后继续复用。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrimaryReadTransaction_RepeatedQueryAndPage_ShouldRecoverAfterFailure(bool asynchronous)
    {
        using var provider = CreateProvider(PrimaryReadStrategy.Transaction);
        using var scope = provider.CreateScope();
        using var readPreference = scope.ServiceProvider
            .GetRequiredService<IReadPreferenceScopeManager>()
            .Use(SqlReadPreference.Primary);
        var marker = $"primary-read-{Guid.NewGuid():N}";
        var missingTable = $"missing_{Guid.NewGuid():N}";
        var messages = new ConcurrentQueue<DiagnosticsMessage>();
        using var observer = new SqliteDiagnosticObserver(messages.Enqueue);
        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create(DataSourceKey);
        var command = query.Sql($"Select '{marker}' As Name");

        Assert.Equal(marker, asynchronous ? await command.SingleAsync<string>() : command.Single<string>());
        Assert.Equal(marker, asynchronous ? await command.SingleAsync<string>() : command.Single<string>());
        var pager = new Pager(1, 1) { Order = "Name" };
        var page = asynchronous ? await command.ToPageAsync<string>(pager) : command.ToPage<string>(pager);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(new[] { marker }, page.Data);

        if (asynchronous)
            await Assert.ThrowsAsync<SqliteException>(() => query.Sql($"Select Name From {missingTable}").SingleAsync<string>());
        else
            Assert.Throws<SqliteException>(() => query.Sql($"Select Name From {missingTable}").Single<string>());
        Assert.Equal(marker, asynchronous ? await command.SingleAsync<string>() : command.Single<string>());

        var readMessages = messages.Where(message => message.Sql?.Contains(marker, StringComparison.Ordinal) == true)
            .ToArray();
        Assert.NotEmpty(readMessages);
        Assert.All(readMessages, message =>
        {
            Assert.NotNull(message.Transaction);
            Assert.True(message.Transaction.HasTransaction);
            Assert.True(message.Transaction.IsPrimaryReadTransaction);
        });
    }

    /// <summary>
    /// Primary 偏好在未配置读取策略时仍可执行普通无事务查询。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrimaryReadWithoutStrategy_ShouldExecuteWithoutTransaction(bool asynchronous)
    {
        using var provider = CreateProvider(PrimaryReadStrategy.None);
        using var scope = provider.CreateScope();
        using var readPreference = scope.ServiceProvider
            .GetRequiredService<IReadPreferenceScopeManager>()
            .Use(SqlReadPreference.Primary);
        const int marker = 417293;
        var messages = new ConcurrentQueue<DiagnosticsMessage>();
        using var observer = new SqliteDiagnosticObserver(messages.Enqueue);
        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create(DataSourceKey);

        var command = query.Sql($"Select {marker} As Value");
        Assert.Equal(marker, asynchronous ? await command.SingleAsync<int>() : command.Single<int>());

        var executions = messages.Where(message => message.Sql?.Contains(marker.ToString(),
            StringComparison.Ordinal) == true).ToArray();
        var execution = Assert.Single(executions);
        Assert.NotNull(execution.Transaction);
        Assert.False(execution.Transaction.HasTransaction);
        Assert.False(execution.Transaction.IsPrimaryReadTransaction);
    }

    /// <summary>
    /// 写入在 Primary 短事务策略下保持无事务执行并提交到 SQLite。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Write_WhenPrimaryReadTransactionIsConfigured_ShouldNotBorrowShortTransaction(bool asynchronous)
    {
        using var provider = CreateProvider(PrimaryReadStrategy.Transaction);
        using var scope = provider.CreateScope();
        using var readPreference = scope.ServiceProvider
            .GetRequiredService<IReadPreferenceScopeManager>()
            .Use(SqlReadPreference.Primary);
        var marker = $"write-primary-read-{Guid.NewGuid():N}";
        var messages = new ConcurrentQueue<DiagnosticsMessage>();
        using var observer = new SqliteDiagnosticObserver(messages.Enqueue);
        using var executor = scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create(DataSourceKey);

        var sql = $"Insert Into samples(Name) Values ('{marker}')";
        Assert.Equal(1, asynchronous ? await executor.ExecuteSqlAsync(sql) : executor.ExecuteSql(sql));

        var execution = Assert.Single(messages, message =>
            message.Sql?.Contains(marker, StringComparison.Ordinal) == true);
        Assert.NotNull(execution.Transaction);
        Assert.False(execution.Transaction.HasTransaction);
        Assert.False(execution.Transaction.IsPrimaryReadTransaction);
        Assert.Contains(marker, await _fixture.ReadNamesAsync("first"));
    }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    private ServiceProvider CreateProvider(PrimaryReadStrategy strategy)
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSqlDataSource(DataSourceKey, DatabaseType.Sqlite, _fixture.FirstConnectionString,
            setupAction: descriptor => descriptor.PrimaryReadStrategy = strategy);
        services.AddSqliteProvider();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// 收集 SQL 执行前的诊断消息。
    /// </summary>
    private sealed class SqliteDiagnosticObserver : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object>>, IDisposable
    {
        private readonly Action<DiagnosticsMessage> _onMessage;
        private readonly IDisposable _allSubscription;
        private IDisposable _listenerSubscription;

        public SqliteDiagnosticObserver(Action<DiagnosticsMessage> onMessage)
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
            if (value.Key == SqlQueryDiagnosticListenerNames.BeforeExecute &&
                value.Value is DiagnosticsMessage message)
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
