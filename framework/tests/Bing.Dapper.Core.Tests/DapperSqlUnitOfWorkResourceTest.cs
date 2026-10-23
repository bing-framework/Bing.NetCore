using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 验证 Dapper Uow 资源键与连接事务资源生命周期。
/// </summary>
public class DapperSqlUnitOfWorkResourceTest
{
    private static readonly SqlProviderTransactionCapabilities TransactionCapabilities = new()
    {
        SupportsTransactions = true,
        SupportsNativeAsyncBegin = false,
        SupportsNativeAsyncCommit = false,
        SupportsNativeAsyncRollback = false
    };

    /// <summary>
    /// 验证资源键仅在完整冻结配置和相同连接工厂下命中。
    /// </summary>
    [Fact]
    public void ResourceKey_ShouldIsolateProviderSourceTenantConfigurationAndResolver()
    {
        var resolver = new Mock<ISqlDbConnectionFactoryResolver>().Object;
        var context = CreateContext("tenant-a");
        var source = context.DataSource;
        var key = CreateKey(context, source, resolver);

        key.Equals(CreateKey(CreateContext("tenant-a"), CreateContext("tenant-a").DataSource, resolver))
            .ShouldBeTrue();
        key.Equals(CreateKey(CreateContext("tenant-b"), CreateContext("tenant-b").DataSource, resolver))
            .ShouldBeFalse();
        key.Equals(CreateKey(CreateContext("tenant-a", "provider-b"),
            CreateContext("tenant-a", "provider-b").DataSource, resolver)).ShouldBeFalse();
        key.Equals(CreateKey(CreateContext("tenant-a", connectionString: "Data Source=other.db"),
            CreateContext("tenant-a", connectionString: "Data Source=other.db").DataSource, resolver)).ShouldBeFalse();
        key.Equals(CreateKey(context, source, new Mock<ISqlDbConnectionFactoryResolver>().Object)).ShouldBeFalse();
    }

    /// <summary>
    /// 验证不可比较配置按 Query 独立，且 key 不公开连接字符串。
    /// </summary>
    [Fact]
    public void ResourceKey_WhenConfigurationIsOpaque_ShouldRemainQuerySpecific()
    {
        var context = CreateContext("tenant-a");
        var resolver = new Mock<ISqlDbConnectionFactoryResolver>().Object;
        var first = CreateKey(context, context.DataSource, resolver, canCompare: false);
        var second = CreateKey(context, context.DataSource, resolver, canCompare: false);

        first.Equals(second).ShouldBeFalse();
        first.ToString().ShouldNotContain("Data Source=orders.db");
    }

    /// <summary>
    /// 验证同步资源只打开、开始、提交和释放一次。
    /// </summary>
    [Fact]
    public void EnsureReadyAndCommit_ShouldOwnOneConnectionAndTransaction()
    {
        var (provider, serviceScope, manager) = CreateScopeManager();
        using (provider)
        using (serviceScope)
        {
            var connection = CreateConnection(out var transaction, out var state);
            var resolver = new Mock<ISqlDbConnectionFactoryResolver>();
            resolver.Setup(item => item.Create("bing.sqlite", "Data Source=orders.db"))
                .Returns(connection.Object);
            var root = manager.Begin();
            var key = new object();
            var resource = root.GetOrAddResource(key, () => new DapperSqlUnitOfWorkConnectionResource(
                key, root, CreateContext("tenant-a").DataSource, "bing.sqlite", "Data Source=orders.db",
                resolver.Object, TransactionCapabilities));

            resource.EnsureReady();
            resource.EnsureReady();
            ReferenceEquals(resource.Connection, connection.Object).ShouldBeTrue();
            ReferenceEquals(resource.Transaction, transaction.Object).ShouldBeTrue();
            state.OpenCount.ShouldBe(1);
            state.BeginCount.ShouldBe(1);

            root.Complete();
            root.Dispose();
            transaction.Verify(item => item.Commit(), Times.Once);
            transaction.Verify(item => item.Dispose(), Times.Once);
            connection.Verify(item => item.Dispose(), Times.Once);
        }
    }

    /// <summary>
    /// 验证异步资源准备遵守取消并回滚失败的工作单元。
    /// </summary>
    [Fact]
    public async Task EnsureReadyAsync_WhenCanceled_ShouldMarkRollbackOnlyWithoutOpening()
    {
        var (provider, serviceScope, manager) = CreateScopeManager();
        using (provider)
        using (serviceScope)
        {
            var connection = CreateConnection(out _, out var state);
            var resolver = new Mock<ISqlDbConnectionFactoryResolver>();
            resolver.Setup(item => item.Create("bing.sqlite", "Data Source=orders.db"))
                .Returns(connection.Object);
            var root = manager.Begin();
            var key = new object();
            var resource = root.GetOrAddResource(key, () => new DapperSqlUnitOfWorkConnectionResource(
                key, root, CreateContext("tenant-a").DataSource, "bing.sqlite", "Data Source=orders.db",
                resolver.Object, TransactionCapabilities));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Should.ThrowAsync<OperationCanceledException>(() => resource.EnsureReadyAsync(cancellation.Token));
            root.IsRollbackOnly.ShouldBeTrue();
            state.OpenCount.ShouldBe(0);
            root.Rollback();
            root.Dispose();
            connection.Verify(item => item.Dispose(), Times.Once);
        }
    }

    /// <summary>
    /// 验证事务不支持时在连接打开前失败。
    /// </summary>
    [Fact]
    public void EnsureReady_WhenProviderDoesNotSupportTransactions_ShouldFailBeforeOpen()
    {
        var (provider, serviceScope, manager) = CreateScopeManager();
        using (provider)
        using (serviceScope)
        {
            var connection = CreateConnection(out _, out var state);
            var resolver = new Mock<ISqlDbConnectionFactoryResolver>();
            resolver.Setup(item => item.Create("bing.sqlite", "Data Source=orders.db"))
                .Returns(connection.Object);
            var root = manager.Begin();
            var key = new object();
            var unsupported = new SqlProviderTransactionCapabilities { SupportsTransactions = false };
            var resource = root.GetOrAddResource(key, () => new DapperSqlUnitOfWorkConnectionResource(
                key, root, CreateContext("tenant-a").DataSource, "bing.sqlite", "Data Source=orders.db",
                resolver.Object, unsupported));

            Should.Throw<NotSupportedException>(resource.EnsureReady);
            state.OpenCount.ShouldBe(0);
            root.IsRollbackOnly.ShouldBeTrue();
            root.Rollback();
            root.Dispose();
        }
    }

    /// <summary>
    /// 验证异步事务能力拒绝在连接打开前失败并标记 root 仅回滚。
    /// </summary>
    [Fact]
    public async Task EnsureReadyAsync_WhenProviderDoesNotSupportTransactions_ShouldFailBeforeOpen()
    {
        var (provider, serviceScope, manager) = CreateScopeManager();
        using (provider)
        using (serviceScope)
        {
            var connection = CreateConnection(out _, out var state);
            var resolver = new Mock<ISqlDbConnectionFactoryResolver>();
            resolver.Setup(item => item.Create("bing.sqlite", "Data Source=orders.db"))
                .Returns(connection.Object);
            var root = manager.Begin();
            var key = new object();
            var unsupported = new SqlProviderTransactionCapabilities { SupportsTransactions = false };
            var resource = root.GetOrAddResource(key, () => new DapperSqlUnitOfWorkConnectionResource(
                key, root, CreateContext("tenant-a").DataSource, "bing.sqlite", "Data Source=orders.db",
                resolver.Object, unsupported));

            await Should.ThrowAsync<NotSupportedException>(() => resource.EnsureReadyAsync());
            root.IsRollbackOnly.ShouldBeTrue();
            state.OpenCount.ShouldBe(0);
            await root.RollbackAsync();
            await root.DisposeAsync();
            connection.Verify(item => item.Dispose(), Times.Once);
        }
    }

    [Theory]
    [InlineData(true, true, PrimaryReadStrategy.None, null)]
    [InlineData(false, false, PrimaryReadStrategy.None, null)]
    [InlineData(false, true, PrimaryReadStrategy.PrimaryDataSource, "primary")]
    public void EnsureReady_WhenFrozenSourceCannotJoinTransaction_ShouldFailBeforeOpen(
        bool isReadOnly, bool supportsTransactions, PrimaryReadStrategy strategy, string primaryKey)
    {
        var (provider, serviceScope, manager) = CreateScopeManager();
        using (provider)
        using (serviceScope)
        {
            var connection = CreateConnection(out _, out var state);
            var resolver = new Mock<ISqlDbConnectionFactoryResolver>();
            resolver.Setup(item => item.Create("bing.sqlite", "Data Source=orders.db"))
                .Returns(connection.Object);
            var root = manager.Begin();
            var key = new object();
            var context = CreateContext("tenant-a", setupSource: source =>
            {
                source.IsReadOnly = isReadOnly;
                source.SupportsTransactions = supportsTransactions;
                source.PrimaryReadStrategy = strategy;
                source.PrimaryDataSourceKey = primaryKey;
            });
            var resource = root.GetOrAddResource(key, () => new DapperSqlUnitOfWorkConnectionResource(
                key, root, context.DataSource, "bing.sqlite", "Data Source=orders.db", resolver.Object,
                TransactionCapabilities));

            Should.Throw<NotSupportedException>(resource.EnsureReady);

            state.OpenCount.ShouldBe(0);
            state.BeginCount.ShouldBe(0);
            root.IsRollbackOnly.ShouldBeTrue();
            root.Rollback();
            root.Dispose();
        }
    }

    [Fact]
    public void EnsureReady_NonTransactionalScopeWithTransactionReadStrategy_ShouldFailBeforeOpen()
    {
        var (provider, serviceScope, manager) = CreateScopeManager();
        using (provider)
        using (serviceScope)
        {
            var connection = CreateConnection(out _, out var state);
            var resolver = new Mock<ISqlDbConnectionFactoryResolver>();
            resolver.Setup(item => item.Create("bing.sqlite", "Data Source=orders.db"))
                .Returns(connection.Object);
            var root = manager.Begin(new UnitOfWorkScopeOptions { IsTransactional = false });
            var key = new object();
            var context = CreateContext("tenant-a", setupSource: source =>
                source.PrimaryReadStrategy = PrimaryReadStrategy.Transaction);
            var resource = root.GetOrAddResource(key, () => new DapperSqlUnitOfWorkConnectionResource(
                key, root, context.DataSource, "bing.sqlite", "Data Source=orders.db", resolver.Object, null));

            Should.Throw<NotSupportedException>(resource.EnsureReady);

            state.OpenCount.ShouldBe(0);
            state.BeginCount.ShouldBe(0);
            root.IsRollbackOnly.ShouldBeTrue();
            root.Rollback();
            root.Dispose();
        }
    }

    private static DapperSqlUnitOfWorkResourceKey CreateKey(DatabaseContext context,
        SqlDataSourceDescriptor source, ISqlDbConnectionFactoryResolver resolver, bool canCompare = true) =>
        new(context, source, DatabaseType.Sqlite, source.ProviderKey, source.ConnectionString, resolver, canCompare);

    private static DatabaseContext CreateContext(string tenantId, string providerKey = "bing.sqlite",
        string connectionString = "Data Source=orders.db",
        Action<SqlDataSourceDescriptor> setupSource = null)
    {
        var source = new SqlDataSourceDescriptor
        {
            Key = "orders",
            ProviderKey = providerKey,
            DatabaseType = DatabaseType.Sqlite,
            ConnectionString = connectionString
        };
        setupSource?.Invoke(source);
        return new DatabaseContext { DbKey = "orders", TenantId = tenantId, DataSource = source };
    }

    private static (ServiceProvider Provider, IServiceScope Scope, IUnitOfWorkScopeManager Manager)
        CreateScopeManager()
    {
        var services = new ServiceCollection();
        services.AddUnitOfWorkScopes();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return (provider, scope, scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>());
    }

    private static Mock<IDbConnection> CreateConnection(out Mock<IDbTransaction> transaction,
        out ConnectionStateTracker state)
    {
        var tracker = new ConnectionStateTracker();
        state = tracker;
        var connection = new Mock<IDbConnection>();
        connection.SetupGet(item => item.State).Returns(() => tracker.State);
        connection.Setup(item => item.Open()).Callback(() =>
        {
            tracker.OpenCount++;
            tracker.State = ConnectionState.Open;
        });
        connection.Setup(item => item.Close()).Callback(() => tracker.State = ConnectionState.Closed);
        transaction = new Mock<IDbTransaction>();
        transaction.SetupGet(item => item.Connection).Returns(connection.Object);
        connection.Setup(item => item.BeginTransaction()).Returns(transaction.Object).Callback(() => tracker.BeginCount++);
        connection.Setup(item => item.BeginTransaction(It.IsAny<IsolationLevel>()))
            .Returns(transaction.Object).Callback(() => tracker.BeginCount++);
        return connection;
    }

    private sealed class ConnectionStateTracker
    {
        public ConnectionState State { get; set; } = ConnectionState.Closed;
        public int OpenCount { get; set; }
        public int BeginCount { get; set; }
    }
}
