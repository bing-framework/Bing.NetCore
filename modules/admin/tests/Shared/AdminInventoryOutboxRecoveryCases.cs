using System.Globalization;
using System.Runtime.ExceptionServices;
using Bing.Admin.Data.Inventory;
using Bing.Data.Transaction;
using Bing.Events.Cap;
using DotNetCore.CAP;
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
/// 验证提交确认丢失后的真实 CAP 出箱后台恢复。
/// </summary>
public sealed partial class AdminMySqlCapIntegrationTest
{
    /// <summary>
    /// 验证原出箱记录由后台扫描投递至 RabbitMQ，且原订单恢复不会重复扣减库存。
    /// </summary>
    /// <remarks>只验证消息最终投递与库存单次写入，不将传输回调次数解释为严格一次投递保证。</remarks>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenCommitAcknowledgementIsLost_ShouldRecoverOutboxThroughBackgroundScan()
    {
        await WithInventoryHostAsync(false, async (normalHost, connection, tenant) =>
        {
            await using var proxy = new InventoryCommitAckLossProxy(connection);
            var observation = new CommitLossAudits();
            var request = InventoryRequest(tenant, 3, 3);
            await WithCommitLossHostAsync(proxy.ConnectionString, observation, async faultHost =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var failure = await Record.ExceptionAsync(() => faultHost.Services
                    .GetRequiredService<AdminInventoryOrderExecutor>().ExecuteAsync(request,
                        "test-user", "commit-loss-outbox-trace", timeout.Token));

                Assert.NotNull(failure);
                AssertCommitLossTriggered(proxy, observation, failure);
                await proxy.AcknowledgementDropped.WaitAsync(timeout.Token);
                Assert.Equal(1, proxy.DropCount);
                Assert.Equal(1, observation.Attempts);
                Assert.Equal(TransactionOutcome.Unknown, failure.GetTransactionOutcome());
            });

            // 两个原宿主保持默认回看窗口；故障宿主停止后，先记录尚未发送的真实出箱。
            using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var pending = await ReadInventoryRecoveryOutboxAsync(connection, request.OrderId, recoveryTimeout.Token);
            Assert.Equal("Scheduled", pending.Status);
            Assert.Equal(1, await CountInventoryOutboxAsync(connection, tenant));
            Assert.Equal(0, InventorySubscriberFor(normalHost).Deliveries(request.OrderId));

            await WithInventoryOutboxRecoveryHostAsync(connection, async () =>
            {
                await WaitForInventoryOutboxSucceededAsync(connection, request.OrderId, pending.Id,
                    recoveryTimeout.Token);
                await InventorySubscriberFor(normalHost).WaitAsync(request.OrderId);
                Assert.True(InventorySubscriberFor(normalHost).Deliveries(request.OrderId) > 0);

                var recovered = await QueryCommittedInventoryAsync(normalHost, connection, request,
                    recoveryTimeout.Token);
                Assert.True(recovered.Applied);
                Assert.True(recovered.Duplicate);
                Assert.Equal(new long?[] { 7, 7 }, recovered.RemainingQuantities);
                var repeated = await ExecuteInventoryAsync(normalHost, request, recoveryTimeout.Token);
                Assert.True(repeated.Duplicate);
                Assert.Equal(recovered.RemainingQuantities, repeated.RemainingQuantities);
                await AssertInventoryCommittedCountsAsync(connection, tenant, 14, 1);
                var completed = await ReadInventoryRecoveryOutboxAsync(connection, request.OrderId,
                    recoveryTimeout.Token);
                Assert.Equal(pending.Id, completed.Id);
                Assert.Equal("Succeeded", completed.Status);
            }, recoveryTimeout.Token);
        });
    }

    /// <summary>
    /// 从权威写库读取当前订单唯一出箱记录及其扫描状态。
    /// </summary>
    /// <param name="connectionString">不经过故障代理的专用测试数据库连接字符串。</param>
    /// <param name="orderId">由测试租户生成的唯一原订单标识。</param>
    /// <param name="token">查询取消令牌。</param>
    /// <returns>原出箱消息的数据库标识、状态及重试次数。</returns>
    private static async Task<(string Id, string Status, int Retries)> ReadInventoryRecoveryOutboxAsync(
        string connectionString, string orderId, CancellationToken token)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = "SELECT `Id`,`StatusName`,`Retries` FROM `cap.published` " +
                              "WHERE `Name`=@topic AND `Content` LIKE @order";
        command.Parameters.AddWithValue("@topic", AdminInventoryService.CompletedTopic);
        command.Parameters.AddWithValue("@order", "%" + orderId + "%");
        await using var reader = await command.ExecuteReaderAsync(token);
        Assert.True(await reader.ReadAsync(token), "原订单未保留真实 CAP 出箱记录。");
        var result = (reader.GetInt64(0).ToString(CultureInfo.InvariantCulture), reader.GetString(1), reader.GetInt32(2));
        Assert.False(await reader.ReadAsync(token), "原订单存在重复 CAP 出箱记录。");
        return result;
    }

    /// <summary>
    /// 有界等待原生 CAP 扫描把同一条原出箱记录标记为发送成功。
    /// </summary>
    /// <param name="connectionString">不经过故障代理的专用测试数据库连接字符串。</param>
    /// <param name="orderId">原订单标识。</param>
    /// <param name="messageId">启动恢复宿主前保存的原出箱数据库标识。</param>
    /// <param name="token">限制后台恢复等待时间的取消令牌。</param>
    private static async Task WaitForInventoryOutboxSucceededAsync(string connectionString, string orderId,
        string messageId, CancellationToken token)
    {
        while (true)
        {
            var current = await ReadInventoryRecoveryOutboxAsync(connectionString, orderId, token);
            Assert.Equal(messageId, current.Id);
            if (current.Status == "Succeeded")
                return;
            await Task.Delay(100, token);
        }
    }

    /// <summary>
    /// 启动直连写库的原生 CAP 恢复宿主，并保留验证和宿主清理失败。
    /// </summary>
    /// <param name="connectionString">原始专用测试数据库连接字符串。</param>
    /// <param name="action">验证原出箱恢复、实际消费与原请求重复执行不增加库存写入的操作。</param>
    /// <param name="token">宿主启动的取消令牌。</param>
    /// <remarks>停止与释放各尝试一次；清理异常附于原始失败，不覆盖原始失败。</remarks>
    private static async Task WithInventoryOutboxRecoveryHostAsync(string connectionString, Func<Task> action,
        CancellationToken token)
    {
        var host = CreateInventoryOutboxRecoveryHost(connectionString);
        Exception failure = null;
        try
        {
            await host.StartAsync(token);
            await action();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            try
            {
                using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await host.StopAsync(stopTimeout.Token);
            }
            catch (Exception exception)
            {
                if (failure == null)
                    failure = exception;
                else
                    failure.Data["Bing.TestOutboxRecoveryHostStopException"] = exception;
            }
            try
            {
                host.Dispose();
            }
            catch (Exception exception)
            {
                if (failure == null)
                    failure = exception;
                else
                    failure.Data["Bing.TestOutboxRecoveryHostDisposeException"] = exception;
            }
        }
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>
    /// 创建只负责原生后台扫描发布的真实 MySQL 与 RabbitMQ 恢复宿主。
    /// </summary>
    /// <param name="connectionString">不经过故障代理的原始测试数据库连接字符串。</param>
    /// <returns>尚未启动且由调用者负责停止与释放的恢复宿主。</returns>
    /// <remarks>不注册库存订阅者，原订单消息由已经就绪的正常宿主接收。</remarks>
    private static IHost CreateInventoryOutboxRecoveryHost(string connectionString)
    {
#if BING_ADMIN_EFCORE_TESTS
        var settings = MySqlCapIntegrationEnvironment.Load().WithConnectionString(connectionString);
#else
        var settings = IntegrationSettings.Load().WithConnectionString(connectionString);
#endif
        return new HostBuilder().ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddCapEventBus(options =>
            {
                options.UseMySql(settings.ConnectionString);
                options.UseRabbitMQ(rabbit =>
                {
                    rabbit.HostName = settings.RabbitHost;
                    rabbit.Port = settings.RabbitPort;
#if BING_ADMIN_EFCORE_TESTS
                    rabbit.UserName = settings.RabbitUserName;
#else
                    rabbit.UserName = settings.RabbitUser;
#endif
                    rabbit.Password = settings.RabbitPassword;
                });
                options.Version = "1.0";
                options.ConsumerThreadCount = 1;
                options.FailedRetryInterval = 1;
                options.FallbackWindowLookbackSeconds = 5;
            });
        }).Build();
    }
}
