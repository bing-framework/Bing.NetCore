using System.Data;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Threading;
using Bing.Data.Enums;
using Bing.Data.Filters;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.Datas.EntityFramework.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// EF Core SQL 适配工厂与请求作用域过滤器的集成契约。
/// </summary>
public sealed class EfCoreSqlQueryFactoryScopeFilterContractTest
{
    private const string EnabledSql =
        "Select `ef_scope_filter_rows`.`Id`,`ef_scope_filter_rows`.`Name`,`ef_scope_filter_rows`.`IsDeleted` \r\nFrom `ef_scope_filter_rows` \r\nWhere `ef_scope_filter_rows`.`IsDeleted`=@_p_0";

    private const string DisabledSql =
        "Select `ef_scope_filter_rows`.`Id`,`ef_scope_filter_rows`.`Name`,`ef_scope_filter_rows`.`IsDeleted` \r\nFrom `ef_scope_filter_rows`";

    /// <summary>
    /// 测试目的：Shared 与 Independent EF 查询都应使用适配器工厂所属作用域的过滤器。
    /// </summary>
    [Theory]
    [InlineData(EfCoreSqlConnectionMode.Shared)]
    [InlineData(EfCoreSqlConnectionMode.Independent)]
    public async Task Create_WhenScopesAreValidated_ShouldKeepFilterStatePerAdapterScope(
        EfCoreSqlConnectionMode mode)
    {
        var connectionString = $"Data Source=file:ef-factory-filter-{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        Seed(keeper);

        using var root = CreateRoot(connectionString);
        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        using var connectionA = new SqliteConnection(connectionString);
        using var connectionB = new SqliteConnection(connectionString);
        connectionA.Open();
        connectionB.Open();
        using var unitOfWorkA = CreateUnitOfWork(connectionA, scopeA.ServiceProvider);
        using var unitOfWorkB = CreateUnitOfWork(connectionB, scopeB.ServiceProvider);
        var factoryA = scopeA.ServiceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();
        var factoryB = scopeB.ServiceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();
        var filterA = scopeA.ServiceProvider.GetRequiredService<IDataFilter>();
        var filterB = scopeB.ServiceProvider.GetRequiredService<IDataFilter>();
        using var observer = new SqlDiagnosticObserver();

        using (var queryA = factoryA.Create(unitOfWorkA, mode))
        using (var queryB = factoryB.Create(unitOfWorkB, mode))
        {
            var sourceA = queryA.From<EfSoftDeleteRow>();
            var sourceB = queryB.From<EfSoftDeleteRow>();

            await AssertQueryAsync(sourceA, enabled: true, new[] { 1 }, mode, observer);
            await AssertQueryAsync(sourceB, enabled: true, new[] { 1 }, mode, observer);

            using (filterA.Disable<ISoftDelete>())
            {
                await AssertQueryAsync(sourceA, enabled: false, new[] { 1, 2 }, mode, observer);
                await AssertQueryAsync(sourceB, enabled: true, new[] { 1 }, mode, observer);
            }

            await AssertQueryAsync(sourceA, enabled: true, new[] { 1 }, mode, observer);
            using (filterB.Disable<ISoftDelete>())
            {
                await AssertQueryAsync(sourceB, enabled: false, new[] { 1, 2 }, mode, observer);
                await AssertQueryAsync(sourceA, enabled: true, new[] { 1 }, mode, observer);
            }
            await AssertQueryAsync(sourceB, enabled: true, new[] { 1 }, mode, observer);

            Assert.Same(filterA, scopeA.ServiceProvider.GetRequiredService<IDataFilter>());
            Assert.NotSame(filterA, filterB);
        }

        if (mode == EfCoreSqlConnectionMode.Shared)
            Assert.Equal(ConnectionState.Open, connectionA.State);
    }

    private static async Task AssertQueryAsync(SqlLambdaQuery source, bool enabled, int[] expectedIds,
        EfCoreSqlConnectionMode mode, SqlDiagnosticObserver observer)
    {
        var expectedSql = enabled ? EnabledSql : DisabledSql;
        Assert.Equal(expectedSql, source.ToSql());
        using var capture = observer.BeginCapture();
        var rows = await source.ToListAsync<EfSoftDeleteRow>();
        Assert.Equal(expectedIds, rows.Select(row => row.Id));
        var message = Assert.Single(capture.Messages);
        Assert.Equal(expectedSql, message.Sql);
        if (enabled)
        {
            var parameter = Assert.Single(message.Parameters.Items);
            Assert.Equal("@_p_0", parameter.Name);
            Assert.Equal(false, parameter.Value);
        }
        else
        {
            Assert.Equal(typeof(ReadOnlyCollection<SqlParam>).FullName, message.Parameters.OriginalParameterType);
            var parameterSourceCount = Assert.Single(message.Parameters.Items);
            Assert.Equal("Count", parameterSourceCount.Name);
            Assert.Equal(0, parameterSourceCount.Value);
        }

        Assert.Equal(mode == EfCoreSqlConnectionMode.Shared
                ? SqlConnectionSource.EntityFrameworkCore
                : SqlConnectionSource.DataSource,
            message.Connection.Source);
        Assert.Equal(mode == EfCoreSqlConnectionMode.Shared
                ? SqlResourceOwnership.External
                : SqlResourceOwnership.Owned,
            message.Connection.Ownership);
    }

    private static ServiceProvider CreateRoot(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqliteProvider();
        services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
        services.AddEfCoreSqlQueryFactory();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private static TestUnitOfWork CreateUnitOfWork(SqliteConnection connection, IServiceProvider serviceProvider)
    {
        var options = new DbContextOptionsBuilder<TestUnitOfWork>()
            .UseSqlite(connection)
            .Options;
        return new TestUnitOfWork(options, serviceProvider);
    }

    private static void Seed(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "Create Table ef_scope_filter_rows (Id Integer Primary Key, Name Text Not Null, IsDeleted Integer Not Null); Insert Into ef_scope_filter_rows Values (1, 'active', 0), (2, 'deleted', 1);";
        command.ExecuteNonQuery();
    }

    private sealed class TestUnitOfWork : UnitOfWorkBase
    {
        public TestUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EfSoftDeleteRow>(builder =>
            {
                builder.ToTable("ef_scope_filter_rows");
                builder.HasKey(row => row.Id);
            });
        }
    }

    public sealed class EfSoftDeleteRow : ISoftDelete
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public bool IsDeleted { get; set; }
    }

    private sealed class SqlDiagnosticObserver : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object>>, IDisposable
    {
        private readonly AsyncLocal<List<DiagnosticsMessage>> _capturedMessages = new();
        private readonly IDisposable _allSubscription;
        private IDisposable _listenerSubscription;

        public SqlDiagnosticObserver()
        {
            _allSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public CaptureScope BeginCapture()
        {
            var previous = _capturedMessages.Value;
            var messages = new List<DiagnosticsMessage>();
            _capturedMessages.Value = messages;
            return new CaptureScope(_capturedMessages, previous, messages);
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == SqlQueryDiagnosticListenerNames.DiagnosticListenerName)
                _listenerSubscription = value.Subscribe(this);
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key == SqlQueryDiagnosticListenerNames.BeforeExecute && value.Value is DiagnosticsMessage message)
                _capturedMessages.Value?.Add(message);
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void Dispose()
        {
            _listenerSubscription?.Dispose();
            _allSubscription.Dispose();
        }

        public sealed class CaptureScope : IDisposable
        {
            private readonly AsyncLocal<List<DiagnosticsMessage>> _storage;
            private readonly List<DiagnosticsMessage> _previous;

            public CaptureScope(AsyncLocal<List<DiagnosticsMessage>> storage,
                List<DiagnosticsMessage> previous, List<DiagnosticsMessage> messages)
            {
                _storage = storage;
                _previous = previous;
                Messages = messages;
            }

            public List<DiagnosticsMessage> Messages { get; }

            public void Dispose() => _storage.Value = _previous;
        }
    }
}
