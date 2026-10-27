using System.Data.Common;
using System.Text.Json;
using Bing.Samples.ResilienceExtensions;
using System.Security.Cryptography;
using Bing.Resilience;
using Bing.Resilience.Operations;
using Bing.Resilience.Operations.Relational;
using Bing.Resilience.Replay;
using Bing.Resilience.Compensation;
using Microsoft.Data.Sqlite;

/// <summary>能力3/6：SQLite Journal 的持久重放、未知结果确认和逆序业务补偿。</summary>
internal static class DurableDemo
{
    public static async Task Run()
    {
        var path = Path.Combine(Path.GetTempPath(), "bing-extension-demo-" + Guid.NewGuid().ToString("N") + ".db");
        var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        var store = new RelationalOperationStore(_ => ValueTask.FromResult<DbConnection>(new SqliteConnection(connection)), ResilienceRelationalDialect.Sqlite);
        await store.InitializeAsync(); // 仅示例库；生产由迁移流程明确创建。
        using var protector = new AesGcmOperationDataProtector("demo", new Dictionary<string, byte[]> { ["demo"] = RandomNumberGenerator.GetBytes(32) });
        try
        {
            var operation = new ResilienceOperationDescriptor("orders.create", "v1", ResilienceOperationKind.IdempotentWrite, "orders");
            var services = new ServiceCollection(); services.AddLogging();
            services.AddBingResilienceOperations(operation);
            services.AddSingleton<IResilienceOperationStore>(store); services.AddSingleton<IOperationDataProtector>(protector);
            var handler = new ReplayHandler();
            var responseLost = false;
            var orderHandler = new SqliteOrderWorkflow(connection, (step, action) =>
            {
                if (step == "pay" && action == SagaStepAction.Forward && !responseLost) { responseLost = true; return true; }
                return false;
            });
            await orderHandler.InitializeAsync();
            services.AddBingDurableReplay(o => { o.Authorize = (_, _) => ValueTask.FromResult(true); o.RetryDelay = TimeSpan.Zero; },
                new ReplayHandlerRegistration("create", "1", operation.Name, _ => handler));
            services.AddBingSaga(o => { o.Authorize = (_, _) => ValueTask.FromResult(true); o.RetryDelay = TimeSpan.Zero; },
                new SagaHandlerRegistration("order-step", "1", _ => orderHandler));
            await using var host = services.BuildServiceProvider();
            var replay = host.GetRequiredService<IReplayCoordinator>();
            var receipt = await replay.EnqueueAsync(new("replay-demo", operation.Name, "v1", "create", "1", "tenant-demo",
                "replay-key", "{\"sku\":1}", DateTimeOffset.UtcNow.AddMinutes(5)));
            var worker = host.GetRequiredService<ReplayWorker>();
            await worker.RunOnceAsync(); // 远端可能已提交，进入 Unknown。
            await worker.RunOnceAsync(); // Confirm 查询同键结果，不执行第二次 Forward。
            if ((await replay.GetAsync(receipt.OperationId, "tenant-demo"))!.State != "Succeeded" || handler.Writes != 1) throw new InvalidOperationException();
            var saga = host.GetRequiredService<ISagaCoordinator>();
            await saga.StartAsync(new("saga-demo", operation.Name, "v1", "tenant-demo", "saga-key",
                new[] { "reserve", "pay", "ship" }.Select(step => new SagaStepDefinition(step, "order-step", "1", JsonSerializer.Serialize(new SqliteOrderWorkflow.Order("order-1", "sku-1", 2, 1500)))).ToArray(),
                DateTimeOffset.UtcNow.AddMinutes(5)));
            var sagaWorker = host.GetRequiredService<SagaWorker>();
            for (var i = 0; i < 8; i++) await sagaWorker.RunOnceAsync();
            var snapshot = (await saga.GetAsync("saga-demo", "tenant-demo"))!;
            if (snapshot.Saga.State != "Compensated") throw new InvalidOperationException(snapshot.Saga.State);
            Console.WriteLine($"Replay={receipt.OperationId}:Succeeded，写入次数={handler.Writes}；Saga={snapshot.Saga.State}");
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }
        // 演示结束清理专属临时库。真实服务必须持久保管 Journal 和加密历史密钥。
    }
    private sealed class ReplayHandler : IReplayCommandHandler
    {
        internal int Writes;
        public ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext context, CancellationToken token)
        { Writes++; return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Unknown, Reason: "响应丢失")); }
        public ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext context, CancellationToken token) =>
            ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded, "同键查询已提交"));
    }
}
