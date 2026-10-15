using System.Diagnostics;
using System.Text;
using Bing.Admin.Data.Inventory;
using Bing.Data.Inventory;
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
/// 验证真实 Admin 库存整单的跨进程竞争和中断恢复。
/// </summary>
public sealed partial class AdminMySqlCapIntegrationTest
{
    /// <summary>
    /// 验证两个独立进程竞争同单仅提交一次库存、审计和出箱。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenProcessesCompeteForSameOrder_ShouldCommitExactlyOnce()
    {
        await WithInventoryHostAsync(false, async (_, connection, tenant) =>
        {
            using var first = StartInventoryProbe("execute", tenant, "order-" + tenant, 3);
            using var second = StartInventoryProbe("execute", tenant, "order-" + tenant, 3);
            await Task.WhenAll(WaitForInventorySignalAsync(first, "READY"),
                WaitForInventorySignalAsync(second, "READY"));
            await ReleaseInventoryProbeAsync(first);
            await ReleaseInventoryProbeAsync(second);
            var results = await Task.WhenAll(ReadInventoryProbeResultAsync(first), ReadInventoryProbeResultAsync(second));
            Assert.All(results, result => Assert.Equal(InventoryDeductionStatus.Applied, result.Status));
            Assert.Single(results, result => result.Duplicate);
            Assert.Single(results, result => !result.Duplicate);
            Assert.Equal(2, results.Select(result => result.ProcessId).Distinct().Count());
            await AssertInventoryCommittedCountsAsync(connection, tenant, 14, 1);
        });
    }

    /// <summary>
    /// 验证不同进程的不同整单竞争有限库存时不超卖。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenProcessesCompeteForDifferentOrders_ShouldKeepStockAtomic()
    {
        await WithInventoryHostAsync(false, async (_, connection, tenant) =>
        {
            using var first = StartInventoryProbe("execute", tenant, "order-" + tenant + "-a", 7);
            using var second = StartInventoryProbe("execute", tenant, "order-" + tenant + "-b", 7);
            await Task.WhenAll(WaitForInventorySignalAsync(first, "READY"),
                WaitForInventorySignalAsync(second, "READY"));
            await ReleaseInventoryProbeAsync(first);
            await ReleaseInventoryProbeAsync(second);
            var results = await Task.WhenAll(ReadInventoryProbeResultAsync(first), ReadInventoryProbeResultAsync(second));
            Assert.Single(results, result => result.Status == InventoryDeductionStatus.Applied);
            Assert.Single(results, result => result.Status == InventoryDeductionStatus.Insufficient);
            Assert.Equal(2, results.Select(result => result.ProcessId).Distinct().Count());
            await AssertInventoryCommittedCountsAsync(connection, tenant, 6, 2);
        });
    }

    /// <summary>
    /// 验证关键审计写入后进程中断会回滚，原单重试仅提交一次。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenProcessDiesBeforeCommit_ShouldRollbackAndRetryOriginalOrder()
    {
        await WithInventoryHostAsync(false, async (host, connection, tenant) =>
        {
            using (var interrupted = StartInventoryProbe("before-commit", tenant, "order-" + tenant, 3))
            {
                await WaitForInventorySignalAsync(interrupted, "READY");
                await ReleaseInventoryProbeAsync(interrupted);
                await WaitForInventorySignalAsync(interrupted, "ACTIVE");
                await StopInventoryProbeAsync(interrupted.Process);
            }
            var result = await ExecuteInventoryAsync(host, InventoryRequest(tenant, 3, 3));
            Assert.True(result.Applied);
            Assert.False(result.Duplicate);
            await AssertInventoryCommittedCountsAsync(connection, tenant, 14, 1);
        });
    }

    /// <summary>
    /// 验证提交后进程中断可查询首次结果且重入不重复扣减。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task InventoryOrder_WhenProcessDiesAfterCommit_ShouldRecoverOriginalResultWithoutDuplicateWrites()
    {
        await WithInventoryHostAsync(false, async (host, connection, tenant) =>
        {
            using (var interrupted = StartInventoryProbe("after-commit", tenant, "order-" + tenant, 3))
            {
                await WaitForInventorySignalAsync(interrupted, "READY");
                await ReleaseInventoryProbeAsync(interrupted);
                await WaitForInventorySignalAsync(interrupted, "COMMITTED");
                await StopInventoryProbeAsync(interrupted.Process);
            }
            var request = InventoryRequest(tenant, 3, 3);
            await using var scope = host.Services.CreateAsyncScope();
            await using var query = new MySqlConnection(connection);
            await query.OpenAsync();
            var recovered = await scope.ServiceProvider.GetRequiredService<IInventoryOrderStore>().FindAsync(query, request);
            Assert.True(recovered.Duplicate);
            Assert.Equal(new long?[] { 7, 7 }, recovered.RemainingQuantities);
            var repeated = await ExecuteInventoryAsync(host, request);
            Assert.True(repeated.Duplicate);
            Assert.Equal(recovered.RemainingQuantities, repeated.RemainingQuantities);
            await AssertInventoryCommittedCountsAsync(connection, tenant, 14, 1);
        });
    }

    /// <summary>
    /// 验证库存余额及整单、审计、出箱的实际提交数量。
    /// </summary>
    /// <param name="connection">专用测试数据库的连接字符串。</param>
    /// <param name="tenant">本轮测试独占的租户标识。</param>
    /// <param name="remaining">两项库存余额的预期总和。</param>
    /// <param name="orders">整单、关键审计和出箱的预期提交数量。</param>
    private static async Task AssertInventoryCommittedCountsAsync(string connection, string tenant, long remaining,
        long orders)
    {
        Assert.Equal(remaining, await InventoryScalarAsync(connection,
            "SELECT SUM(Quantity) FROM bing_inventory_stock WHERE TenantId=@tenant", tenant));
        Assert.Equal(0, await InventoryScalarAsync(connection,
            "SELECT COUNT(*) FROM bing_inventory_stock WHERE TenantId=@tenant AND Quantity<0", tenant));
        Assert.Equal(orders, await CountInventoryOrdersAsync(connection, tenant));
        Assert.Equal(orders, await InventoryScalarAsync(connection,
            "SELECT COUNT(*) FROM bing_audit_log WHERE TenantId=@tenant", tenant));
        Assert.Equal(orders, await CountInventoryOutboxAsync(connection, tenant));
    }

    /// <summary>
    /// 启动当前测试所属的独立库存进程。
    /// </summary>
    /// <param name="mode">正常执行、提交前中断或提交后中断的探针模式。</param>
    /// <param name="tenant">本轮测试独占的租户标识。</param>
    /// <param name="order">竞争或恢复时使用的原始整单标识。</param>
    /// <param name="quantity">每项库存明细的扣减数量。</param>
    /// <returns>负责回收本轮测试子进程的探针包装对象。</returns>
    private static InventoryProbeProcess StartInventoryProbe(string mode, string tenant, string order, int quantity)
    {
#if BING_ADMIN_EFCORE_TESTS
        const string assemblyName = "Bing.Admin.EFCore.InventoryProbe.dll";
#else
        const string assemblyName = "Bing.Admin.FreeSQL.InventoryProbe.dll";
#endif
        var path = Path.Combine(AppContext.BaseDirectory, "InventoryProbe", assemblyName);
        Assert.True(File.Exists(path), "库存独立进程探针尚未复制。");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var value in new[] { path, mode, tenant, order, quantity.ToString(System.Globalization.CultureInfo.InvariantCulture) })
            start.ArgumentList.Add(value);
        return new InventoryProbeProcess(Process.Start(start) ?? throw new InvalidOperationException("库存探针启动失败。"));
    }

    /// <summary>
    /// 等待当前进程报告指定执行阶段。
    /// </summary>
    /// <param name="probe">当前测试独占的库存探针。</param>
    /// <param name="expected">表示探针已到达目标阶段的完整信号行。</param>
    /// <remarks>最多等待一百秒；探针提前退出时附加标准错误输出。</remarks>
    private static async Task WaitForInventorySignalAsync(InventoryProbeProcess probe, string expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        while (true)
        {
            var line = await probe.Process.StandardOutput.ReadLineAsync().WaitAsync(timeout.Token);
            if (line == expected)
                return;
            if (line == null)
                throw new InvalidOperationException("库存探针提前退出：" + await probe.Error);
        }
    }

    /// <summary>
    /// 放行已启动的库存探针。
    /// </summary>
    /// <param name="probe">等待启动信号的库存探针。</param>
    private static async Task ReleaseInventoryProbeAsync(InventoryProbeProcess probe)
    {
        await probe.Process.StandardInput.WriteLineAsync("GO");
        await probe.Process.StandardInput.FlushAsync();
    }

    /// <summary>
    /// 等待探针正常退出并读取真实业务结果。
    /// </summary>
    /// <param name="probe">已经开始执行业务的库存探针。</param>
    /// <returns>探针进程标识、整单处理状态及是否读取首次持久化结果。</returns>
    /// <remarks>最多等待一百秒；退出代码和结果行必须同时满足成功条件。</remarks>
    private static async Task<(int ProcessId, InventoryDeductionStatus Status, bool Duplicate)> ReadInventoryProbeResultAsync(
        InventoryProbeProcess probe)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        var output = await probe.Process.StandardOutput.ReadToEndAsync().WaitAsync(timeout.Token);
        await probe.Process.WaitForExitAsync(timeout.Token);
        Assert.True(probe.Process.ExitCode == 0, await probe.Error);
        var parts = output.Split('\n').Single(line => line.StartsWith("RESULT|", StringComparison.Ordinal)).Trim().Split('|');
        return (int.Parse(parts[1]), Enum.Parse<InventoryDeductionStatus>(parts[2]), bool.Parse(parts[3]));
    }

    /// <summary>
    /// 终止当前测试独占的探针并等待进程退出。
    /// </summary>
    /// <param name="process">当前测试创建并独占的探针进程。</param>
    /// <remarks>强制终止仅用于中断恢复测试和测试失败清理，不代表执行了托管资源释放。</remarks>
    private static async Task StopInventoryProbeAsync(Process process)
    {
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.WaitForExitAsync(timeout.Token);
    }

    /// <summary>
    /// 持有测试独占进程并确保失败路径不会遗留子进程。
    /// </summary>
    private sealed class InventoryProbeProcess : IDisposable
    {
        /// <summary>
        /// 初始化一个 <see cref="InventoryProbeProcess" /> 类型的实例。
        /// </summary>
        /// <param name="process">当前测试创建并独占的探针进程。</param>
        internal InventoryProbeProcess(Process process)
        {
            Process = process;
            Error = process.StandardError.ReadToEndAsync();
        }

        /// <summary>
        /// 获取当前测试独占的进程。
        /// </summary>
        internal Process Process { get; }

        /// <summary>
        /// 获取标准错误输出的异步读取任务。
        /// </summary>
        internal Task<string> Error { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            try { StopInventoryProbeAsync(Process).GetAwaiter().GetResult(); }
            finally { Process.Dispose(); }
        }
    }
}
