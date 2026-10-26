using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class EnhancementResourceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admission_ReservesAcrossRestartAndReleasesOnlyAfterCleanup(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql)
        { ConfigureManagement = o => { o.Admission.TenantSessions = 1; o.Admission.TenantBytes = 10; } };
        await env.InitializeAsync();
        var request = new CreateFileUpload { RequestId = Guid.NewGuid(), FileName = "one.bin", Length = 10 };
        var service = env.Get<FileManagementService>();
        var first = await service.CreateUploadAsync(request);
        Assert.Equal(first.Id, (await service.CreateUploadAsync(request)).Id);
        Assert.Equal(10, (await env.Get<IFileManagementStore>().GetOperationalSnapshotAsync()).ReservedBytes);
        await env.RestartAsync(); service = env.Get<FileManagementService>();
        env.Identity.UserId = "other-owner";
        Assert.Equal("capacity", (await Assert.ThrowsAsync<FileManagementException>(() => service.CreateUploadAsync(
            new() { RequestId = Guid.NewGuid(), FileName = "two.bin", Length = 1 }))).Code);
        env.Identity.UserId = "user-a";
        await service.AbortUploadAsync(first.Id);
        Assert.Equal(10, (await env.Get<IFileManagementStore>().GetOperationalSnapshotAsync()).ReservedBytes);
        await env.RunAsync();
        Assert.Equal(0, (await env.Get<IFileManagementStore>().GetOperationalSnapshotAsync()).ReservedBytes);
        await env.UploadAsync(new byte[] { 1 });
        Assert.Equal(0, (await env.Get<IFileManagementStore>().GetOperationalSnapshotAsync()).ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admission_ConcurrentCreatesNeverOverbookAndScopesRemainIndependent(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql) { ConfigureManagement = o => o.Admission.UserBytes = 10 };
        await env.InitializeAsync();
        var service = env.Get<FileManagementService>();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "ten.bin", Length = 10 }); return true; }
            catch (FileManagementException ex) when (ex.Code == "capacity" || ex.Code == "conflict") { return false; }
        }));
        Assert.Equal(1, results.Count(x => x));
        env.Identity.UserId = "user-b";
        await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "second.bin", Length = 10 });
        env.Identity.Tenant = "tenant-b";
        await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "third.bin", Length = 10 });
        Assert.Equal(30, (await env.Get<IFileManagementStore>().GetOperationalSnapshotAsync()).ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResourceSlots_AreSharedBoundedAndTokenFenced(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql)
        { ConfigureManagement = o => o.StoreIoLimits["local-v1"] = new() { LocalParts = 1, BackgroundOperations = 1 } };
        await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>(); var gate = env.Get<FileResourceGate>();
        var permit = await gate.TryEnterAsync(new[] { "local-v1" }, false); Assert.NotNull(permit);
        var secondGate = new FileResourceGate(store, env.Get<FileManagementOptions>());
        Assert.Null(await secondGate.TryEnterAsync(new[] { "local-v1" }, false));
        await using (var background = await secondGate.TryEnterAsync(new[] { "local-v1" }, true)) Assert.NotNull(background);
        await permit!.DisposeAsync();
        await using (var next = await secondGate.TryEnterAsync(new[] { "local-v1" }, false)) Assert.NotNull(next);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var old = new FileIoLease { Id = Guid.NewGuid(), PoolKey = "expiry", Token = "old", ExpiresAt = now - 1 };
        Assert.True(await store.TryAcquireIoLeaseAsync(old, 1, now - 100));
        var replacement = new FileIoLease { Id = Guid.NewGuid(), PoolKey = "expiry", Token = "new", ExpiresAt = now + 10000 };
        Assert.True(await store.TryAcquireIoLeaseAsync(replacement, 1, now));
        Assert.False(await store.RenewIoLeaseAsync(old.Id, old.Token, now + 10000));
        await store.ReleaseIoLeaseAsync(replacement.Id, "wrong-token");
        Assert.False(await store.TryAcquireIoLeaseAsync(new() { Id = Guid.NewGuid(), PoolKey = "expiry", Token = "extra", ExpiresAt = now + 10000 }, 1, now));
        await store.ReleaseIoLeaseAsync(replacement.Id, replacement.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Outbox_IsAtomicWithStateAndReplaysSameEventAfterRestart(bool freeSql)
    {
        var handler = new RecordingHandler();
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.UploadAsync(new byte[] { 4, 2 });
        var store = env.Get<IFileManagementStore>();
        var ready = Assert.Single(await store.ListDueEventsAsync(long.MaxValue, 100));
        Assert.Equal("FileReady", ready.Kind);
        var entry = (await store.FindAsync(session.FileId))!;
        entry.File.State = FileState.Deleted;
        Assert.False(await store.TryUpdateAsync(entry, entry.File.Version - 1));
        Assert.Single(await store.ListDueEventsAsync(long.MaxValue, 100));
        var dispatcher = new FileOutboxDispatcher(store, env.Get<FileManagementOptions>(), new[] { handler });
        Assert.Equal(0, await dispatcher.DispatchAsync());
        var retry = Assert.Single(await store.ListDueEventsAsync(long.MaxValue, 100));
        Assert.Equal(ready.Id, retry.Id); Assert.Equal("consumer_pending", retry.ErrorCode);
        Assert.Equal("handler_failed", Assert.Single(await store.ListEventDeliveriesAsync(ready.Id)).ErrorCode);
        // 显式重放复用原事件 ID，同时重置指定消费者的退避。
        await new FileGovernanceAdministration(store, env.Get<FileManagementOptions>(), new[] { handler }).ReplayAsync(
            retry.ScopeKey, "user-a", retry.Id, new() { RequestId = Guid.NewGuid(), ExpectedVersion = retry.Version, Consumer = handler.Name, Reason = "retry" });
        await env.RestartAsync(); store = env.Get<IFileManagementStore>(); handler.Fail = false;
        dispatcher = new FileOutboxDispatcher(store, env.Get<FileManagementOptions>(), new[] { handler });
        Assert.Equal(1, await dispatcher.DispatchAsync()); Assert.Equal(0, await dispatcher.DispatchAsync());
        Assert.Equal(new[] { ready.Id, ready.Id }, handler.Received);
        await env.Get<FileManagementService>().DeleteAsync(session.FileId); await env.RunAsync();
        Assert.Equal("FileDeleted", Assert.Single(await store.ListDueEventsAsync(long.MaxValue, 100)).Kind);
    }

    private sealed class RecordingHandler : IFileLifecycleEventHandler
    {
        public string Name => "test";
        public bool Fail { get; set; } = true;
        public List<Guid> Received { get; } = new();
        public Task HandleAsync(FileLifecycleEvent message, CancellationToken ct)
        { Received.Add(message.Id); if (Fail) throw new IOException("secret detail must not persist"); return Task.CompletedTask; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalPartSlot_RejectsBeforeReadingAndAllowsRetryAfterRelease(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql)
        { ConfigureManagement = o => o.StoreIoLimits["local-v1"] = new() { LocalParts = 1 } };
        await env.InitializeAsync(); var files = env.Get<FileManagementService>();
        var session = await files.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "part.bin", Length = 1 });
        await env.RunAsync();
        var bytes = new byte[] { 3 };
        await files.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(bytes)) });
        var held = (await env.Get<FileResourceGate>().TryEnterAsync(new[] { "local-v1" }, false))!;
        using var input = new MemoryStream(bytes);
        Assert.Equal("capacity", (await Assert.ThrowsAsync<FileManagementException>(() => files.UploadPartAsync(session.Id, 1, input))).Code);
        Assert.Equal(0, input.Position); await held.DisposeAsync();
        await files.UploadPartAsync(session.Id, 1, input); await files.CompleteUploadAsync(session.Id); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await files.GetSessionAsync(session.Id)).State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scheduler_SkipsStoreWithoutCapacityAndDoesNotSpendItsAttempts(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql) { ConfigureManagement = o =>
        { o.ShortOperationConcurrency = 1; o.OperationPollInterval = TimeSpan.FromMilliseconds(100); o.StoreIoLimits["local-v1"] = new() { BackgroundOperations = 1 }; } };
        await env.InitializeAsync(); var files = env.Get<FileManagementService>(); var store = env.Get<IFileManagementStore>();
        var blocked = await files.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "blocked.bin", Length = 1 });
        var other = await files.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "other.bin", Length = 1 });
        var entry = (await store.FindAsync(other.FileId))!; entry.File.StoreName = "local-v2";
        Assert.True(await store.TryUpdateAsync(entry, entry.File.Version));
        await using var held = (await env.Get<FileResourceGate>().TryEnterAsync(new[] { "local-v1" }, true))!;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var worker = env.Get<FileOperationScheduler>().RunAsync(stop.Token);
        try
        {
            while ((await store.FindAsync(other.FileId))!.File.State == FileState.Pending) await Task.Delay(50, stop.Token);
            Assert.Equal(FileState.Uploading, (await store.FindAsync(other.FileId))!.File.State);
            Assert.Equal(FileState.Pending, (await store.FindAsync(blocked.FileId))!.File.State);
            Assert.Equal(0, Assert.Single(await store.QueryOperationsAsync(new() { ScopeKey = entry.File.ScopeKey, FileId = blocked.FileId })).Attempts);
        }
        finally { stop.Cancel(); await worker; }
    }
}
