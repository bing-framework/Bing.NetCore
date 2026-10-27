using System.Text.Json;
using Bing.Resilience.Compensation;
using Bing.Samples.ResilienceExtensions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class OrderWorkflowSampleTests
{
    [Fact]
    public async Task PersistedOrderSurvivesRestartConfirmsLostPaymentAndCompensatesInReverse()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var input = JsonSerializer.Serialize(new SqliteOrderWorkflow.Order("order-1", "sku-1", 3, 1900));
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.sample");
        var command = new SagaCommand("order-saga", operation.Name, "v1", "tenant", "same-key",
            new[] { "reserve", "pay", "ship" }.Select(step => new SagaStepDefinition(step, "order", "1", input)).ToArray(), DateTimeOffset.UtcNow.AddMinutes(5));
        var handler = new SqliteOrderWorkflow(database.ConnectionString, (step, action) => step == "pay" && action == SagaStepAction.Forward);
        await handler.InitializeAsync();
        DurableWorkflowHost Host(SqliteOrderWorkflow workflow)
        {
            var services = DurableWorkflowTestSupport.CreateServices(database, operation);
            services.AddBingSaga(o => { o.Authorize = (_, _) => ValueTask.FromResult(true); o.RetryDelay = TimeSpan.Zero; },
                new SagaHandlerRegistration("order", "1", _ => workflow));
            return DurableWorkflowTestSupport.Build(services);
        }
        using (var original = Host(handler))
        {
            await original.Services.GetRequiredService<ISagaCoordinator>().StartAsync(command);
            var worker = original.Services.GetRequiredService<SagaWorker>();
            await worker.RunOnceAsync(); await worker.RunOnceAsync();
            Assert.Equal("Unknown", (await original.Services.GetRequiredService<ISagaCoordinator>().GetAsync(command.SagaId, "tenant"))!.Steps[1].State);
        }
        // 重建处理器和 DI 容器，不复用内存业务状态。
        using (var restarted = Host(new SqliteOrderWorkflow(database.ConnectionString)))
        {
            var worker = restarted.Services.GetRequiredService<SagaWorker>();
            for (var i = 0; i < 8; i++) await worker.RunOnceAsync();
            Assert.Equal("Compensated", (await restarted.Services.GetRequiredService<ISagaCoordinator>().GetAsync(command.SagaId, "tenant"))!.Saga.State);
        }
        await using var connection = new SqliteConnection(database.ConnectionString);
        Assert.Equal(10, await connection.ExecuteScalarAsync<int>("SELECT available FROM demo_stock WHERE sku='sku-1'"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM demo_effects WHERE state='Compensated'"));
        Assert.Equal(new[] { "pay", "reserve" }, await connection.QueryAsync<string>("SELECT e.kind FROM demo_compensations c JOIN demo_effects e ON e.forward_key=c.forward_key ORDER BY c.rowid"));
    }

    [Fact]
    public async Task DuplicateForwardAndCompensationDoNotRepeatBusinessEffectsAndConfirmUsesCorrectLedger()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var handler = new SqliteOrderWorkflow(database.ConnectionString, (_, action) => action == SagaStepAction.Compensate);
        await handler.InitializeAsync();
        var step = new SagaStepDefinition("reserve", "order", "1", JsonSerializer.Serialize(new SqliteOrderWorkflow.Order("one", "sku-1", 2, 100)));
        var saga = new SagaCommand("saga", "order", "v1", "tenant", "key", new[] { step }, DateTimeOffset.UtcNow.AddHours(1));
        var context = new SagaStepContext(saga, step, "forward", "compensate", null, 1, SagaStepAction.Forward, null);
        Assert.Equal(SagaOutcome.Succeeded, (await handler.ForwardAsync(context, default)).Outcome);
        Assert.Equal(SagaOutcome.Succeeded, (await handler.ForwardAsync(context, default)).Outcome);
        Assert.Equal(SagaOutcome.Unknown, (await handler.CompensateAsync(context with { Action = SagaStepAction.Compensate }, default)).Outcome);
        Assert.Equal(SagaOutcome.Succeeded, (await new SqliteOrderWorkflow(database.ConnectionString).ConfirmAsync(context with { Action = SagaStepAction.ConfirmCompensation }, default)).Outcome);
        Assert.Equal(SagaOutcome.Succeeded, (await handler.CompensateAsync(context, default)).Outcome);
        var wrongKey = context with { CompensationKey = "changed-key", Action = SagaStepAction.ConfirmCompensation };
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await handler.CompensateAsync(wrongKey, default));
        Assert.Equal(SagaOutcome.Unknown, (await handler.ConfirmAsync(wrongKey, default)).Outcome);
        await using var connection = new SqliteConnection(database.ConnectionString);
        Assert.Equal(10, await connection.ExecuteScalarAsync<int>("SELECT available FROM demo_stock"));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM demo_compensations"));
        var changed = context with { Step = step with { Input = JsonSerializer.Serialize(new SqliteOrderWorkflow.Order("other", "sku-1", 2, 100)) } };
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await handler.ForwardAsync(changed, default));
        Assert.Equal(SagaOutcome.Unknown, (await handler.CompensateAsync(context with { ForwardKey = "missing" }, default)).Outcome);
    }
}
