using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Bing.Admin.Data.Inventory;
using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.Data.Inventory;
using Bing.Data.Transaction;
using Bing.Events.Cap;
using Bing.Events.Messages;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>
/// 验证真实 Admin 工作单元的整单库存、关键审计与 CAP 事务链路。
/// </summary>
public sealed partial class AdminMySqlCapIntegrationTest
{
    /// <summary>
    /// 验证成功提交与重复请求只写一次库存、审计和消息。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenRepeated_ShouldCommitInventoryAuditAndOutboxOnce()
    {
        await WithInventoryHostAsync(false, async (host, connection, tenant) =>
        {
            var first = await ExecuteInventoryAsync(host, InventoryRequest(tenant, 3, 2));
            Assert.True(first.Applied);
            Assert.Equal(new long?[] { 7, 8 }, first.RemainingQuantities);
            var repeated = await ExecuteInventoryAsync(host, InventoryRequest(tenant, 3, 2));
            Assert.True(repeated.Duplicate);
            Assert.Equal(first.RemainingQuantities, repeated.RemainingQuantities);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ExecuteInventoryAsync(host, InventoryRequest(tenant, 4, 2)));
            Assert.Equal(7, await InventoryScalarAsync(connection,
                "SELECT Quantity FROM bing_inventory_stock WHERE TenantId=@tenant AND Sku='a'", tenant));
            Assert.Equal(1, await CountInventoryOrdersAsync(connection, tenant));
            Assert.Equal(1, await InventoryScalarAsync(connection,
                "SELECT COUNT(*) FROM bing_audit_log WHERE TenantId=@tenant", tenant));
            Assert.Equal(1, await CountInventoryOutboxAsync(connection, tenant));
            await using (var query = new MySqlConnection(connection))
            {
                await query.OpenAsync();
                await using var queryScope = host.Services.CreateAsyncScope();
                var found = await queryScope.ServiceProvider.GetRequiredService<IInventoryOrderStore>()
                    .FindAsync(query, InventoryRequest(tenant, 3, 2));
                Assert.True(found.Duplicate);
                Assert.Equal(first.RemainingQuantities, found.RemainingQuantities);
            }
            await InventorySubscriberFor(host).WaitAsync("order-" + tenant);
            Assert.Equal(1, InventorySubscriberFor(host).Deliveries("order-" + tenant));
        });
    }

    /// <summary>
    /// 验证保留首尾空格的租户身份在库存、审计和出箱中保持一致。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenTenantIdentityContainsWhitespace_ShouldKeepAuditAndOutboxIsolated()
    {
        await WithInventoryHostAsync(false, async (host, connection, tenant) =>
        {
            var exactTenant = tenant + " ";
            await SeedInventoryTenantAsync(connection, exactTenant);
            var result = await ExecuteInventoryAsync(host, InventoryRequest(exactTenant, 1, 1));

            Assert.True(result.Applied);
            Assert.Equal(9, await InventoryScalarAsync(connection,
                "SELECT Quantity FROM bing_inventory_stock WHERE TenantId=@tenant AND Sku='a'", exactTenant));
            Assert.Equal(1, await InventoryScalarAsync(connection,
                "SELECT COUNT(*) FROM bing_audit_log WHERE TenantId=@tenant", exactTenant));
            Assert.Equal(0, await InventoryScalarAsync(connection,
                "SELECT COUNT(*) FROM bing_audit_log WHERE TenantId=@tenant", tenant));
            Assert.Equal(1, await CountInventoryOutboxAsync(connection, exactTenant));
        });
    }

    /// <summary>
    /// 验证两个独立工作单元竞争同一整单仅提交一次。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenScopesCompeteForSameOrder_ShouldCommitExactlyOnce()
    {
        await WithInventoryHostAsync(false, async (host, connection, tenant) =>
        {
            var request = InventoryRequest(tenant, 3, 2);
            var results = await Task.WhenAll(ExecuteInventoryAsync(host, request), ExecuteInventoryAsync(host, request));
            Assert.All(results, result => Assert.True(result.Applied));
            Assert.Single(results, result => !result.Duplicate);
            Assert.Single(results, result => result.Duplicate);
            Assert.Equal(15, await InventoryScalarAsync(connection,
                "SELECT SUM(Quantity) FROM bing_inventory_stock WHERE TenantId=@tenant", tenant));
            Assert.Equal(1, await CountInventoryOrdersAsync(connection, tenant));
            Assert.Equal(1, await CountInventoryOutboxAsync(connection, tenant));
            Assert.Equal(1, await InventoryScalarAsync(connection,
                "SELECT COUNT(*) FROM bing_audit_log WHERE TenantId=@tenant", tenant));
        });
    }

    /// <summary>
    /// 验证不同整单竞争有限库存不会超卖或留下部分扣减。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenDifferentOrdersCompete_ShouldKeepNonNegativeAtomicStock()
    {
        await WithInventoryHostAsync(false, async (host, connection, tenant) =>
        {
            var first = new InventoryOrderRequest(tenant, "warehouse", "order-" + tenant + "-a",
                new[] { new InventoryOrderLine("a", 7), new InventoryOrderLine("b", 7) });
            var second = new InventoryOrderRequest(tenant, "warehouse", "order-" + tenant + "-b", first.Lines);
            var results = await Task.WhenAll(ExecuteInventoryAsync(host, first), ExecuteInventoryAsync(host, second));
            Assert.Single(results, result => result.Applied);
            Assert.Single(results, result => result.Status == InventoryDeductionStatus.Insufficient);
            Assert.Equal(6, await InventoryScalarAsync(connection,
                "SELECT SUM(Quantity) FROM bing_inventory_stock WHERE TenantId=@tenant", tenant));
            Assert.Equal(0, await InventoryScalarAsync(connection,
                "SELECT COUNT(*) FROM bing_inventory_stock WHERE TenantId=@tenant AND Quantity<0", tenant));
            Assert.Equal(2, await CountInventoryOrdersAsync(connection, tenant));
            Assert.Equal(2, await CountInventoryOutboxAsync(connection, tenant));
            Assert.Equal(2, await InventoryScalarAsync(connection,
                "SELECT COUNT(*) FROM bing_audit_log WHERE TenantId=@tenant", tenant));
        });
    }

    /// <summary>
    /// 验证后项不足回退所有扣减并持久化首次不足结果。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenLaterSkuIsInsufficient_ShouldRememberResultWithoutPartialDeduction()
    {
        await WithInventoryHostAsync(false, async (host, connection, tenant) =>
        {
            var request = InventoryRequest(tenant, 3, 11);
            var first = await ExecuteInventoryAsync(host, request);
            Assert.False(first.Applied);
            Assert.Equal(InventoryDeductionStatus.Insufficient, first.Status);
            Assert.Equal(20, await InventoryScalarAsync(connection,
                "SELECT SUM(Quantity) FROM bing_inventory_stock WHERE TenantId=@tenant", tenant));
            await InventoryScalarAsync(connection,
                "UPDATE bing_inventory_stock SET Quantity=20 WHERE TenantId=@tenant AND Sku='b'; SELECT 1", tenant);
            var repeated = await ExecuteInventoryAsync(host, request);
            Assert.True(repeated.Duplicate);
            Assert.False(repeated.Applied);
            Assert.Equal(30, await InventoryScalarAsync(connection,
                "SELECT SUM(Quantity) FROM bing_inventory_stock WHERE TenantId=@tenant", tenant));
            Assert.Equal(0, await InventoryScalarAsync(connection,
                "SELECT COUNT(*) FROM bing_inventory_request WHERE TenantId=@tenant", tenant));
            Assert.Equal(1, await CountInventoryOutboxAsync(connection, tenant));
            Assert.Equal(1, await InventoryScalarAsync(connection,
                "SELECT COUNT(*) FROM bing_audit_log WHERE TenantId=@tenant", tenant));
        });
    }

    /// <summary>
    /// 验证真实审计写入后发生异常仍回滚库存和整单结果。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenCriticalAuditFails_ShouldRollbackAllBusinessWrites()
    {
        await WithInventoryHostAsync(true, async (host, connection, tenant) =>
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ExecuteInventoryAsync(host, InventoryRequest(tenant, 3, 2)));
            Assert.Equal("critical audit failure", failure.Message);
            Assert.Equal(TransactionOutcome.RolledBack, failure.GetTransactionOutcome());
            await AssertInventoryRolledBackAsync(connection, tenant);
        });
    }

    /// <summary>
    /// 验证 CAP 出箱完成后取消仍回滚库存、整单和关键审计。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenCancelledAfterOutbox_ShouldRollbackAllWrites()
    {
        await WithInventoryHostAsync(false, async (host, connection, tenant) =>
        {
            var cancellation = host.Services.GetRequiredService<InventoryCancellation>();
            cancellation.Enabled = true;
            var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                ExecuteInventoryAsync(host, InventoryRequest(tenant, 3, 2), cancellation.Source.Token));
            Assert.Equal(TransactionOutcome.RolledBack, failure.GetTransactionOutcome());
            Assert.Equal(1, cancellation.PublishedBeforeCancel);
            await AssertInventoryRolledBackAsync(connection, tenant);
        });
    }

    /// <summary>
    /// 注册真实库存业务和可控事务故障。
    /// </summary>
    /// <param name="services">当前测试宿主服务集合。</param>
    /// <param name="failAudit">是否在真实审计写入后抛错。</param>
    private static void ConfigureInventory(IServiceCollection services, bool failAudit)
    {
        services.AddAdminInventory();
        if (failAudit)
            services.AddSingleton<ITransactionalAuditingStore>(new InventoryAuditFailure());
        services.AddSingleton<InventoryCancellation>();
        services.AddScoped<IMessageEventBus>(provider => new InventoryCancellingBus(
            new MessageEventBus(provider.GetRequiredService<ICapPublisher>(),
                provider.GetRequiredService<ITransactionActionManager>(),
                provider.GetRequiredService<ILogger<MessageEventBus>>()),
            provider.GetRequiredService<InventoryCancellation>()));
        services.AddSingleton<ICapSubscribe, InventorySubscriber>();
    }

    /// <summary>
    /// 在专用数据库运行拥有独立租户数据的真实宿主。
    /// </summary>
    /// <param name="failAudit">是否启用审计故障。</param>
    /// <param name="action">业务验证操作。</param>
    /// <param name="configureInventory">在默认库存注册后配置当前测试的观测或故障服务。</param>
    private static async Task WithInventoryHostAsync(bool failAudit, Func<IHost, string, string, Task> action,
        Action<IServiceCollection> configureInventory = null)
    {
#if BING_ADMIN_EFCORE_TESTS
        var settings = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(settings, withInventory: true, failInventoryAudit: failAudit,
            configureInventory: configureInventory);
#else
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        using var host = CreateHost(settings, orm, withInventory: true, failInventoryAudit: failAudit,
            configureInventory: configureInventory);
#endif
        var tenant = "inventory-test-" + Guid.NewGuid().ToString("N");
        await using (var connection = new MySqlConnection(settings.ConnectionString))
        {
            await connection.OpenAsync();
            foreach (var sql in InventorySchema.GetCreateStatements(InventoryDatabase.MySql)
                         .Concat(InventoryOrderSchema.GetCreateStatements(InventoryDatabase.MySql))
                         .Concat(AuditLogSchema.GetCreateStatements(AuditLogDatabase.MySql)))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
            await using var seed = connection.CreateCommand();
            seed.CommandText = "INSERT INTO bing_inventory_stock (TenantId, WarehouseId, Sku, Quantity) " +
                               "VALUES (@tenant,'warehouse','a',10),(@tenant,'warehouse','b',10)";
            seed.Parameters.AddWithValue("@tenant", tenant);
            await seed.ExecuteNonQueryAsync();
        }
        try
        {
            await host.StartAsync();
            // Bootstrap 完成不代表新主题已经绑定，用实际同主题消息确认就绪。
            var probe = "probe-" + tenant;
            var elapsed = Stopwatch.StartNew();
            while (InventorySubscriberFor(host).Deliveries(probe) == 0)
            {
                if (elapsed.Elapsed > TimeSpan.FromSeconds(30))
                    throw new TimeoutException("库存 CAP 订阅未就绪。");
                await using var scope = host.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ICapPublisher>().PublishAsync(
                    AdminInventoryService.CompletedTopic, new { OrderId = probe });
                await Task.Delay(100);
            }
            await action(host, settings.ConnectionString, tenant);
        }
        finally
        {
            try { await host.StopAsync(); }
            finally
            {
                foreach (var table in new[] { "bing_inventory_request", "bing_inventory_stock", "bing_audit_log" })
                    await InventoryScalarAsync(settings.ConnectionString,
                        $"DELETE FROM {table} WHERE TenantId=@tenant; SELECT 1", tenant);
                await InventoryScalarAsync(settings.ConnectionString,
                    "DELETE FROM bing_inventory_order WHERE TenantId=@binaryTenant; SELECT 1", tenant);
                await InventoryScalarAsync(settings.ConnectionString,
                    "DELETE FROM bing_inventory_ordered_projection WHERE TenantId=@binaryTenant; SELECT 1", tenant);
                var exactTenant = tenant + " ";
                foreach (var table in new[] { "bing_inventory_request", "bing_inventory_stock", "bing_audit_log" })
                    await InventoryScalarAsync(settings.ConnectionString,
                        $"DELETE FROM {table} WHERE TenantId=@tenant; SELECT 1", exactTenant);
                await InventoryScalarAsync(settings.ConnectionString,
                    "DELETE FROM bing_inventory_order WHERE TenantId=@binaryTenant; SELECT 1", exactTenant);
                await InventoryScalarAsync(settings.ConnectionString,
                    "DELETE FROM bing_inventory_ordered_projection WHERE TenantId=@binaryTenant; SELECT 1", exactTenant);
            }
        }
    }

    /// <summary>
    /// 创建双 SKU 的独立整单请求。
    /// </summary>
    /// <param name="tenant">测试租户。</param>
    /// <param name="first">首项数量。</param>
    /// <param name="second">后项数量。</param>
    /// <returns>不可变业务请求。</returns>
    private static InventoryOrderRequest InventoryRequest(string tenant, long first, long second) =>
        new(tenant, "warehouse", "order-" + tenant,
            new[] { new InventoryOrderLine("a", first), new InventoryOrderLine("b", second) });

    /// <summary>
    /// 使用独立作用域调用生产库存业务。
    /// </summary>
    /// <param name="host">已启动测试宿主。</param>
    /// <param name="request">整单请求。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>已提交业务结果。</returns>
    private static async Task<InventoryOrderResult> ExecuteInventoryAsync(IHost host, InventoryOrderRequest request,
        CancellationToken token = default)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AdminInventoryOrderExecutor>()
            .ExecuteAsync(request, "test-user", "test-trace", token);
    }

    /// <summary>
    /// 核对失败事务没有保留部分业务记录。
    /// </summary>
    /// <param name="connection">专用数据库连接字符串。</param>
    /// <param name="tenant">当前测试拥有的租户。</param>
    private static async Task AssertInventoryRolledBackAsync(string connection, string tenant)
    {
        Assert.Equal(20, await InventoryScalarAsync(connection,
            "SELECT SUM(Quantity) FROM bing_inventory_stock WHERE TenantId=@tenant", tenant));
        Assert.Equal(0, await CountInventoryOrdersAsync(connection, tenant));
        Assert.Equal(0, await CountInventoryOutboxAsync(connection, tenant));
        foreach (var table in new[] { "bing_inventory_request", "bing_audit_log" })
            Assert.Equal(0, await InventoryScalarAsync(connection,
                $"SELECT COUNT(*) FROM {table} WHERE TenantId=@tenant", tenant));
    }

    /// <summary>
    /// 查询整单结果数量。
    /// </summary>
    /// <param name="connection">专用数据库连接字符串。</param>
    /// <param name="tenant">测试租户。</param>
    /// <returns>整单结果数量。</returns>
    private static Task<long> CountInventoryOrdersAsync(string connection, string tenant) =>
        InventoryScalarAsync(connection,
            "SELECT COUNT(*) FROM bing_inventory_order WHERE TenantId=@binaryTenant", tenant);

    /// <summary>
    /// 查询当前测试业务的 CAP 出箱数量。
    /// </summary>
    /// <param name="connection">专用数据库连接字符串。</param>
    /// <param name="tenant">测试租户。</param>
    /// <returns>不包含就绪探针的业务消息数量。</returns>
    private static Task<long> CountInventoryOutboxAsync(string connection, string tenant) =>
        InventoryScalarAsync(connection, "SELECT COUNT(*) FROM `cap.published` " +
            "WHERE Name='bing.admin.inventory.completed' AND Content LIKE @business", tenant);

    /// <summary>
    /// 为精确租户身份补充独立库存初始行。
    /// </summary>
    /// <param name="connection">专用数据库连接字符串。</param>
    /// <param name="tenant">保留原值的租户标识。</param>
    private static async Task SeedInventoryTenantAsync(string connection, string tenant)
    {
        await using var database = new MySqlConnection(connection);
        await database.OpenAsync();
        await using var command = database.CreateCommand();
        command.CommandText = "INSERT INTO bing_inventory_stock (TenantId, WarehouseId, Sku, Quantity) " +
                              "VALUES (@tenant, 'warehouse', 'a', 10), (@tenant, 'warehouse', 'b', 10)";
        command.Parameters.AddWithValue("@tenant", Encoding.UTF8.GetBytes(tenant));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 执行参数化的测试数据库标量语句。
    /// </summary>
    /// <param name="connectionString">专用数据库连接字符串。</param>
    /// <param name="sql">仅来自测试代码的 SQL。</param>
    /// <param name="tenant">测试租户。</param>
    /// <returns>标量整数结果。</returns>
    private static async Task<long> InventoryScalarAsync(string connectionString, string sql, string tenant)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = sql;
        command.Parameters.AddWithValue("@tenant", tenant);
        command.Parameters.AddWithValue("@binaryTenant", Encoding.UTF8.GetBytes(tenant));
        command.Parameters.AddWithValue("@business", "%order-" + tenant + "%");
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 取得当前宿主专属库存订阅者。
    /// </summary>
    /// <param name="host">当前宿主。</param>
    /// <returns>库存订阅者实例。</returns>
    private static InventorySubscriber InventorySubscriberFor(IHost host) =>
        host.Services.GetServices<ICapSubscribe>().OfType<InventorySubscriber>().Single();

    /// <summary>
    /// 接收真实 RabbitMQ 库存消息的测试订阅者。
    /// </summary>
    public sealed class InventorySubscriber : ICapSubscribe
    {
        /// <summary>
        /// 本宿主已完成接收的消息计数。
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _received = new(StringComparer.Ordinal);

        /// <summary>
        /// 接收库存消息并记录业务请求标识。
        /// </summary>
        /// <param name="payload">CAP 实际反序列化的业务负载。</param>
#if BING_ADMIN_EFCORE_TESTS
        [CapSubscribe(AdminInventoryService.CompletedTopic, Group = "bing-admin-efcore-inventory-integration")]
#else
        [CapSubscribe(AdminInventoryService.CompletedTopic, Group = "bing-admin-freesql-inventory-integration")]
#endif
        public Task ConsumeAsync(JsonElement payload)
        {
            _received.AddOrUpdate(payload.GetProperty("OrderId").GetString(), 1, (_, count) => count + 1);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 读取当前宿主收到的消息次数。
        /// </summary>
        /// <param name="order">整单或探针标识。</param>
        /// <returns>接收次数。</returns>
        internal int Deliveries(string order) => _received.TryGetValue(order, out var count) ? count : 0;

        /// <summary>
        /// 有界等待真实业务消息到达。
        /// </summary>
        /// <param name="order">整单标识。</param>
        internal async Task WaitAsync(string order)
        {
            var elapsed = Stopwatch.StartNew();
            while (Deliveries(order) == 0)
            {
                if (elapsed.Elapsed > TimeSpan.FromSeconds(30))
                    throw new TimeoutException("库存消息未被真实订阅者收到。");
                await Task.Delay(50);
            }
        }
    }

    /// <summary>
    /// 在真实关键审计写入后抛出故障。
    /// </summary>
    private sealed class InventoryAuditFailure : ITransactionalAuditingStore
    {
        /// <inheritdoc />
        public async Task SaveAsync(DbTransaction transaction, string auditId, AuditLogInfo auditInfo,
            CancellationToken cancellationToken = default)
        {
            await new DbAuditingStore(AuditLogDatabase.MySql).SaveAsync(transaction, auditId, auditInfo, cancellationToken);
            throw new InvalidOperationException("critical audit failure");
        }
    }

    /// <summary>
    /// 持有当前测试宿主的可控取消状态。
    /// </summary>
    private sealed class InventoryCancellation : IDisposable
    {
        /// <summary>
        /// 初始化一个 <see cref="InventoryCancellation" /> 类型的实例。
        /// </summary>
        public InventoryCancellation()
        {
        }
        /// <summary>
        /// 获取在真实出箱动作之后触发的取消源。
        /// </summary>
        internal CancellationTokenSource Source { get; } = new();
        /// <summary>
        /// 获取或设置是否启用出箱后取消。
        /// </summary>
        internal bool Enabled { get; set; }
        /// <summary>
        /// 获取或设置取消前完成的出箱次数。
        /// </summary>
        internal int PublishedBeforeCancel { get; set; }
        /// <inheritdoc />
        public void Dispose() => Source.Dispose();
    }

    /// <summary>
    /// 完成真实 CAP 发布后触发测试取消。
    /// </summary>
    private sealed class InventoryCancellingBus : IMessageEventBus
    {
        /// <summary>
        /// 当前作用域的真实消息总线。
        /// </summary>
        private readonly IMessageEventBus _inner;
        /// <summary>
        /// 当前宿主的取消控制器。
        /// </summary>
        private readonly InventoryCancellation _cancellation;
        /// <summary>
        /// 初始化一个 <see cref="InventoryCancellingBus" /> 类型的实例。
        /// </summary>
        /// <param name="inner">真实消息总线。</param>
        /// <param name="cancellation">当前测试控制器。</param>
        internal InventoryCancellingBus(IMessageEventBus inner, InventoryCancellation cancellation)
        {
            _inner = inner;
            _cancellation = cancellation;
        }
        /// <inheritdoc />
        public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IMessageEvent
        {
            await _inner.PublishAsync(@event, cancellationToken);
            if (_cancellation.Enabled)
            {
                _cancellation.PublishedBeforeCancel++;
                _cancellation.Source.Cancel();
            }
        }
        /// <inheritdoc />
        public Task PublishAsync(string name, object data, string callback = null, bool send = false,
            CancellationToken cancellationToken = default) =>
            _inner.PublishAsync(name, data, callback, send, cancellationToken);
    }
}
