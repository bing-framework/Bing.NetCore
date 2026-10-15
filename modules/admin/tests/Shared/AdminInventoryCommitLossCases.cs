using System.Data.Common;
using Bing.Admin.Data.Inventory;
using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.Data.Inventory;
using Bing.Data.Transaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>
/// 验证 MySQL 提交成功但确认报文丢失后的库存恢复。
/// </summary>
public sealed partial class AdminMySqlCapIntegrationTest
{
    /// <summary>
    /// 验证确认丢失保留未知结果且按原请求恢复不会重复扣减。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOriginalCommittedResult()
    {
        await WithInventoryHostAsync(false, async (normalHost, connection, tenant) =>
        {
            await using var proxy = new InventoryCommitAckLossProxy(connection);
            var observation = new CommitLossAudits();
            await WithCommitLossHostAsync(proxy.ConnectionString, observation, async host =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var request = InventoryRequest(tenant, 3, 3);
                var failure = await Record.ExceptionAsync(() => host.Services
                    .GetRequiredService<AdminInventoryOrderExecutor>().ExecuteAsync(request,
                        "test-user", "commit-loss-trace", timeout.Token));

                Assert.NotNull(failure);
                AssertCommitLossTriggered(proxy, observation, failure);
                await proxy.AcknowledgementDropped.WaitAsync(timeout.Token);
                Assert.Equal(1, proxy.DropCount);
                Assert.Equal(1, observation.Attempts);
                Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
                Assert.False(failure.Data.Contains("Bing.InventoryCommittedResult"));
                Assert.False(failure.Data.Contains("Bing.InventoryRecoveredResult"));

                var recovered = await QueryCommittedInventoryAsync(normalHost, connection, request, timeout.Token);
                Assert.True(recovered.Applied);
                Assert.True(recovered.Duplicate);
                Assert.Equal(new long?[] { 7, 7 }, recovered.RemainingQuantities);
                var repeated = await ExecuteInventoryAsync(normalHost, request, timeout.Token);
                Assert.True(repeated.Duplicate);
                Assert.Equal(recovered.RemainingQuantities, repeated.RemainingQuantities);
                await AssertInventoryCommittedCountsAsync(connection, tenant, 14, 1);
            });
        });
    }

    /// <summary>
    /// 验证恢复确认提交时仍保留断连后的事务清理失败。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldPreserveRecoveryAndCleanupFailures()
    {
        await WithInventoryHostAsync(false, async (normalHost, connection, tenant) =>
        {
            await using var proxy = new InventoryCommitAckLossProxy(connection);
            var observation = new CommitLossAudits();
            await WithCommitLossHostAsync(proxy.ConnectionString, observation, async host =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var request = InventoryRequest(tenant, 3, 3);
                InventoryOrderResult queryResult = null;
                var recoveryCalls = 0;
                var failure = await Record.ExceptionAsync(() => host.Services
                    .GetRequiredService<AdminInventoryOrderExecutor>().ExecuteWithRecoveryAsync(request,
                        "test-user", "commit-loss-recovery-trace", async (original, token) =>
                        {
                            Assert.Same(request, original);
                            Assert.True(proxy.AcknowledgementDropped.IsCompletedSuccessfully);
                            recoveryCalls++;
                            queryResult = await QueryCommittedInventoryAsync(normalHost, connection, original, token);
                            return queryResult;
                        }, timeout.Token));

                Assert.NotNull(failure);
                AssertCommitLossTriggered(proxy, observation, failure);
                await proxy.AcknowledgementDropped.WaitAsync(timeout.Token);
                Assert.Equal(1, proxy.DropCount);
                Assert.Equal(1, observation.Attempts);
                Assert.Equal(1, recoveryCalls);
                Assert.True(queryResult.Applied);
                Assert.True(queryResult.Duplicate);
                Assert.Same(queryResult, failure.Data["Bing.InventoryRecoveredResult"]);
                Assert.Equal(TransactionOutcome.Committed, failure.GetTransactionOutcome());
                Assert.True(failure is AggregateException ||
                    failure.Data.Contains("Bing.TransactionRollbackException") ||
                    failure.Data.Contains("Bing.TransactionCleanupException") ||
                    failure.Data.Contains("Bing.InventoryScopeCleanupException"));
                await AssertInventoryCommittedCountsAsync(connection, tenant, 14, 1);
            });
        });
    }

    /// <summary>
    /// 核对真实故障已经触发并提供不含业务正文的失败定位信息。
    /// </summary>
    /// <param name="proxy">当前测试的提交确认故障代理。</param>
    /// <param name="observation">当前测试宿主的存储创建记录。</param>
    /// <param name="failure">真实业务调用返回的异常。</param>
    private static void AssertCommitLossTriggered(InventoryCommitAckLossProxy proxy, CommitLossAudits observation,
        Exception failure)
    {
        var errors = new List<string>();
        for (var current = failure; current != null && errors.Count < 8; current = current.InnerException)
            errors.Add(current.GetType().FullName + ":" + current.GetType().GetProperty("Number")?.GetValue(current));
        Assert.True(proxy.DropCount == 1,
            $"Commit fault not triggered. Attempts={observation.Attempts}; Errors={string.Join(",", errors)}; " +
            $"Outcome={failure.GetTransactionOutcome()}; Stack={failure.StackTrace}");
    }

    /// <summary>
    /// 从不经过故障代理的权威写库查询原请求结果。
    /// </summary>
    /// <param name="host">正常测试宿主。</param>
    /// <param name="connection">专用数据库的原连接字符串。</param>
    /// <param name="request">需要核对身份及内容的原请求。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>真实写库中已提交的首次整单结果。</returns>
    private static async Task<InventoryOrderResult> QueryCommittedInventoryAsync(IHost host, string connection,
        InventoryOrderRequest request, CancellationToken token)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await using var database = new MySqlConnection(connection);
        await database.OpenAsync(token);
        return await scope.ServiceProvider.GetRequiredService<IInventoryOrderStore>().FindAsync(database, request, token);
    }

    /// <summary>
    /// 使用故障代理运行独立的真实 Admin 工作单元宿主。
    /// </summary>
    /// <param name="connection">连接到本机确认丢失代理的专用测试数据库连接字符串。</param>
    /// <param name="audits">标记当前库存提交连接的审计存储。</param>
    /// <param name="action">使用真实执行器的验证操作。</param>
    private static async Task WithCommitLossHostAsync(string connection, CommitLossAudits audits,
        Func<IHost, Task> action)
    {
#if BING_ADMIN_EFCORE_TESTS
        var original = MySqlCapIntegrationEnvironment.Load();
        var settings = original.WithConnectionString(connection);
        using var host = CreateHost(settings, withInventory: true, configureInventory: audits.ConfigureServices);
#else
        var original = IntegrationSettings.Load();
        var settings = original.WithConnectionString(connection);
        using var orm = CreateOrm(connection);
        using var host = CreateHost(settings, orm, withInventory: true, configureInventory: audits.ConfigureServices);
#endif
        Exception failure = null;
        try
        {
            await host.StartAsync();
            await action(host);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            try
            {
                await host.StopAsync();
            }
            catch (Exception exception) when (failure != null)
            {
                failure.Data["Bing.TestCommitLossHostStopException"] = exception;
            }
        }
    }

    /// <summary>
    /// 在真实审计写入后标记唯一需要丢弃提交确认的库存连接。
    /// </summary>
    private sealed class CommitLossAudits : ITransactionalAuditingStore
    {
        /// <summary>
        /// 获取本宿主实际创建的整单存储数量。
        /// </summary>
        internal int Attempts { get; private set; }

        /// <summary>
        /// 注册真实存储及当前连接的确认丢失标记。
        /// </summary>
        /// <param name="services">当前故障测试宿主的服务集合。</param>
        internal void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITransactionalAuditingStore>(this);
            services.AddScoped<IInventoryOrderStore>(provider =>
            {
                Attempts++;
                return new DbInventoryOrderStore(InventoryDatabase.MySql, provider.GetRequiredService<IInventoryStore>());
            });
        }

        /// <inheritdoc />
        public async Task SaveAsync(DbTransaction transaction, string auditId, AuditLogInfo auditInfo,
            CancellationToken cancellationToken = default)
        {
            await new DbAuditingStore(AuditLogDatabase.MySql).SaveAsync(transaction, auditId, auditInfo, cancellationToken);
            await using var marker = transaction.Connection.CreateCommand();
            marker.Transaction = transaction;
            marker.CommandText = InventoryCommitAckLossProxy.MarkerCommand;
            await marker.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
