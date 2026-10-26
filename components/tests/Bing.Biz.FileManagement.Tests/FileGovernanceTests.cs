using System.Text.Json;
using Bing.FileStorage;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class FileGovernanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsumerReceipts_SkipSuccessAcrossRestart_AndFreezeSubscription(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        await env.UploadAsync(new byte[] { 42 });
        var success = new Handler("success"); var failure = new Handler("failure") { Fail = true };
        var dispatcher = Dispatcher(env, success, failure);
        Assert.Equal(0, await dispatcher.DispatchAsync());
        var store = env.Get<IFileManagementStore>(); var value = Assert.Single(await store.QueryEventsAsync(new() { ScopeKey = StorageScope.ForTenant("tenant-a").Key }));
        Assert.Equal(2, (await store.ListEventDeliveriesAsync(value.Id)).Count);
        Assert.True((await store.ListEventDeliveriesAsync(value.Id)).Single(x => x.Consumer == "success").Delivered);
        Assert.Equal("handler_failed", (await store.ListEventDeliveriesAsync(value.Id)).Single(x => x.Consumer == "failure").ErrorCode);
        await MutateAsync<FileOutboxEvent>(env.Provider, x => { x.NextAttemptAt = 0; });
        await MutateAsync<FileEventDelivery>(env.Provider, x => { x.NextAttemptAt = 0; });
        await env.RestartAsync(); failure.Fail = false;
        var newcomer = new Handler("new-consumer");
        Assert.Equal(1, await Dispatcher(env, success, failure, newcomer).DispatchAsync());
        Assert.Equal(1, success.Calls); Assert.Equal(2, failure.Calls); Assert.Equal(0, newcomer.Calls);
        Assert.Empty(await env.Get<IFileManagementStore>().ListDueEventsAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExhaustedConsumer_RequiresScopedVersionedIdempotentReplay_AndRefusesLiveLease(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql) { ConfigureManagement = o => o.MaxEventDeliveryAttempts = 1 };
        await env.InitializeAsync(); await env.UploadAsync(Array.Empty<byte>());
        var handler = new Handler("audit") { Fail = true }; await Dispatcher(env, handler).DispatchAsync();
        var store = env.Get<IFileManagementStore>(); var value = Assert.Single(await store.QueryEventsAsync(new() { ScopeKey = StorageScope.ForTenant("tenant-a").Key }));
        Assert.Equal(long.MaxValue, value.NextAttemptAt); Assert.Equal("attempts_exhausted", value.ErrorCode);
        Assert.Equal(0, await Dispatcher(env, handler).DispatchAsync()); Assert.Equal(1, handler.Calls);
        var admin = new FileGovernanceAdministration(store, env.Get<FileManagementOptions>(), new[] { handler });
        var request = new ReplayFileEvent { RequestId = Guid.NewGuid(), Consumer = handler.Name, ExpectedVersion = value.Version, Reason = "修复消费者后重放" };
        Assert.Equal("not_found", (await Assert.ThrowsAsync<FileManagementException>(() => admin.ReplayAsync("tenant:other", "user-a", value.Id, request))).Code);
        request.ExpectedVersion--;
        Assert.Equal("conflict", (await Assert.ThrowsAsync<FileManagementException>(() => admin.ReplayAsync(value.ScopeKey, "user-a", value.Id, request))).Code);
        request.ExpectedVersion++;
        var audit = await admin.ReplayAsync(value.ScopeKey, "user-a", value.Id, request);
        Assert.Equal(audit.ResultJson, (await admin.ReplayAsync(value.ScopeKey, "user-a", value.Id, request)).ResultJson);
        Assert.Single(await store.ListGovernanceAuditAsync(value.ScopeKey, value.Id, 0, 50));
        request.Reason = "different";
        Assert.Equal("conflict", (await Assert.ThrowsAsync<FileManagementException>(() => admin.ReplayAsync(value.ScopeKey, "user-a", value.Id, request))).Code);
        value = (await store.FindEventAsync(value.Id))!;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.True(await store.TryClaimEventAsync(value.Id, value.Version, "active", now, now + 30000));
        value = (await store.FindEventAsync(value.Id))!;
        request.RequestId = Guid.NewGuid(); request.ExpectedVersion = value.Version;
        Assert.Equal("conflict", (await Assert.ThrowsAsync<FileManagementException>(() => admin.ReplayAsync(value.ScopeKey, "user-a", value.Id, request))).Code);
        Assert.True(await store.FinishEventAsync(value.Id, "active", false, 0, null));
        handler.Fail = false; Assert.Equal(1, await Dispatcher(env, handler).DispatchAsync()); Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeliveryCommit_FencesExpiredLeaseAndVersionConflict(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync(); await env.UploadAsync(Array.Empty<byte>());
        var store = env.Get<IFileManagementStore>(); var value = Assert.Single(await store.ListDueEventsAsync(long.MaxValue, 100));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.True(await store.TryClaimEventAsync(value.Id, value.Version, "worker", now, now + 30000));
        var original = (await store.FindEventAsync(value.Id))!;
        var updated = Clone(original); updated.Version++;
        var rows = new[] { new FileEventDelivery { EventId = original.Id, Consumer = "audit", Delivered = true, CompletedAt = now } };
        Assert.False(await store.TryUpdateEventDeliveryAsync(original, updated, rows, "wrong", null));
        Assert.True(await store.TryUpdateEventDeliveryAsync(original, updated, rows, "worker", null));
        Assert.False(await store.TryUpdateEventDeliveryAsync(original, updated, rows, "worker", null));
        await MutateAsync<FileOutboxEvent>(env.Provider, x => { x.LeaseUntil = now - 1; });
        original = (await store.FindEventAsync(value.Id))!; updated = Clone(original); updated.Version++; updated.Delivered = true;
        Assert.False(await store.TryUpdateEventDeliveryAsync(original, updated, rows, "worker", null));
        Assert.False((await store.FindEventAsync(value.Id))!.Delivered);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task History_ArchivesOnlyOldTerminalRecords_AndRestoresWithoutDeletingFiles(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync(); var file = await env.UploadAsync(new byte[] { 7 });
        var old = DateTimeOffset.UtcNow.AddDays(-100).ToUnixTimeMilliseconds();
        await MutateAsync<FileOperation>(env.Provider, x => { x.FinishedAt = old; });
        var service = env.Get<FileManagementService>();
        await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "pending.bin", Length = 1 });
        var admin = env.Get<FileGovernanceAdministration>(); var scope = (await service.GetContextAsync()).ScopeKey;
        var selected = await admin.PreviewAsync(scope, FileHistoryKind.Operation, take: 100);
        Assert.True(selected.Items.Count >= 2);
        Assert.Empty((await admin.PreviewAsync("tenant:other", FileHistoryKind.Operation)).Items);
        var request = new FileGovernanceRequest { RequestId = Guid.NewGuid(), Reason = "历史记录整理" };
        var result = await admin.ArchiveAsync(scope, "user-a", selected, request);
        Assert.Equal(result.ResultJson, (await admin.ArchiveAsync(scope, "user-a", selected, request)).ResultJson);
        var archives = await admin.ListArchivesAsync(scope, FileHistoryKind.Operation, 0, 100);
        Assert.Equal(selected.Items.Count, archives.Count);
        var store = env.Get<IFileManagementStore>();
        var active = await store.QueryOperationsAsync(new() { ScopeKey = scope });
        Assert.DoesNotContain(active, op => selected.Items.Any(s => s.Id == op.Id));
        Assert.NotEmpty(active);
        Assert.NotNull(await store.FindOperationAsync(selected.Items[0].Id, scope));
        Assert.Equal(FileState.Ready, (await store.FindAsync(file.FileId))!.File.State);
        var restore = new FileGovernanceRequest { RequestId = Guid.NewGuid(), Reason = "恢复查询" };
        await admin.RestoreAsync(scope, "user-a", archives[0].Id, restore); await admin.RestoreAsync(scope, "user-a", archives[0].Id, restore);
        Assert.Contains(await store.QueryOperationsAsync(new() { ScopeKey = scope }), op => op.Id == archives[0].EntityId);
        Assert.Equal(2, (await admin.ListAuditAsync(scope, null, 0, 100)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Archive_RejectsChangedPreviewAtomically_AndReplayRestoresArchivedEvent(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync(); await env.UploadAsync(Array.Empty<byte>());
        var old = DateTimeOffset.UtcNow.AddDays(-100).ToUnixTimeMilliseconds();
        await MutateAsync<FileOperation>(env.Provider, x => { x.FinishedAt = old; });
        var admin = env.Get<FileGovernanceAdministration>(); var scope = StorageScope.ForTenant("tenant-a").Key;
        var selected = await admin.PreviewAsync(scope, FileHistoryKind.Operation);
        await MutateAsync<FileOperation>(env.Provider, x => { if (x.Id == selected.Items[^1].Id) x.Version++; });
        Assert.Equal("conflict", (await Assert.ThrowsAsync<FileManagementException>(() => admin.ArchiveAsync(scope, "user-a", selected,
            new() { RequestId = Guid.NewGuid(), Reason = "changed" }))).Code);
        Assert.Empty(await admin.ListArchivesAsync(scope, null, 0, 100)); Assert.Empty(await admin.ListAuditAsync(scope, null, 0, 100));
        var handler = new Handler("audit"); await Dispatcher(env, handler).DispatchAsync();
        await MutateAsync<FileOutboxEvent>(env.Provider, x => { x.CreatedAt = old; });
        admin = new(env.Get<IFileManagementStore>(), env.Get<FileManagementOptions>(), new[] { handler });
        selected = await admin.PreviewAsync(scope, FileHistoryKind.Event);
        await admin.ArchiveAsync(scope, "user-a", selected, new() { RequestId = Guid.NewGuid(), Reason = "archive" });
        Assert.Empty(await admin.ListEventsAsync(new() { ScopeKey = scope }));
        var evt = Assert.Single(await admin.ListEventsAsync(new() { ScopeKey = scope, IncludeArchived = true }));
        await admin.ReplayAsync(scope, "user-a", evt.Id, new() { RequestId = Guid.NewGuid(), Reason = "replay", ExpectedVersion = evt.Version, Consumer = "audit" });
        Assert.Single(await admin.ListEventsAsync(new() { ScopeKey = scope })); Assert.Empty(await admin.ListArchivesAsync(scope, null, 0, 100));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingConsumer_RemainsPending_AndNewConsumerRequiresExplicitReplay(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql) { ConfigureManagement = o => o.MaxEventDeliveryAttempts = 1 };
        await env.InitializeAsync(); await env.UploadAsync(Array.Empty<byte>());
        var old = new Handler("removed") { Fail = true }; await Dispatcher(env, old).DispatchAsync();
        var replacement = new Handler("replacement");
        Assert.Equal(0, await Dispatcher(env, replacement).DispatchAsync());
        var store = env.Get<IFileManagementStore>(); var evt = Assert.Single(await store.QueryEventsAsync(new() { ScopeKey = StorageScope.ForTenant("tenant-a").Key }));
        Assert.False(evt.Delivered); Assert.Equal(0, replacement.Calls);
        var admin = new FileGovernanceAdministration(store, env.Get<FileManagementOptions>(), new[] { old, replacement });
        await admin.ReplayAsync(evt.ScopeKey, "user-a", evt.Id, new() { RequestId = Guid.NewGuid(), ExpectedVersion = evt.Version, Consumer = "replacement", Reason = "explicit subscription" });
        await Dispatcher(env, replacement).DispatchAsync(); Assert.Equal(1, replacement.Calls);
        Assert.False((await store.FindEventAsync(evt.Id))!.Delivered);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedBatch_ArchivesAndRestores_WithoutDroppingItems(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync(); await env.UploadAsync(new byte[] { 9 });
        var batches = env.Get<FileMigrationBatchService>();
        var batch = await batches.CreateAsync(StorageScope.ForTenant("tenant-a"), "user-a", new()
        { RequestId = Guid.NewGuid(), SourceStore = "local-v1", TargetStore = "local-v2" });
        await env.RunAsync(5); await batches.AdvanceAsync();
        Assert.Equal(FileMigrationBatchState.Completed, (await batches.GetAsync(batch.ScopeKey, batch.Id)).Batch.State);
        await MutateAsync<FileMigrationBatch>(env.Provider, row => row.CreatedAt = DateTimeOffset.UtcNow.AddDays(-100).ToUnixTimeMilliseconds());
        var admin = env.Get<FileGovernanceAdministration>();
        var preview = await admin.PreviewAsync(batch.ScopeKey, FileHistoryKind.MigrationBatch);
        Assert.Single(preview.Items);
        await admin.ArchiveAsync(batch.ScopeKey, "user-a", preview, new() { RequestId = Guid.NewGuid(), Reason = "archive batch" });
        Assert.Empty(await env.Get<IFileManagementStore>().ListBatchesAsync(batch.ScopeKey, 0, 50));
        Assert.Equal(1, (await batches.GetAsync(batch.ScopeKey, batch.Id)).Succeeded);
        var archived = Assert.Single(await admin.ListArchivesAsync(batch.ScopeKey, FileHistoryKind.MigrationBatch, 0, 50));
        await admin.RestoreAsync(batch.ScopeKey, "user-a", archived.Id, new() { RequestId = Guid.NewGuid(), Reason = "restore batch" });
        Assert.Single(await env.Get<IFileManagementStore>().ListBatchesAsync(batch.ScopeKey, 0, 50));
    }

    internal static async Task MutateAsync<T>(IServiceProvider provider, Action<T> change) where T : class
    {
        if (provider.GetService<IFreeSql>() is { } orm)
        {
            foreach (var row in await orm.Select<T>().ToListAsync()) { change(row); await orm.Update<T>().SetSource(row).ExecuteAffrowsAsync(); }
            return;
        }
        await using var db = await provider.GetRequiredService<IDbContextFactory<FileManagementDbContext>>().CreateDbContextAsync();
        foreach (var row in await db.Set<T>().ToListAsync()) change(row);
        await db.SaveChangesAsync();
    }
    private static FileOutboxDispatcher Dispatcher(FileTestEnvironment env, params IFileLifecycleEventHandler[] handlers)
        => new(env.Get<IFileManagementStore>(), env.Get<FileManagementOptions>(), handlers);
    private static FileOutboxEvent Clone(FileOutboxEvent value) => JsonSerializer.Deserialize<FileOutboxEvent>(JsonSerializer.Serialize(value))!;
    internal sealed class Handler : IFileLifecycleEventHandler
    {
        public Handler(string name) { Name = name; }
        public string Name { get; }
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public Task HandleAsync(FileLifecycleEvent message, CancellationToken ct)
        { Calls++; if (Fail) throw new IOException("secret exception must not be persisted"); return Task.CompletedTask; }
    }
}
