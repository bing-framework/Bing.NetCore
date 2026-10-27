using System.Data.Common;
using System.Text.Json;
using Bing.Resilience.Compensation;
using Bing.Resilience.Operations;
using Bing.Resilience.Operations.Relational;
using Bing.Resilience.Replay;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MySqlConnector;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class OperationRetentionTests
{
    [Fact]
    public void RetentionRejectsUnboundedOrNonPositiveConfiguration()
    {
        var valid = new OperationRetentionPolicy(TimeSpan.FromDays(1), TimeSpan.FromDays(1));
        foreach (var invalid in new[] { valid with { ProtectionPeriod = TimeSpan.Zero }, valid with { PayloadRetention = TimeSpan.MaxValue },
                     valid with { AttemptRetention = TimeSpan.Zero }, valid with { AuditRetention = TimeSpan.FromSeconds(-1) },
                     valid with { BatchSize = 0 }, valid with { HistoryBatchSize = 10001 } })
            Assert.Throws<ResilienceConfigurationException>(invalid.Validate);
    }

    [Fact] public Task SqliteCleanupAndClaimDoNotEraseInFlightData() => ConcurrentContract(false);
    [MySqlFact] public Task MySqlCleanupAndClaimDoNotEraseInFlightData() => ConcurrentContract(true);
    private static async Task ConcurrentContract(bool mysql)
    {
        await using var db = await Database.Create(mysql);
        await db.Seed(new[] { Record("pending", "Pending", DateTimeOffset.UtcNow), Record("done", "Succeeded", DateTimeOffset.UtcNow) });
        var cleanup = Task.Run(async () => await db.Store.CleanupAsync("replay", new(TimeSpan.FromDays(1), TimeSpan.FromDays(1)), _ => "minimal"));
        var claim = Task.Run(async () =>
        {
            await using var tx = await db.Store.BeginAsync();
            var saved = await tx.ClaimDueAsync("replay", new[] { "Pending" }, TimeSpan.FromMinutes(1));
            Assert.Equal("sensitive-pending", saved!.Data);
            await tx.CommitAsync();
        });
        await Task.WhenAll(cleanup, claim);
        Assert.Equal(1, cleanup.Result.Payloads);
        await using var read = await db.Store.BeginAsync();
        var pending = (await read.FindAsync("pending"))!;
        Assert.Equal("sensitive-pending", pending.Data); Assert.NotNull(pending.LeaseToken);
    }

    [Fact] public Task SqliteRetentionPreservesProtectedStatesAndBoundsBatches() => StoreContract(false);
    [MySqlFact] public Task MySqlRetentionPreservesProtectedStatesAndBoundsBatches() => StoreContract(true);

    private static async Task StoreContract(bool mysql)
    {
        await using var db = await Database.Create(mysql);
        var now = DateTimeOffset.UtcNow;
        var records = new[]
        {
            Record("done", "Succeeded", now), Record("failed", "Failed", now),
            Record("expired", "Expired", now), Record("unknown", "Unknown", now),
            Record("manual", "ManualIntervention", now), Record("pending", "Pending", now),
            Record("protected", "Succeeded", now) with { Deadline = now.AddHours(-1) },
            Record("leased", "Succeeded", now) with { LeaseToken = "active", LeaseUntil = now.AddHours(1) }
        };
        await db.Seed(records);
        var policy = new OperationRetentionPolicy(TimeSpan.FromDays(1), TimeSpan.FromDays(1),
            AttemptRetention: TimeSpan.FromDays(1), BatchSize: 1, HistoryBatchSize: 1);
        var payloads = 0; var attempts = 0;
        for (var i = 0; i < 20; i++)
        {
            var batch = await db.Store.CleanupAsync("replay", policy, _ => "minimal");
            Assert.InRange(batch.Operations, 0, 1); Assert.InRange(batch.Attempts, 0, 1);
            payloads += batch.Payloads; attempts += batch.Attempts;
            if (batch.Operations == 0) break;
        }
        Assert.Equal(3, payloads); Assert.Equal(9, attempts);
        foreach (var original in records)
        {
            await using var tx = await db.Store.BeginAsync();
            var saved = (await tx.FindAsync(original.Id))!;
            Assert.Equal(original.KeyHash, saved.KeyHash); Assert.Equal(original.Fingerprint, saved.Fingerprint);
            Assert.Equal(original.State, saved.State);
            Assert.Equal(original.Id is "done" or "failed" or "expired", OperationRetainedData.IsRedacted(saved.Data));
            Assert.NotNull(await tx.FindByKeyAsync(original.Kind, original.ScopeHash, original.KeyHash));
        }
        Assert.Equal(8, await db.Count("audit", "event_type='seed'")); // 审计默认永久保留。
        var auditsPolicy = policy with { AuditRetention = TimeSpan.FromDays(1) };
        for (var i = 0; i < 20 && (await db.Store.CleanupAsync("replay", auditsPolicy, _ => "minimal")).Operations > 0; i++) { }
        Assert.Equal(5, await db.Count("audit", "event_type='seed'"));
        Assert.Equal(0, (await db.Store.CleanupAsync("replay", auditsPolicy, _ => throw new Exception())).Operations);

        await using var claim = await db.Store.BeginAsync();
        var pending = await claim.ClaimDueAsync("replay", new[] { "Pending" }, TimeSpan.FromMinutes(1));
        Assert.Equal("pending", pending?.Id); Assert.Equal("sensitive-pending", pending?.Data);
    }

    [Fact] public Task SqliteRetentionRollsBackCancellationAndRedactorFailure() => RollbackContract(false);
    [MySqlFact] public Task MySqlRetentionRollsBackCancellationAndRedactorFailure() => RollbackContract(true);
    private static async Task RollbackContract(bool mysql)
    {
        await using var db = await Database.Create(mysql);
        await db.Seed(new[] { Record("a", "Succeeded", DateTimeOffset.UtcNow), Record("b", "Succeeded", DateTimeOffset.UtcNow) });
        var policy = new OperationRetentionPolicy(TimeSpan.FromDays(1), TimeSpan.FromDays(1));
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.Store.CleanupAsync("replay", policy,
            _ => ++calls == 2 ? throw new InvalidOperationException() : "minimal").AsTask());
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.Store.CleanupAsync("replay", policy,
            _ => { cancellation.Cancel(); return "minimal"; }, cancellation.Token).AsTask());
        await using var tx = await db.Store.BeginAsync();
        Assert.Equal("sensitive-a", (await tx.FindAsync("a"))!.Data);
        Assert.Equal("sensitive-b", (await tx.FindAsync("b"))!.Data);
    }

    [Fact] public Task SqliteSagaCleanupIsAtomicAndProtectsUnknownChildren() => SagaStoreContract(false);
    [MySqlFact] public Task MySqlSagaCleanupIsAtomicAndProtectsUnknownChildren() => SagaStoreContract(true);
    private static async Task SagaStoreContract(bool mysql)
    {
        await using var db = await Database.Create(mysql);
        var now = DateTimeOffset.UtcNow;
        var parent = Record("saga", "Compensated", now) with { Kind = "saga" };
        var unknownParent = Record("unsafe", "Completed", now) with { Kind = "saga" };
        var child = Record("child", "Compensated", now) with { Kind = "saga-step", ScopeHash = OperationFingerprint.Hash(parent.Id) };
        var unknown = Record("unknown-child", "Unknown", now) with { Kind = "saga-step", ScopeHash = OperationFingerprint.Hash(unknownParent.Id) };
        await db.Seed(new[] { parent, child, unknownParent, unknown });
        var policy = new OperationRetentionPolicy(TimeSpan.FromDays(1), TimeSpan.FromDays(1), TimeSpan.FromDays(1), TimeSpan.FromDays(1), HistoryBatchSize: 1);
        var payloads = 0;
        for (var i = 0; i < 12; i++) payloads += (await db.Store.CleanupAsync("saga", policy, _ => "minimal")).Payloads;
        Assert.Equal(2, payloads);
        Assert.Equal(0, await db.Count("attempts", "operation_id IN ('saga','child')"));
        Assert.Equal(0, await db.Count("audit", "operation_id IN ('saga','child') AND event_type='seed'"));
        await using var tx = await db.Store.BeginAsync();
        Assert.Equal("sensitive-unsafe", (await tx.FindAsync("unsafe"))!.Data);
        Assert.Equal("sensitive-unknown-child", (await tx.FindAsync("unknown-child"))!.Data);
    }

    [Fact]
    public async Task ReplayCleanupRetainsAuthorizationAndDeduplicationButRemovesPayloadAndResult()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var db = await DurableSqliteDatabase.CreateAsync(clock);
        var services = DurableWorkflowTestSupport.CreateServices(db, DurableWorkflowTestSupport.WriteOperation("orders"), clock);
        services.AddBingDurableReplay(o => { o.Authorize = (_, _) => ValueTask.FromResult(true); o.Retention = new(TimeSpan.FromHours(1), TimeSpan.FromHours(1)); },
            new ReplayHandlerRegistration("write", "v1", "orders", _ => new Handler()));
        using var host = DurableWorkflowTestSupport.Build(services);
        var coordinator = host.Services.GetRequiredService<IReplayCoordinator>();
        var worker = host.Services.GetRequiredService<ReplayWorker>();
        var command = new ReplayCommand("op", "orders", "v1", "write", "v1", "scope", "key", "secret-body", clock.GetUtcNow().AddHours(1));
        await coordinator.EnqueueAsync(command); Assert.True(await worker.RunOnceAsync());
        Assert.Equal(0, (await worker.CleanupOnceAsync()).Payloads);
        clock.Advance(TimeSpan.FromDays(2));
        Assert.Equal(1, (await worker.CleanupOnceAsync()).Payloads);
        var saved = (await coordinator.GetAsync("op", "scope"))!;
        Assert.True(OperationRetainedData.IsRedacted(saved.Data)); Assert.Equal("Succeeded", saved.State);
        var plain = host.Services.GetRequiredService<IOperationDataProtector>().Unprotect(OperationRetainedData.Unwrap(saved.Data));
        Assert.DoesNotContain("secret-body", plain); Assert.DoesNotContain("secret-result", plain);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => coordinator.GetAsync("op", "other").AsTask());
        await coordinator.EnqueueAsync(command with { Deadline = clock.GetUtcNow().AddHours(1) });
        await Assert.ThrowsAsync<OperationIdempotencyConflictException>(() => coordinator.EnqueueAsync(command with { Payload = "changed", Deadline = clock.GetUtcNow().AddHours(1) }).AsTask());
        Assert.False(await worker.RunOnceAsync()); Assert.Equal(0, (await worker.CleanupOnceAsync()).Payloads);
    }

    [Fact]
    public async Task SagaCleanupPreservesQueriesAndRejectsReactivation()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var db = await DurableSqliteDatabase.CreateAsync(clock);
        var services = DurableWorkflowTestSupport.CreateServices(db, DurableWorkflowTestSupport.WriteOperation("orders"), clock);
        services.AddBingSaga(o => { o.Authorize = (_, _) => ValueTask.FromResult(true); o.Retention = new(TimeSpan.FromHours(1), TimeSpan.FromHours(1)); }, new SagaHandlerRegistration("step", "v1", _ => new Handler()));
        using var host = DurableWorkflowTestSupport.Build(services);
        var coordinator = host.Services.GetRequiredService<ISagaCoordinator>();
        var worker = host.Services.GetRequiredService<SagaWorker>();
        await coordinator.StartAsync(new("saga", "orders", "v1", "scope", "key", new[] { new SagaStepDefinition("step", "step", "v1", "secret-input") }, clock.GetUtcNow().AddHours(1)));
        Assert.True(await worker.RunOnceAsync()); Assert.True(await worker.RunOnceAsync());
        clock.Advance(TimeSpan.FromDays(2)); Assert.Equal(2, (await worker.CleanupOnceAsync()).Payloads);
        var saved = (await coordinator.GetAsync("saga", "scope"))!;
        Assert.Equal("Completed", saved.Saga.State); Assert.Single(saved.Steps);
        Assert.All(saved.Steps.Append(saved.Saga), r => Assert.True(OperationRetainedData.IsRedacted(r.Data)));
        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => coordinator.ApplyManualAsync("saga", "step", "scope", saved.Saga.Version, SagaManualAction.Compensate, "admin", "retry").AsTask());
    }

    private sealed class Handler : IReplayCommandHandler, ISagaStepHandler
    {
        public ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext c, CancellationToken t) => ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded, "secret-result"));
        public ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext c, CancellationToken t) => ExecuteAsync(c, t);
        public ValueTask<SagaStepResult> ForwardAsync(SagaStepContext c, CancellationToken t) => ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded, "secret-output"));
        public ValueTask<SagaStepResult> ConfirmAsync(SagaStepContext c, CancellationToken t) => ForwardAsync(c, t);
        public ValueTask<SagaStepResult> CompensateAsync(SagaStepContext c, CancellationToken t) => ForwardAsync(c, t);
    }
    private static OperationRecord Record(string id, string state, DateTimeOffset now) => new(id, "replay", OperationFingerprint.Hash("scope"),
        OperationFingerprint.Hash(id), OperationFingerprint.Hash("body"), state, 0, "sensitive-" + id, 3,
        now.AddDays(-10), now.AddDays(-8), now.AddDays(-8), now.AddDays(-9));

    private sealed class Database(string prefix, string? path, Func<DbConnection> connection, RelationalOperationStore store) : IAsyncDisposable
    {
        public RelationalOperationStore Store { get; } = store;
        public static async Task<Database> Create(bool mysql)
        {
            var prefix = "ret_" + Guid.NewGuid().ToString("N")[..16];
            var path = mysql ? null : Path.Combine(Path.GetTempPath(), prefix + ".db");
            Func<DbConnection> connection = mysql ? () => new MySqlConnection(MySqlTestSettings.ConnectionString) : () => new SqliteConnection($"Data Source={path};Pooling=false");
            var store = new RelationalOperationStore(_ => ValueTask.FromResult(connection()), mysql ? ResilienceRelationalDialect.MySql : ResilienceRelationalDialect.Sqlite, prefix);
            await store.InitializeAsync(); return new(prefix, path, connection, store);
        }
        public async Task Seed(IEnumerable<OperationRecord> records)
        {
            await using var tx = await Store.BeginAsync();
            foreach (var r in records)
            {
                Assert.True(await tx.TryInsertAsync(r));
                for (var i = 1; i <= 3; i++) await tx.AppendAttemptAsync(new(r.Id, i, "forward", "old", r.UpdatedAt, r.UpdatedAt, "success"));
                await tx.AppendAuditAsync(new(r.Id, 0, "seed", "test", "seed", r.UpdatedAt));
            }
            await tx.CommitAsync();
        }
        public async Task<int> Count(string suffix, string where)
        {
            await using var c = connection(); await c.OpenAsync();
            return await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {prefix}_{suffix} WHERE {where}");
        }
        public async ValueTask DisposeAsync()
        {
            await using (var c = connection())
            {
                await c.OpenAsync();
                foreach (var table in new[] { "attempts", "audit", "operations" }) await c.ExecuteAsync($"DROP TABLE {prefix}_{table}");
            }
            if (path is not null) File.Delete(path);
        }
    }
}
