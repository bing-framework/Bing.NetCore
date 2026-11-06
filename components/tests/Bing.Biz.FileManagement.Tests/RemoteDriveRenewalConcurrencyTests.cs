using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.FileStorage.RemoteDrive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>用真实 SQLite 和保存前 barrier 固定心跳读取与业务提交的竞争顺序。</summary>
public sealed class RemoteDriveRenewalConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Renewal_OverlapsCheckpointOrPublish_ReloadsOwner(bool publish)
    {
        var gate = new RenewalGate();
        await using var env = new FileTestEnvironment();
        env.CustomizeServices = services => RenewalGate.Register(services, env.Root, gate);
        await env.InitializeAsync();
        var store = env.Get<IRemoteDriveStateStore>();
        var transfer = await RemoteDriveLeaseTests.CreateTransferAsync(store);
        Assert.True(await store.TryClaimTransferAsync(transfer.Id, transfer.Version, "owner", Now + 30_000));
        var snapshot = (await store.FindTransferAsync(transfer.Id))!;
        var until = Now + 60_000;
        var renewal = store.RenewTransferAsync(transfer.Id, "owner", until);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            if (publish)
                Assert.True(await store.TryPublishAsync(RemoteDriveLeaseTests.CreateObject(snapshot), 0, snapshot, snapshot.Version, "owner"));
            else
            {
                snapshot.CheckpointJson = "{\"part\":1}";
                Assert.True(await store.TryUpdateTransferAsync(snapshot, snapshot.Version, "owner"));
            }
        }
        finally { gate.Resume.TrySetResult(true); }
        Assert.True(await renewal);
        var saved = (await store.FindTransferAsync(transfer.Id))!;
        Assert.Equal(snapshot.Version, saved.Version);
        Assert.Equal(snapshot.CheckpointJson, saved.CheckpointJson);
        Assert.Equal(until, saved.LeaseUntil);
        Assert.Equal("owner", saved.LeaseToken);
        Assert.Equal(2, gate.Attempts);
        Assert.Equal(1, gate.Conflicts);
        Assert.False(await store.TryClaimTransferAsync(saved.Id, saved.Version, "competitor", Now + 90_000));
    }

    [Theory]
    [InlineData("takeover")]
    [InlineData("completed")]
    [InlineData("aborted")]
    [InlineData("expired")]
    public async Task Renewal_RechecksOwnershipExpiryAndTerminalState(string change)
    {
        var gate = new RenewalGate();
        await using var env = new FileTestEnvironment();
        env.CustomizeServices = services => RenewalGate.Register(services, env.Root, gate);
        await env.InitializeAsync();
        var store = env.Get<IRemoteDriveStateStore>();
        var transfer = await RemoteDriveLeaseTests.CreateTransferAsync(store);
        var originalUntil = Now + (change == "expired" ? 500 : 30_000);
        Assert.True(await store.TryClaimTransferAsync(transfer.Id, transfer.Version, "owner", originalUntil));
        var snapshot = (await store.FindTransferAsync(transfer.Id))!;
        var renewal = store.RenewTransferAsync(transfer.Id, "owner", Now + 60_000);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            if (change == "takeover")
            {
                snapshot.LeaseToken = null;
                snapshot.LeaseUntil = 0;
                Assert.True(await store.TryUpdateTransferAsync(snapshot, snapshot.Version, "owner"));
                Assert.True(await store.TryClaimTransferAsync(snapshot.Id, snapshot.Version, "new-owner", Now + 60_000));
            }
            else
            {
                snapshot.State = change == "completed" ? RemoteDriveTransferState.Completed :
                    change == "aborted" ? RemoteDriveTransferState.Aborted : snapshot.State;
                snapshot.CheckpointJson = "{\"changed\":true}";
                Assert.True(await store.TryUpdateTransferAsync(snapshot, snapshot.Version, "owner"));
                if (change == "expired") await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, originalUntil - Now + 10)));
            }
        }
        finally { gate.Resume.TrySetResult(true); }
        Assert.False(await renewal);
        var saved = (await store.FindTransferAsync(transfer.Id))!;
        Assert.Equal(change == "takeover" ? "new-owner" : "owner", saved.LeaseToken);
        if (change != "takeover") Assert.Equal(originalUntil, saved.LeaseUntil);
        Assert.Equal(1, gate.Attempts);
    }

    [Fact]
    public async Task Renewal_RepeatedCheckpointConflicts_AreBounded()
    {
        var gate = new RenewalGate();
        await using var env = new FileTestEnvironment();
        env.CustomizeServices = services => RenewalGate.Register(services, env.Root, gate);
        await env.InitializeAsync();
        var store = env.Get<IRemoteDriveStateStore>();
        var transfer = await RemoteDriveLeaseTests.CreateTransferAsync(store);
        var until = Now + 30_000;
        Assert.True(await store.TryClaimTransferAsync(transfer.Id, transfer.Version, "owner", until));
        gate.BeforeEveryAttempt = async (_, ct) =>
        {
            var fresh = (await store.FindTransferAsync(transfer.Id, ct))!;
            fresh.CheckpointJson = "{\"version\":" + fresh.Version + "}";
            Assert.True(await store.TryUpdateTransferAsync(fresh, fresh.Version, "owner", ct));
        };
        Assert.False(await store.RenewTransferAsync(transfer.Id, "owner", Now + 60_000));
        Assert.Equal(3, gate.Attempts);
        Assert.Equal(3, gate.Conflicts);
        Assert.Equal(until, (await store.FindTransferAsync(transfer.Id))!.LeaseUntil);
    }

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

internal sealed class RenewalGate : SaveChangesInterceptor
{
    public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Renewed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Func<RemoteDriveTransfer, CancellationToken, Task>? BeforeEveryAttempt { get; set; }
    public int Attempts { get; private set; }
    public int Conflicts { get; private set; }
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> _renewingContexts = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var transfer = eventData.Context!.ChangeTracker.Entries<RemoteDriveTransfer>().SingleOrDefault();
        if (transfer == null || !transfer.Property(x => x.LeaseUntil).IsModified || transfer.Property(x => x.Version).IsModified)
            return result;
        Attempts++;
        _renewingContexts.TryAdd(eventData.Context.ContextId.InstanceId, 0);
        if (BeforeEveryAttempt != null) await BeforeEveryAttempt(transfer.Entity, cancellationToken);
        else if (Attempts == 1)
        {
            Entered.TrySetResult(true);
            await Resume.Task.WaitAsync(cancellationToken);
        }
        return result;
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Exception is DbUpdateConcurrencyException && _renewingContexts.TryRemove(eventData.Context!.ContextId.InstanceId, out _)) Conflicts++;
        return Task.CompletedTask;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (_renewingContexts.TryRemove(eventData.Context!.ContextId.InstanceId, out _)) Renewed.TrySetResult(true);
        return ValueTask.FromResult(result);
    }

    public static void Register(IServiceCollection services, string root, RenewalGate gate) =>
        services.AddSingleton<IDbContextFactory<FileManagementDbContext>>(new GateFactory(root, gate));

    private sealed class GateFactory : IDbContextFactory<FileManagementDbContext>
    {
        private readonly DbContextOptions<FileManagementDbContext> _options;
        public GateFactory(string root, RenewalGate gate) => _options = new DbContextOptionsBuilder<FileManagementDbContext>()
            .UseSqlite("Data Source=" + Path.Combine(root, "files.db")).AddInterceptors(gate).Options;
        public FileManagementDbContext CreateDbContext() => new(_options);
        public Task<FileManagementDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
