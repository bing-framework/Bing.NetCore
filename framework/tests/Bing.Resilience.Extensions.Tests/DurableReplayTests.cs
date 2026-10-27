using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Bing.Http.Resilience;
using Bing.Http.Resilience.Replay;
using Bing.Resilience;
using Bing.Resilience.Operations;
using Bing.Resilience.Replay;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class DurableReplayTests
{
    [Fact]
    public async Task EnqueueReturnsVerifiedCommitAndCallerTransactionControlsVisibility()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.durable-enqueue");
        var services = DurableWorkflowTestSupport.CreateServices(database, operation);
        services.AddBingDurableReplay(options => options.Authorize = (_, _) => ValueTask.FromResult(true),
            DurableWorkflowTestSupport.ReplayHandler("orders", "1", operation.Name, new NoopReplayHandler()));
        using var host = DurableWorkflowTestSupport.Build(services);
        var coordinator = host.Services.GetRequiredService<IReplayCoordinator>();
        var store = host.Services.GetRequiredService<IResilienceOperationStore>();
        var command = Command("durable-proof", operation.Name, "payload-that-must-be-encrypted");

        var receipt = await coordinator.EnqueueAsync(command);

        Assert.Equal(command.OperationId, receipt.OperationId);
        Assert.Equal(command.Deadline.ToUnixTimeMilliseconds(), receipt.Deadline.ToUnixTimeMilliseconds());
        var committed = await FindAsync(store, command.OperationId);
        Assert.NotNull(committed);
        Assert.Equal("Pending", committed!.State);
        Assert.DoesNotContain(command.Payload, committed.Data, StringComparison.Ordinal);

        var rollbackCommand = Command("durable-rollback", operation.Name, "rollback-payload");
        await using (var transaction = await store.BeginAsync())
        {
            await coordinator.EnqueueInTransactionAsync(transaction, rollbackCommand);
        }

        Assert.Null(await FindAsync(store, rollbackCommand.OperationId));
    }

    [Fact]
    public async Task UnknownAttemptIsConfirmedBeforeAnyForwardCanRepeat()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.confirm-before-replay");
        var calls = new List<string>();
        var handler = new DelegateReplayHandler(
            (context, _) =>
            {
                calls.Add($"execute:{context.Attempt}:{context.Command.IdempotencyKey}");
                return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Unknown, Reason: "response_lost"));
            },
            (context, _) =>
            {
                calls.Add($"confirm:{context.Attempt}:{context.Command.IdempotencyKey}");
                return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded, "result-1"));
            });
        var services = DurableWorkflowTestSupport.CreateServices(database, operation);
        services.AddBingDurableReplay(options => { options.Authorize = (_, _) => ValueTask.FromResult(true); options.RetryDelay = TimeSpan.Zero; },
            DurableWorkflowTestSupport.ReplayHandler("orders", "1", operation.Name, handler));
        using var host = DurableWorkflowTestSupport.Build(services);
        var coordinator = host.Services.GetRequiredService<IReplayCoordinator>();
        var command = Command("confirm-first", operation.Name, "payload");
        await coordinator.EnqueueAsync(command);
        var worker = host.Services.GetRequiredService<ReplayWorker>();

        Assert.True(await worker.RunOnceAsync());
        Assert.Equal("Unknown", (await FindAsync(host.Services.GetRequiredService<IResilienceOperationStore>(), command.OperationId))!.State);
        Assert.True(await worker.RunOnceAsync());

        Assert.Equal(new[] { "execute:1:confirm-first-key", "confirm:2:confirm-first-key" }, calls);
        var saved = await FindAsync(host.Services.GetRequiredService<IResilienceOperationStore>(), command.OperationId);
        Assert.Equal("Succeeded", saved!.State);
        Assert.Equal(2, saved.Attempts);
        Assert.Equal(new[] { "Unknown", "Succeeded" }, await AttemptOutcomesAsync(host.Services.GetRequiredService<IResilienceOperationStore>(), command.OperationId));
    }

    [Fact]
    public async Task UnknownConfirmationCanRepeatOnlyWhenSameKeyRecoveryIsRegistered()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.same-key-recovery");
        var calls = new List<(bool Confirm, string Key)>();
        var handler = new DelegateReplayHandler(
            (context, _) =>
            {
                calls.Add((false, context.Command.IdempotencyKey));
                return ValueTask.FromResult(new ReplayHandlerResult(calls.Count == 1 ? ReplayOutcome.Unknown : ReplayOutcome.Succeeded));
            },
            (context, _) =>
            {
                calls.Add((true, context.Command.IdempotencyKey));
                return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Unknown, Reason: "still_unknown"));
            });
        var services = DurableWorkflowTestSupport.CreateServices(database, operation);
        services.AddBingDurableReplay(options => { options.Authorize = (_, _) => ValueTask.FromResult(true); options.RetryDelay = TimeSpan.Zero; },
            DurableWorkflowTestSupport.ReplayHandler("orders", "1", operation.Name, handler, allowSameKeyRecovery: true));
        using var host = DurableWorkflowTestSupport.Build(services);
        var coordinator = host.Services.GetRequiredService<IReplayCoordinator>();
        var command = Command("same-key-recovery", operation.Name, "payload");
        await coordinator.EnqueueAsync(command);
        var worker = host.Services.GetRequiredService<ReplayWorker>();

        await worker.RunOnceAsync();
        await worker.RunOnceAsync();
        var afterConfirmation = await FindAsync(host.Services.GetRequiredService<IResilienceOperationStore>(), command.OperationId);
        Assert.Equal("RetryScheduled", afterConfirmation!.State);
        await worker.RunOnceAsync();

        Assert.Equal(new[] { (false, "same-key-recovery-key"), (true, "same-key-recovery-key"), (false, "same-key-recovery-key") }, calls);
        Assert.Equal("Succeeded", (await FindAsync(host.Services.GetRequiredService<IResilienceOperationStore>(), command.OperationId))!.State);
    }

    [Fact]
    public async Task AttemptLimitExpiresWithoutAnExtraHandlerCall()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.attempt-limit");
        var calls = 0;
        var services = DurableWorkflowTestSupport.CreateServices(database, operation);
        services.AddBingDurableReplay(options =>
        {
            options.Authorize = (_, _) => ValueTask.FromResult(true);
            options.MaxAttempts = 1;
            options.RetryDelay = TimeSpan.Zero;
        }, DurableWorkflowTestSupport.ReplayHandler("orders", "1", operation.Name,
            new DelegateReplayHandler((_, _) =>
            {
                calls++;
                return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.RetryableNoEffect));
            })));
        using var host = DurableWorkflowTestSupport.Build(services);
        var coordinator = host.Services.GetRequiredService<IReplayCoordinator>();
        var command = Command("attempt-limit", operation.Name, "payload");
        await coordinator.EnqueueAsync(command);
        var worker = host.Services.GetRequiredService<ReplayWorker>();

        await worker.RunOnceAsync();
        await worker.RunOnceAsync();

        var saved = await FindAsync(host.Services.GetRequiredService<IResilienceOperationStore>(), command.OperationId);
        Assert.Equal("Expired", saved!.State);
        Assert.Equal(1, saved.Attempts);
        Assert.Equal(1, calls);
        Assert.Single(await AttemptsAsync(host.Services.GetRequiredService<IResilienceOperationStore>(), command.OperationId));
    }

    [Fact]
    public async Task ExpiredDeadlineDoesNotStartForwardAttempt()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var database = await DurableSqliteDatabase.CreateAsync(clock);
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.deadline");
        var calls = 0;
        var services = DurableWorkflowTestSupport.CreateServices(database, operation, clock);
        services.AddBingDurableReplay(options => options.Authorize = (_, _) => ValueTask.FromResult(true),
            DurableWorkflowTestSupport.ReplayHandler("orders", "1", operation.Name,
                new DelegateReplayHandler((_, _) =>
                {
                    calls++;
                    return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
                })));
        using var host = DurableWorkflowTestSupport.Build(services);
        var coordinator = host.Services.GetRequiredService<IReplayCoordinator>();
        var command = Command("deadline-expired", operation.Name, "payload", clock.GetUtcNow().AddMinutes(1));
        await coordinator.EnqueueAsync(command);
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.True(await host.Services.GetRequiredService<ReplayWorker>().RunOnceAsync());

        var saved = await FindAsync(host.Services.GetRequiredService<IResilienceOperationStore>(), command.OperationId);
        Assert.Equal("Expired", saved!.State);
        Assert.Equal(0, saved.Attempts);
        Assert.Equal(0, calls);
        Assert.Empty(await AttemptsAsync(host.Services.GetRequiredService<IResilienceOperationStore>(), command.OperationId));
    }

    [Fact]
    public async Task ExpiredOldLeaseCannotOverwriteTheSuccessorWorkerState()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.stale-worker");
        var calls = 0;
        var services = DurableWorkflowTestSupport.CreateServices(database, operation);
        services.AddBingDurableReplay(options =>
        {
            options.Authorize = (_, _) => ValueTask.FromResult(true);
            options.LeaseDuration = TimeSpan.FromMilliseconds(300);
            options.RetryDelay = TimeSpan.Zero;
        }, DurableWorkflowTestSupport.ReplayHandler("orders", "1", operation.Name,
            new DelegateReplayHandler((_, _) =>
            {
                calls++;
                return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
            })));
        using var host = DurableWorkflowTestSupport.Build(services);
        var store = host.Services.GetRequiredService<IResilienceOperationStore>();
        var coordinator = host.Services.GetRequiredService<IReplayCoordinator>();
        var command = Command("stale-worker", operation.Name, "payload");
        await coordinator.EnqueueAsync(command);
        OperationRecord staleClaim;
        await using (var transaction = await store.BeginAsync())
        {
            staleClaim = (await transaction.ClaimDueAsync("replay", new[] { "Pending" }, TimeSpan.FromMilliseconds(300)))!;
            Assert.NotNull(staleClaim);
            await transaction.CommitAsync();
        }
        await Task.Delay(TimeSpan.FromMilliseconds(400));

        Assert.True(await host.Services.GetRequiredService<ReplayWorker>().RunOnceAsync());
        await using var staleWriter = await store.BeginAsync();
        var staleUpdate = staleClaim with { State = "StaleWorkerWrite", Version = staleClaim.Version + 1 };
        Assert.False(await staleWriter.CompareExchangeAsync(staleUpdate, staleClaim.Version, staleClaim.LeaseToken));
        var saved = await staleWriter.FindAsync(command.OperationId);

        Assert.Equal("Succeeded", saved!.State);
        Assert.Equal(1, saved.Attempts);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RevokedAuthorizationMovesHistoricalCommandToManualIntervention()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.revoked");
        var originalCalls = 0;
        using (var original = CreateReplayHost(database, operation, new DelegateReplayHandler((_, _) =>
               {
                   originalCalls++;
                   return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
               }), authorize: true))
            await original.Services.GetRequiredService<IReplayCoordinator>().EnqueueAsync(Command("revoked", operation.Name, "payload"));

        var currentCalls = 0;
        using var restarted = CreateReplayHost(database, operation, new DelegateReplayHandler((_, _) =>
        {
            currentCalls++;
            return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
        }), authorize: false, rotatedKeyRing: true);
        await restarted.Services.GetRequiredService<ReplayWorker>().RunOnceAsync();

        var saved = await FindAsync(restarted.Services.GetRequiredService<IResilienceOperationStore>(), "revoked");
        Assert.Equal("ManualIntervention", saved!.State);
        Assert.Equal(0, saved.Attempts);
        Assert.Equal(0, originalCalls);
        Assert.Equal(0, currentCalls);
        Assert.Empty(await AttemptsAsync(restarted.Services.GetRequiredService<IResilienceOperationStore>(), "revoked"));
    }

    [Fact]
    public async Task MissingHistoricalHandlerMovesCommandToManualIntervention()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.handler-version");
        using (var original = CreateReplayHost(database, operation, new NoopReplayHandler(), handlerName: "orders", schema: "1"))
            await original.Services.GetRequiredService<IReplayCoordinator>().EnqueueAsync(Command("old-handler", operation.Name, "payload"));

        var currentCalls = 0;
        var services = DurableWorkflowTestSupport.CreateServices(database, operation, rotatedKeyRing: true);
        services.AddBingDurableReplay(options => options.Authorize = (_, _) => ValueTask.FromResult(true),
            DurableWorkflowTestSupport.ReplayHandler("orders-v2", "2", operation.Name,
                new DelegateReplayHandler((_, _) =>
                {
                    currentCalls++;
                    return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
                })));
        using var restarted = DurableWorkflowTestSupport.Build(services);

        await restarted.Services.GetRequiredService<ReplayWorker>().RunOnceAsync();

        var saved = await FindAsync(restarted.Services.GetRequiredService<IResilienceOperationStore>(), "old-handler");
        Assert.Equal("ManualIntervention", saved!.State);
        Assert.Equal(0, saved.Attempts);
        Assert.Equal(0, currentCalls);
    }

    [Fact]
    public async Task CorruptJournalDataMovesReplayToManualInterventionWithoutRetryingHandler()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.corrupt-journal");
        var calls = 0;
        using var host = CreateReplayHost(database, operation, new DelegateReplayHandler((_, _) =>
        {
            calls++;
            return ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
        }));
        var coordinator = host.Services.GetRequiredService<IReplayCoordinator>();
        var store = host.Services.GetRequiredService<IResilienceOperationStore>();
        var command = Command("corrupt-journal", operation.Name, "payload");
        await coordinator.EnqueueAsync(command);

        await using (var transaction = await store.BeginAsync())
        {
            var saved = (await transaction.FindAsync(command.OperationId))!;
            Assert.True(await transaction.CompareExchangeAsync(saved with { Data = "v1.corrupt", Version = saved.Version + 1 }, saved.Version));
            await transaction.CommitAsync();
        }

        Assert.True(await host.Services.GetRequiredService<ReplayWorker>().RunOnceAsync());
        var manual = await FindAsync(store, command.OperationId);
        Assert.Equal("ManualIntervention", manual!.State);
        Assert.Equal(0, manual.Attempts);
        Assert.Equal(0, calls);
        Assert.True(await host.Services.GetRequiredService<ReplayWorker>().RunOnceAsync() == false);
    }

    [Fact]
    public void InvalidProtectedDataIsReportedAsConfigurationFailure()
    {
        using var protector = new AesGcmOperationDataProtector("key", new Dictionary<string, byte[]> { ["key"] = new byte[32] });
        Assert.Throws<ResilienceConfigurationException>(() => protector.Unprotect("v1.key.not-base64.not-base64.not-base64"));
        var tampered = protector.Protect("payload").Split('.');
        tampered[3] = Convert.ToBase64String(new byte[16]);
        Assert.Throws<ResilienceConfigurationException>(() => protector.Unprotect(string.Join('.', tampered)));
    }

    [Fact]
    public async Task DurableWorkerAttemptDisablesNestedHttpRetryAndWriteReplay()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var operation = DurableWorkflowTestSupport.WriteOperation("orders.http-durable");
        var sentMethods = new ConcurrentQueue<string>();
        var services = DurableWorkflowTestSupport.CreateServices(database, operation);
        services.AddBingResilience().AddBuiltInProvider("builtin").UseDefaultProvider("builtin");
        services.AddHttpClient("durable-http")
            .ConfigurePrimaryHttpMessageHandler(() => new UnavailableTransport(sentMethods))
            .AddBingHttpResilience("durable-http", options =>
            {
                options.Mode = BingHttpResilienceMode.Retry;
                options.CanRetryRequest = _ => true;
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.Zero;
                options.Retry.MaxDelay = TimeSpan.Zero;
                options.Retry.UseJitter = false;
                options.CircuitBreaker.Enabled = false;
                options.Concurrency.Enabled = false;
                options.Hedging.ValidateCustomTransport = handler => handler is UnavailableTransport;
            })
            .AddBingHttpWriteReplay(options =>
            {
                options.Enabled = true;
                options.CanReplayWrite = _ => true;
                options.GetContract = candidate => new HttpIdempotencyContract(
                    candidate.ContractVersion, candidate.IdempotencyScope!, TimeSpan.FromDays(1), SharedAcrossEndpoints: false);
            });
        services.AddBingDurableReplay(options =>
        {
            options.Authorize = (_, _) => ValueTask.FromResult(true);
            options.RetryDelay = TimeSpan.Zero;
        }, new ReplayHandlerRegistration("orders", "1", operation.Name,
            provider => new HttpCallingReplayHandler(provider.GetRequiredService<IHttpClientFactory>(), operation)));
        using var host = DurableWorkflowTestSupport.Build(services);
        await host.Services.GetRequiredService<IReplayCoordinator>().EnqueueAsync(Command("http-durable", operation.Name, "payload"));

        await host.Services.GetRequiredService<ReplayWorker>().RunOnceAsync();

        Assert.Equal(new[] { "GET", "POST" }, sentMethods.ToArray());
    }

    private static DurableWorkflowHost CreateReplayHost(DurableSqliteDatabase database, ResilienceOperationDescriptor operation,
        IReplayCommandHandler handler, bool authorize = true, string handlerName = "orders", string schema = "1", bool rotatedKeyRing = false)
    {
        var services = DurableWorkflowTestSupport.CreateServices(database, operation, rotatedKeyRing: rotatedKeyRing);
        services.AddBingDurableReplay(options => options.Authorize = (_, _) => ValueTask.FromResult(authorize),
            DurableWorkflowTestSupport.ReplayHandler(handlerName, schema, operation.Name, handler));
        return DurableWorkflowTestSupport.Build(services);
    }

    private static ReplayCommand Command(string id, string operation, string payload, DateTimeOffset? deadline = null) =>
        new(id, operation, "v1", "orders", "1", "tenant-a", id + "-key", payload,
            deadline ?? DateTimeOffset.UtcNow.AddHours(1));

    private static async Task<OperationRecord?> FindAsync(IResilienceOperationStore store, string id)
    {
        await using var transaction = await store.BeginAsync();
        return await transaction.FindAsync(id);
    }

    private static async Task<IReadOnlyList<OperationAttempt>> AttemptsAsync(IResilienceOperationStore store, string id)
    {
        await using var transaction = await store.BeginAsync();
        return await transaction.GetAttemptsAsync(id);
    }

    private static async Task<string?[]> AttemptOutcomesAsync(IResilienceOperationStore store, string id) =>
        (await AttemptsAsync(store, id)).Select(x => x.Outcome).ToArray();

    private sealed class NoopReplayHandler : IReplayCommandHandler
    {
        public ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext context, CancellationToken token) =>
            ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
        public ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext context, CancellationToken token) =>
            ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
    }

    private sealed class DelegateReplayHandler(
        Func<ReplayExecutionContext, CancellationToken, ValueTask<ReplayHandlerResult>> execute,
        Func<ReplayExecutionContext, CancellationToken, ValueTask<ReplayHandlerResult>>? confirm = null) : IReplayCommandHandler
    {
        public ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext context, CancellationToken token) => execute(context, token);
        public ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext context, CancellationToken token) =>
            confirm is null ? ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Unknown)) : confirm(context, token);
    }

    private sealed class UnavailableTransport(ConcurrentQueue<string> methods) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            methods.Enqueue(request.Method.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    private sealed class HttpCallingReplayHandler(IHttpClientFactory clients, ResilienceOperationDescriptor operation) : IReplayCommandHandler
    {
        public async ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext context, CancellationToken token)
        {
            var client = clients.CreateClient("durable-http");
            using (var read = new HttpRequestMessage(HttpMethod.Get, "https://durable.test/read"))
            using (var response = await client.SendAsync(read, token))
            {
                if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
                    throw new InvalidOperationException("The test transport must return a transient response.");
            }
            using var write = new HttpRequestMessage(HttpMethod.Post, "https://durable.test/write")
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("durable-payload"))
            };
            write.Options.Set(BingHttpOperationMetadata.Operation, operation);
            write.Options.Set(BingHttpOperationMetadata.IdempotencyKey, context.Command.IdempotencyKey);
            write.Options.Set(BingHttpOperationMetadata.AccessScope, context.Command.AccessScope);
            using var writeResponse = await client.SendAsync(write, token);
            if (writeResponse.StatusCode != HttpStatusCode.ServiceUnavailable)
                throw new InvalidOperationException("The test transport must return a transient response.");
            return new ReplayHandlerResult(ReplayOutcome.Succeeded, "checked");
        }

        public ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext context, CancellationToken token) =>
            ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded, "confirmed"));
    }
}
