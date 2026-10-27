using System.Collections.Concurrent;
using System.Data.Common;
using Bing.Resilience;
using Bing.Resilience.Compensation;
using Bing.Resilience.Operations;
using Bing.Resilience.Operations.Relational;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class SagaTests
{
    [Fact]
    public async Task ForwardRunsInOrderAndFailureCompensatesInReverseOrder()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("saga.reverse-order");
        var calls = new List<string>();
        var handler = new DelegateSagaHandler(
            (context, _) =>
            {
                calls.Add("forward:" + context.Step.Id);
                return ValueTask.FromResult(new SagaStepResult(context.Step.Id == "reserve" ? SagaOutcome.FailedNoEffect : SagaOutcome.Succeeded));
            },
            (context, _) =>
            {
                calls.Add("confirm:" + context.Step.Id);
                return ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
            },
            (context, _) =>
            {
                calls.Add("compensate:" + context.Step.Id);
                return ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
            });
        using var host = CreateSagaHost(database, operation, handler);
        var command = Command("reverse-order", operation.Name,
            Step("charge", "charge"), Step("reserve", "reserve"));
        await host.Services.GetRequiredService<ISagaCoordinator>().StartAsync(command);
        var worker = host.Services.GetRequiredService<SagaWorker>();

        Assert.True(await worker.RunOnceAsync());
        Assert.True(await worker.RunOnceAsync());
        Assert.True(await worker.RunOnceAsync());
        Assert.True(await worker.RunOnceAsync());

        Assert.Equal(new[] { "forward:charge", "forward:reserve", "compensate:charge" }, calls);
        var snapshot = await host.Services.GetRequiredService<ISagaCoordinator>().GetAsync(command.SagaId, command.AccessScope);
        Assert.Equal("Compensated", snapshot!.Saga.State);
        Assert.Equal(new[] { "Compensated", "Failed" }, snapshot.Steps.Select(x => x.State));
    }

    [Fact]
    public async Task UnknownForwardIsConfirmedAfterRestartBeforeAnyDuplicateForward()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("saga.restart-confirm");
        var calls = new List<SagaStepAction>();
        var command = Command("restart-confirm", operation.Name, Step("write", "orders"));
        using (var firstProcess = CreateSagaHost(database, operation, new DelegateSagaHandler(
                   (context, _) =>
                   {
                       calls.Add(context.Action);
                       return ValueTask.FromResult(new SagaStepResult(SagaOutcome.Unknown, Reason: "response_lost"));
                   })))
        {
            await firstProcess.Services.GetRequiredService<ISagaCoordinator>().StartAsync(command);
            await firstProcess.Services.GetRequiredService<SagaWorker>().RunOnceAsync();
            var saved = await firstProcess.Services.GetRequiredService<ISagaCoordinator>().GetAsync(command.SagaId, command.AccessScope);
            Assert.Equal("Unknown", Assert.Single(saved!.Steps).State);
            Assert.Equal("AwaitingConfirmation", saved.Saga.State);
        }

        using var restarted = CreateSagaHost(database, operation, new DelegateSagaHandler(
            (context, _) =>
            {
                calls.Add(context.Action);
                return ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
            }, confirm: (context, _) =>
            {
                calls.Add(context.Action);
                return ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
            }), rotatedKeyRing: true);
        Assert.True(await restarted.Services.GetRequiredService<SagaWorker>().RunOnceAsync());

        Assert.Equal(new[] { SagaStepAction.Forward, SagaStepAction.ConfirmForward }, calls);
        var recovered = await restarted.Services.GetRequiredService<ISagaCoordinator>().GetAsync(command.SagaId, command.AccessScope);
        Assert.Equal("Succeeded", Assert.Single(recovered!.Steps).State);
        Assert.Equal(2, recovered.Saga.Attempts);
    }

    [Fact]
    public async Task CorruptSagaJournalMovesParentToManualInterventionWithoutRunningSteps()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("saga.corrupt-journal");
        var calls = 0;
        using var host = CreateSagaHost(database, operation, new DelegateSagaHandler((_, _) =>
        {
            calls++;
            return ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
        }));
        var coordinator = host.Services.GetRequiredService<ISagaCoordinator>();
        var store = host.Services.GetRequiredService<IResilienceOperationStore>();
        var command = Command("corrupt-journal", operation.Name, Step("write", "orders"));
        await coordinator.StartAsync(command);

        await using (var transaction = await store.BeginAsync())
        {
            var saved = (await transaction.FindAsync(command.SagaId))!;
            Assert.True(await transaction.CompareExchangeAsync(saved with { Data = "v1.corrupt", Version = saved.Version + 1 }, saved.Version));
            await transaction.CommitAsync();
        }

        Assert.True(await host.Services.GetRequiredService<SagaWorker>().RunOnceAsync());
        await using var read = await store.BeginAsync();
        var manual = await read.FindAsync(command.SagaId);
        Assert.Equal("ManualIntervention", manual!.State);
        Assert.Equal(0, manual.Attempts);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task PartialEffectDuringConfirmationTriggersCompensation()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("saga.partial-effect");
        var calls = new List<SagaStepAction>();
        var handler = new DelegateSagaHandler(
            (context, _) =>
            {
                calls.Add(context.Action);
                return ValueTask.FromResult(new SagaStepResult(SagaOutcome.Unknown));
            },
            confirm: (context, _) =>
            {
                calls.Add(context.Action);
                return ValueTask.FromResult(new SagaStepResult(SagaOutcome.PartialEffect));
            },
            compensate: (context, _) =>
            {
                calls.Add(context.Action);
                return ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
            });
        using var host = CreateSagaHost(database, operation, handler);
        var command = Command("partial-effect", operation.Name, Step("create", "orders"));
        await host.Services.GetRequiredService<ISagaCoordinator>().StartAsync(command);
        var worker = host.Services.GetRequiredService<SagaWorker>();

        await worker.RunOnceAsync();
        var afterUnknown = await host.Services.GetRequiredService<ISagaCoordinator>().GetAsync(command.SagaId, command.AccessScope);
        Assert.Equal("Unknown", Assert.Single(afterUnknown!.Steps).State);
        await worker.RunOnceAsync();
        var afterPartial = await host.Services.GetRequiredService<ISagaCoordinator>().GetAsync(command.SagaId, command.AccessScope);
        Assert.Equal("Succeeded", Assert.Single(afterPartial!.Steps).State);
        Assert.Equal("Compensating", afterPartial.Saga.State);
        await worker.RunOnceAsync();
        await worker.RunOnceAsync();

        Assert.Equal(new[] { SagaStepAction.Forward, SagaStepAction.ConfirmForward, SagaStepAction.Compensate }, calls);
        var saved = await host.Services.GetRequiredService<ISagaCoordinator>().GetAsync(command.SagaId, command.AccessScope);
        Assert.Equal("Compensated", saved!.Saga.State);
        Assert.Equal("Compensated", Assert.Single(saved.Steps).State);
    }

    [Fact]
    public async Task FailedCompensationRequiresVersionCheckedManualRecovery()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("saga.manual-compensation");
        var handler = new DelegateSagaHandler(
            (_, _) => ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded)),
            compensate: (_, _) => ValueTask.FromResult(new SagaStepResult(SagaOutcome.FailedNoEffect, Reason: "refund_declined")));
        using var host = CreateSagaHost(database, operation, handler);
        var coordinator = host.Services.GetRequiredService<ISagaCoordinator>();
        var command = Command("manual-compensation", operation.Name, Step("refund", "payment"));
        await coordinator.StartAsync(command);
        await host.Services.GetRequiredService<SagaWorker>().RunOnceAsync();
        var beforeManual = await coordinator.GetAsync(command.SagaId, command.AccessScope);
        await coordinator.ApplyManualAsync(command.SagaId, "refund", command.AccessScope, beforeManual!.Saga.Version,
            SagaManualAction.Compensate, "operator-1", "start refund recovery");
        await host.Services.GetRequiredService<SagaWorker>().RunOnceAsync();
        var failed = await coordinator.GetAsync(command.SagaId, command.AccessScope);

        Assert.Equal("ManualIntervention", failed!.Saga.State);
        Assert.Equal("CompensationFailed", Assert.Single(failed.Steps).State);
        await coordinator.ApplyManualAsync(command.SagaId, "refund", command.AccessScope, failed.Saga.Version,
            SagaManualAction.Retry, "operator-1", "retry after provider reconciliation");
        var retried = await coordinator.GetAsync(command.SagaId, command.AccessScope);
        Assert.Equal("CompensationRetryScheduled", Assert.Single(retried!.Steps).State);
        Assert.Equal(failed.Saga.Version + 1, retried.Saga.Version);

        await Assert.ThrowsAsync<OperationConcurrencyException>(() => coordinator.ApplyManualAsync(command.SagaId, "refund",
            command.AccessScope, failed.Saga.Version, SagaManualAction.Retry, "operator-1", "stale retry").AsTask());
    }

    [Fact]
    public async Task CompensationDagRejectsCyclesAndMissingDependencies()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("saga.invalid-dag");
        using var host = CreateSagaHost(database, operation, new DelegateSagaHandler((_, _) =>
            ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded))));
        var coordinator = host.Services.GetRequiredService<ISagaCoordinator>();
        var cycle = Command("dag-cycle", operation.Name,
            Step("a", "handler", compensateAfter: new[] { "b" }),
            Step("b", "handler", compensateAfter: new[] { "a" })) with { UseCompensationDag = true };
        var missing = Command("dag-missing", operation.Name,
            Step("a", "handler", compensateAfter: new[] { "missing" })) with { UseCompensationDag = true };

        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => coordinator.StartAsync(cycle).AsTask());
        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => coordinator.StartAsync(missing).AsTask());
        Assert.Null(await coordinator.GetAsync(cycle.SagaId, cycle.AccessScope));
        Assert.Null(await coordinator.GetAsync(missing.SagaId, missing.AccessScope));
    }

    [Fact]
    public async Task IndependentDagCompensationsRunConcurrentlyWithinConfiguredLimit()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("saga.parallel-dag");
        var active = 0;
        var peak = 0;
        var entered = 0;
        var bothEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new DelegateSagaHandler(
            (context, _) => ValueTask.FromResult(new SagaStepResult(
                context.Step.Id == "fail" ? SagaOutcome.FailedNoEffect : SagaOutcome.Succeeded)),
            compensate: async (context, _) =>
            {
                var current = Interlocked.Increment(ref active);
                UpdateMaximum(ref peak, current);
                if (Interlocked.Increment(ref entered) == 2) bothEntered.TrySetResult(true);
                try
                {
                    await release.Task;
                    return new SagaStepResult(SagaOutcome.Succeeded);
                }
                finally { Interlocked.Decrement(ref active); }
            });
        using var host = CreateSagaHost(database, operation, handler, maxParallelCompensations: 2);
        var coordinator = host.Services.GetRequiredService<ISagaCoordinator>();
        var command = Command("parallel-dag", operation.Name,
            Step("a", "handler"), Step("b", "handler"), Step("fail", "handler")) with { UseCompensationDag = true };
        await coordinator.StartAsync(command);
        var worker = host.Services.GetRequiredService<SagaWorker>();
        await worker.RunOnceAsync();
        await worker.RunOnceAsync();
        await worker.RunOnceAsync();

        var compensationRun = worker.RunOnceAsync().AsTask();
        try
        {
            var enteredResult = await Task.WhenAny(bothEntered.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(bothEntered.Task, enteredResult);
            Assert.Equal(2, Volatile.Read(ref peak));
        }
        finally { release.TrySetResult(true); }
        Assert.True(await compensationRun);

        var saved = await coordinator.GetAsync(command.SagaId, command.AccessScope);
        Assert.Equal(new[] { "Compensated", "Compensated", "Failed" }, saved!.Steps.Select(x => x.State));
    }

    [Fact]
    public async Task UsesLocalTransactionCommitsBusinessWriteWithSagaStepState()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("saga.local-transaction");
        var handler = new LocalTransactionSagaHandler();
        using var host = CreateSagaHost(database, operation, handler, usesLocalTransaction: true);
        var command = Command("local-transaction", operation.Name, Step("write", "local"));
        var coordinator = host.Services.GetRequiredService<ISagaCoordinator>();
        await coordinator.StartAsync(command);

        await host.Services.GetRequiredService<SagaWorker>().RunOnceAsync();

        var saved = await coordinator.GetAsync(command.SagaId, command.AccessScope);
        Assert.Equal(1, await database.CountBusinessRowsAsync());
        Assert.Equal("Succeeded", Assert.Single(saved!.Steps).State);
        Assert.True(handler.ObservedOwnedTransaction);
        Assert.True(handler.ObservedSharedConnectionAndTransaction);
    }

    [Fact]
    public async Task ManualRetryAfterBudgetExhaustionGrantsOneAttemptWithoutResettingLedger()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("saga.manual-budget");
        var sends = 0;
        using var host = CreateSagaHost(database, operation, new DelegateSagaHandler((_, _) =>
            ValueTask.FromResult(new SagaStepResult(++sends == 1 ? SagaOutcome.RetryableNoEffect : SagaOutcome.Succeeded))), maxAttempts: 1);
        var coordinator = host.Services.GetRequiredService<ISagaCoordinator>();
        var worker = host.Services.GetRequiredService<SagaWorker>();
        var command = Command("manual-budget", operation.Name, Step("write", "orders"));
        await coordinator.StartAsync(command);
        await worker.RunOnceAsync();
        await worker.RunOnceAsync();
        var exhausted = (await coordinator.GetAsync(command.SagaId, command.AccessScope))!;
        Assert.Equal("ManualIntervention", exhausted.Saga.State);
        Assert.Equal(1, exhausted.Steps.Single().Attempts);
        await coordinator.ApplyManualAsync(command.SagaId, "write", command.AccessScope, exhausted.Saga.Version,
            SagaManualAction.Retry, "operator-1", "confirmed no prior side effect");
        await worker.RunOnceAsync();
        await worker.RunOnceAsync();
        var saved = (await coordinator.GetAsync(command.SagaId, command.AccessScope))!;
        Assert.Equal("Completed", saved.Saga.State);
        Assert.Equal(2, sends);
        Assert.Equal(2, saved.Steps.Single().Attempts);
        Assert.Equal(2, saved.Saga.Attempts);
    }

    private static DurableWorkflowHost CreateSagaHost(DurableSqliteDatabase database, ResilienceOperationDescriptor operation,
        ISagaStepHandler handler, bool usesLocalTransaction = false, int maxParallelCompensations = 4, bool rotatedKeyRing = false, int maxAttempts = 10)
    {
        var services = DurableWorkflowTestSupport.CreateServices(database, operation, rotatedKeyRing: rotatedKeyRing);
        services.AddBingSaga(options =>
        {
            options.Authorize = (_, _) => ValueTask.FromResult(true);
            options.RetryDelay = TimeSpan.Zero;
            options.MaxParallelCompensations = maxParallelCompensations;
            options.MaxAttemptsPerStep = maxAttempts;
        }, DurableWorkflowTestSupport.SagaHandler("handler", "1", handler, usesLocalTransaction));
        return DurableWorkflowTestSupport.Build(services);
    }

    private static SagaCommand Command(string id, string operation, params SagaStepDefinition[] steps) =>
        new(id, operation, "v1", "tenant-a", "stable-key", steps, DateTimeOffset.UtcNow.AddHours(1));

    private static SagaStepDefinition Step(string id, string input, IReadOnlyList<string>? compensateAfter = null) =>
        new(id, "handler", "1", input, CanCompensate: true, CompensateAfter: compensateAfter);

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        int current;
        while (candidate > (current = Volatile.Read(ref maximum)) && Interlocked.CompareExchange(ref maximum, candidate, current) != current) { }
    }

    private sealed class DelegateSagaHandler(
        Func<SagaStepContext, CancellationToken, ValueTask<SagaStepResult>> forward,
        Func<SagaStepContext, CancellationToken, ValueTask<SagaStepResult>>? confirm = null,
        Func<SagaStepContext, CancellationToken, ValueTask<SagaStepResult>>? compensate = null) : ISagaStepHandler
    {
        public ValueTask<SagaStepResult> ForwardAsync(SagaStepContext context, CancellationToken token) => forward(context, token);
        public ValueTask<SagaStepResult> ConfirmAsync(SagaStepContext context, CancellationToken token) =>
            confirm is null ? ValueTask.FromResult(new SagaStepResult(SagaOutcome.Unknown)) : confirm(context, token);
        public ValueTask<SagaStepResult> CompensateAsync(SagaStepContext context, CancellationToken token) =>
            compensate is null ? ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded)) : compensate(context, token);
    }

    private sealed class LocalTransactionSagaHandler : ISagaStepHandler
    {
        public bool ObservedOwnedTransaction { get; private set; }
        public bool ObservedSharedConnectionAndTransaction { get; private set; }

        public async ValueTask<SagaStepResult> ForwardAsync(SagaStepContext context, CancellationToken token)
        {
            var transaction = Assert.IsAssignableFrom<IRelationalOperationTransaction>(context.Transaction);
            ObservedOwnedTransaction = transaction.OwnsTransaction;
            ObservedSharedConnectionAndTransaction = ReferenceEquals(transaction.Transaction.Connection, transaction.Connection);
            await using var command = transaction.Connection.CreateCommand();
            command.Transaction = transaction.Transaction;
            command.CommandText = "INSERT INTO business_rows(value) VALUES ($value)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$value";
            parameter.Value = "saga-local-write";
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync(token);
            return new SagaStepResult(SagaOutcome.Succeeded, "written");
        }

        public ValueTask<SagaStepResult> ConfirmAsync(SagaStepContext context, CancellationToken token) =>
            ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));

        public ValueTask<SagaStepResult> CompensateAsync(SagaStepContext context, CancellationToken token) =>
            ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
    }
}
