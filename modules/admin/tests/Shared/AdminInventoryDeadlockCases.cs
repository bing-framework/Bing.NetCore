using System.Data.Common;
using System.Runtime.ExceptionServices;
using System.Text;
using Bing.Admin.Data;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.Data.Inventory;
using Bing.Data.Transaction;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>
/// 验证真实 Admin 库存事务的 InnoDB 死锁重试。
/// </summary>
public sealed partial class AdminMySqlCapIntegrationTest
{
    /// <summary>
    /// 验证真实死锁回滚后使用新作用域提交一次整单。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenInnoDbDeadlocks_ShouldAwaitCleanupAndRetryFreshScope()
    {
        await using var deadlock = new InventoryDeadlockScenario(1);
        await WithInventoryHostAsync(false, async (host, connection, tenant) =>
        {
            await deadlock.InitializeAsync(connection, tenant);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var result = await ExecuteInventoryAsync(host, InventoryRequest(tenant, 3, 3), timeout.Token);

            Assert.True(result.Applied);
            Assert.False(result.Duplicate);
            deadlock.AssertAttempts(2, 1);
            await AssertInventoryCommittedCountsAsync(connection, tenant, 14, 1);
        }, deadlock.ConfigureServices);
    }

    /// <summary>
    /// 验证持续真实死锁达到四次上限后保留最后异常且全部回滚。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenInnoDbDeadlocksPersist_ShouldStopAfterFourCleanAttempts()
    {
        await using var deadlock = new InventoryDeadlockScenario(4);
        await WithInventoryHostAsync(false, async (host, connection, tenant) =>
        {
            await deadlock.InitializeAsync(connection, tenant);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var failure = await Assert.ThrowsAsync<MySqlException>(() =>
                ExecuteInventoryAsync(host, InventoryRequest(tenant, 3, 3), timeout.Token));

            deadlock.AssertAttempts(4, 4);
            Assert.Same(deadlock.Failures.Last(), failure);
            await AssertInventoryRolledBackAsync(connection, tenant);
        }, deadlock.ConfigureServices);
    }

    /// <summary>
    /// 通过真实相反行锁顺序制造库存事务死锁。
    /// </summary>
    /// <remarks>竞争事务修改大量独占测试行，使 InnoDB 选择较小的库存事务作为牺牲者；不使用 SIGNAL 或伪造驱动异常。</remarks>
    private sealed class InventoryDeadlockScenario : ITransactionalAuditingStore, IAsyncDisposable
    {
        /// <summary>
        /// 每次测试独占且只能由框架生成的表名。
        /// </summary>
        private readonly string _table = "bing_inv_deadlock_" + Guid.NewGuid().ToString("N");

        /// <summary>
        /// 需要制造死锁的尝试次数。
        /// </summary>
        private readonly int _deadlockAttempts;

        /// <summary>
        /// 各尝试实际解析的工作单元引用。
        /// </summary>
        private readonly List<object> _unitOfWorks = new();

        /// <summary>
        /// 各尝试实际使用的数据库事务引用。
        /// </summary>
        private readonly List<DbTransaction> _transactions = new();

        /// <summary>
        /// 已执行的作用域存储释放次数。
        /// </summary>
        private int _disposals;

        /// <summary>
        /// 已到达审计回调的次数。
        /// </summary>
        private int _auditCalls;

        /// <summary>
        /// 专用测试数据库连接字符串。
        /// </summary>
        private string _connection;

        /// <summary>
        /// 本轮独占的库存租户。
        /// </summary>
        private string _tenant;

        /// <summary>
        /// 初始化一个 <see cref="InventoryDeadlockScenario" /> 类型的实例。
        /// </summary>
        /// <param name="deadlockAttempts">制造死锁的次数。</param>
        internal InventoryDeadlockScenario(int deadlockAttempts) => _deadlockAttempts = deadlockAttempts;

        /// <summary>
        /// 获取真实数据库操作产生的库存异常。
        /// </summary>
        internal List<MySqlException> Failures { get; } = new();

        /// <summary>
        /// 注册真实存储的作用域观测器及审计死锁回调。
        /// </summary>
        /// <param name="services">当前独立宿主的服务集合。</param>
        internal void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITransactionalAuditingStore>(this);
            services.AddScoped<IInventoryOrderStore>(provider => new ObservedInventoryOrders(
                new DbInventoryOrderStore(InventoryDatabase.MySql, provider.GetRequiredService<IInventoryStore>()),
                provider.GetRequiredService<AdminUnitOfWork>(), this));
        }

        /// <summary>
        /// 创建当前测试独占的 InnoDB 竞争行。
        /// </summary>
        /// <param name="connection">专用测试库连接字符串。</param>
        /// <param name="tenant">当前库存测试租户。</param>
        internal async Task InitializeAsync(string connection, string tenant)
        {
            _connection = connection;
            _tenant = tenant;
            await using var database = new MySqlConnection(connection);
            await database.OpenAsync();
            await using var command = database.CreateCommand();
            command.CommandText = $"CREATE TABLE `{_table}` (Id int NOT NULL PRIMARY KEY, Value int NOT NULL) ENGINE=InnoDB";
            await command.ExecuteNonQueryAsync();
            command.CommandText = $"INSERT INTO `{_table}` VALUES " +
                string.Join(",", Enumerable.Range(0, 256).Select(value => $"({value},0)"));
            Assert.Equal(256, await command.ExecuteNonQueryAsync());
        }

        /// <inheritdoc />
        public async Task SaveAsync(DbTransaction transaction, string auditId, AuditLogInfo auditInfo,
            CancellationToken cancellationToken = default)
        {
            if (++_auditCalls <= _deadlockAttempts)
                await CauseDeadlockAsync(transaction, cancellationToken);
            await new DbAuditingStore(AuditLogDatabase.MySql)
                .SaveAsync(transaction, auditId, auditInfo, cancellationToken);
        }

        /// <summary>
        /// 在独立事务持有竞争行后与库存事务形成真实等待环。
        /// </summary>
        /// <param name="inventory">已经持有库存行锁的业务事务。</param>
        /// <param name="token">限制本轮数据库操作时长的令牌。</param>
        private async Task CauseDeadlockAsync(DbTransaction inventory, CancellationToken token)
        {
            await using var database = new MySqlConnection(_connection);
            await database.OpenAsync(token);
            await using var competitor = await database.BeginTransactionAsync(token);
            await using var weight = database.CreateCommand();
            weight.Transaction = competitor;
            weight.CommandText = $"UPDATE `{_table}` SET Value=Value+1";
            Assert.Equal(256, await weight.ExecuteNonQueryAsync(token));

            await using var stock = database.CreateCommand();
            stock.Transaction = competitor;
            stock.CommandTimeout = 15;
            stock.CommandText = "UPDATE bing_inventory_stock SET Quantity=Quantity " +
                "WHERE TenantId=@tenant AND WarehouseId='warehouse' AND Sku='a'";
            stock.Parameters.AddWithValue("@tenant", Encoding.UTF8.GetBytes(_tenant));
            // 库存事务拥有库存行，竞争事务拥有测试行；两边互相请求对方的锁。
            var waitingForStock = stock.ExecuteNonQueryAsync(token);
            Exception failure = null;
            try
            {
                await using var guard = inventory.Connection.CreateCommand();
                guard.Transaction = inventory;
                guard.CommandTimeout = 15;
                guard.CommandText = $"UPDATE `{_table}` SET Value=Value+1 WHERE Id=0";
                await guard.ExecuteNonQueryAsync(token);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            try
            {
                await waitingForStock;
            }
            catch (Exception exception)
            {
                if (failure == null)
                    failure = new InvalidOperationException("竞争事务被选为牺牲者，未覆盖库存重试路径。", exception);
                else
                    failure.Data["Bing.TestDeadlockCompetitorException"] = exception;
            }
            // 只清理本测试创建的竞争事务，库存事务仍由真实工作单元负责。
            await competitor.RollbackAsync(CancellationToken.None);
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
            throw new InvalidOperationException("受控竞争没有产生预期的 InnoDB 死锁。");
        }

        /// <summary>
        /// 核对真实尝试、作用域释放及事务结果。
        /// </summary>
        /// <param name="attempts">预期整单尝试数。</param>
        /// <param name="failures">预期真实死锁数。</param>
        internal void AssertAttempts(int attempts, int failures)
        {
            Assert.Equal(attempts, _unitOfWorks.Count);
            Assert.Equal(attempts, _unitOfWorks.Distinct(ReferenceEqualityComparer.Instance).Count());
            Assert.Equal(attempts, _transactions.Distinct(ReferenceEqualityComparer.Instance).Count());
            Assert.Equal(attempts, _disposals);
            Assert.Equal(failures, Failures.Count);
            Assert.All(Failures, exception =>
            {
                Assert.Equal(1213, exception.Number);
                Assert.Equal(TransactionOutcome.RolledBack, exception.GetTransactionOutcome());
                Assert.False(exception.Data.Contains("Bing.InventoryScopeCleanupException"));
                Assert.False(exception.Data.Contains("Bing.TransactionCleanupException"));
                Assert.False(exception.Data.Contains("Bing.TransactionRollbackException"));
            });
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_connection == null)
                return;
            await using var database = new MySqlConnection(_connection);
            await database.OpenAsync();
            await using var command = database.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS `{_table}`";
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 观测真实整单存储的工作单元身份、原生异常和释放。
        /// </summary>
        private sealed class ObservedInventoryOrders : IInventoryOrderStore, IDisposable
        {
            /// <summary>
            /// 当前作用域中执行 SQL 的真实整单存储。
            /// </summary>
            private readonly IInventoryOrderStore _inner;

            /// <summary>
            /// 当前测试的事务及释放观测状态。
            /// </summary>
            private readonly InventoryDeadlockScenario _scenario;

            /// <summary>
            /// 初始化一个 <see cref="ObservedInventoryOrders" /> 类型的实例。
            /// </summary>
            /// <param name="inner">当前作用域的真实存储。</param>
            /// <param name="unitOfWork">当前作用域的真实工作单元。</param>
            /// <param name="scenario">当前测试观测器。</param>
            internal ObservedInventoryOrders(IInventoryOrderStore inner, object unitOfWork,
                InventoryDeadlockScenario scenario)
            {
                _inner = inner;
                _scenario = scenario;
                // 下一轮实际解析服务前，上一轮作用域必须已经释放。
                Assert.Equal(scenario._unitOfWorks.Count, scenario._disposals);
                scenario._unitOfWorks.Add(unitOfWork);
            }

            /// <inheritdoc />
            public Task<InventoryOrderResult> FindAsync(DbConnection connection, InventoryOrderRequest request,
                CancellationToken cancellationToken = default) =>
                _inner.FindAsync(connection, request, cancellationToken);

            /// <inheritdoc />
            public async Task<InventoryOrderResult> ExecuteAsync(DbTransaction transaction, InventoryOrderRequest request,
                Func<DbTransaction, InventoryOrderResult, CancellationToken, Task> onCompleted,
                CancellationToken cancellationToken = default)
            {
                _scenario._transactions.Add(transaction);
                try
                {
                    return await _inner.ExecuteAsync(transaction, request, onCompleted, cancellationToken);
                }
                catch (MySqlException exception)
                {
                    _scenario.Failures.Add(exception);
                    throw;
                }
            }

            /// <inheritdoc />
            public void Dispose() => _scenario._disposals++;
        }
    }
}
