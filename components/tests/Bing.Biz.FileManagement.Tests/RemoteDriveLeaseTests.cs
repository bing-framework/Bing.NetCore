using System.Diagnostics;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class RemoteDriveLeaseTests
{
    private const string OwnerToken = "lease-owner";
    private static readonly TimeSpan LeaseWaitTimeout = TimeSpan.FromSeconds(2);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckpointWriteback_PreservesRenewedLeaseAndExclusivity(bool freeSql)
    {
        await using var env = await CreateEnvironmentAsync(freeSql);
        var store = env.Get<IRemoteDriveStateStore>();
        var transfer = await CreateTransferAsync(store);
        var initialLeaseUntil = UtcNow + 400;
        Assert.True(await store.TryClaimTransferAsync(transfer.Id, transfer.Version, OwnerToken, initialLeaseUntil));

        var staleSnapshot = (await store.FindTransferAsync(transfer.Id))!;
        var versionBeforeRenewal = staleSnapshot.Version;
        var renewedLeaseUntil = UtcNow + 3_000;
        Assert.True(await store.RenewTransferAsync(transfer.Id, OwnerToken, renewedLeaseUntil));

        var renewed = (await store.FindTransferAsync(transfer.Id))!;
        Assert.Equal(versionBeforeRenewal, renewed.Version);
        Assert.Equal(renewedLeaseUntil, renewed.LeaseUntil);
        await WaitPastAsync(initialLeaseUntil);

        staleSnapshot.CheckpointJson = "{\"progress\":1}";
        Assert.True(await store.TryUpdateTransferAsync(staleSnapshot, staleSnapshot.Version, OwnerToken));
        Assert.Equal(versionBeforeRenewal + 1, staleSnapshot.Version);

        var saved = (await store.FindTransferAsync(transfer.Id))!;
        Assert.Equal(renewedLeaseUntil, saved.LeaseUntil);
        Assert.Equal(staleSnapshot.CheckpointJson, saved.CheckpointJson);
        Assert.False(await store.TryClaimTransferAsync(saved.Id, saved.Version, "competing-worker", UtcNow + 3_000));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishWriteback_PreservesRenewedLeaseAndExclusivity(bool freeSql)
    {
        await using var env = await CreateEnvironmentAsync(freeSql);
        var store = env.Get<IRemoteDriveStateStore>();
        var transfer = await CreateTransferAsync(store);
        var initialLeaseUntil = UtcNow + 400;
        Assert.True(await store.TryClaimTransferAsync(transfer.Id, transfer.Version, OwnerToken, initialLeaseUntil));

        var staleSnapshot = (await store.FindTransferAsync(transfer.Id))!;
        var versionBeforeRenewal = staleSnapshot.Version;
        var renewedLeaseUntil = UtcNow + 3_000;
        Assert.True(await store.RenewTransferAsync(transfer.Id, OwnerToken, renewedLeaseUntil));
        await WaitPastAsync(initialLeaseUntil);

        var value = CreateObject(staleSnapshot);
        Assert.True(await store.TryPublishAsync(value, 0, staleSnapshot, staleSnapshot.Version, OwnerToken));
        Assert.Equal(versionBeforeRenewal + 1, staleSnapshot.Version);
        Assert.Equal(1L, value.Version);

        var saved = (await store.FindTransferAsync(transfer.Id))!;
        Assert.Equal(renewedLeaseUntil, saved.LeaseUntil);
        Assert.Equal(OwnerToken, saved.LeaseToken);
        Assert.NotNull(await store.FindObjectAsync(value.Id));
        Assert.False(await store.TryClaimTransferAsync(saved.Id, saved.Version, "competing-worker", UtcNow + 3_000));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedRenewalAndCheckpoint_PreserveVersionAndRequireOwnerToRelease(bool freeSql)
    {
        await using var env = await CreateEnvironmentAsync(freeSql);
        var store = env.Get<IRemoteDriveStateStore>();
        var transfer = await CreateTransferAsync(store);
        var initialLeaseUntil = UtcNow + 350;
        Assert.True(await store.TryClaimTransferAsync(transfer.Id, transfer.Version, OwnerToken, initialLeaseUntil));

        var snapshot = (await store.FindTransferAsync(transfer.Id))!;
        var expectedVersion = snapshot.Version;
        long renewedLeaseUntil = 0;
        for (var round = 1; round <= 4; round++)
        {
            await Task.Delay(120);
            renewedLeaseUntil = UtcNow + 1_500;
            Assert.True(await store.RenewTransferAsync(snapshot.Id, OwnerToken, renewedLeaseUntil));

            var afterRenewal = (await store.FindTransferAsync(snapshot.Id))!;
            Assert.Equal(expectedVersion, afterRenewal.Version);
            Assert.Equal(renewedLeaseUntil, afterRenewal.LeaseUntil);

            snapshot.CheckpointJson = "{\"round\":" + round + "}";
            Assert.Equal(initialLeaseUntil, snapshot.LeaseUntil);
            Assert.True(await store.TryUpdateTransferAsync(snapshot, snapshot.Version, OwnerToken));
            expectedVersion++;
            Assert.Equal(expectedVersion, snapshot.Version);

            var afterCheckpoint = (await store.FindTransferAsync(snapshot.Id))!;
            Assert.Equal(expectedVersion, afterCheckpoint.Version);
            Assert.Equal(renewedLeaseUntil, afterCheckpoint.LeaseUntil);
            Assert.Equal(snapshot.CheckpointJson, afterCheckpoint.CheckpointJson);
        }

        Assert.True(UtcNow > initialLeaseUntil);
        var unownedSnapshot = (await store.FindTransferAsync(snapshot.Id))!;
        var versionBeforeUnownedWriteback = unownedSnapshot.Version;
        unownedSnapshot.LeaseToken = null;
        unownedSnapshot.LeaseUntil = 0;
        unownedSnapshot.CheckpointJson = "{\"writer\":\"unowned-snapshot\"}";
        Assert.True(await store.TryUpdateTransferAsync(unownedSnapshot, unownedSnapshot.Version));
        Assert.Equal(versionBeforeUnownedWriteback + 1, unownedSnapshot.Version);

        var stillLeased = (await store.FindTransferAsync(snapshot.Id))!;
        Assert.Equal(OwnerToken, stillLeased.LeaseToken);
        Assert.Equal(renewedLeaseUntil, stillLeased.LeaseUntil);
        Assert.Equal(unownedSnapshot.CheckpointJson, stillLeased.CheckpointJson);

        stillLeased.LeaseToken = null;
        stillLeased.LeaseUntil = 0;
        Assert.False(await store.TryUpdateTransferAsync(stillLeased, stillLeased.Version, "wrong-owner"));

        Assert.True(await store.TryUpdateTransferAsync(stillLeased, stillLeased.Version, OwnerToken));
        var released = (await store.FindTransferAsync(stillLeased.Id))!;
        Assert.Null(released.LeaseToken);
        Assert.Equal(0L, released.LeaseUntil);
        Assert.True(await store.TryClaimTransferAsync(released.Id, released.Version, "next-owner", UtcNow + 3_000));
        Assert.Equal("next-owner", (await store.FindTransferAsync(stillLeased.Id))!.LeaseToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredLeaseCanBeTakenOver_ButOldTokenCannotWriteOrPublishAtLatestVersion(bool freeSql)
    {
        await using var env = await CreateEnvironmentAsync(freeSql);
        var store = env.Get<IRemoteDriveStateStore>();
        var transfer = await CreateTransferAsync(store);
        var initialLeaseUntil = UtcNow + 400;
        Assert.True(await store.TryClaimTransferAsync(transfer.Id, transfer.Version, OwnerToken, initialLeaseUntil));

        var staleSnapshot = (await store.FindTransferAsync(transfer.Id))!;
        await WaitPastAsync(initialLeaseUntil);

        const string nextToken = "next-owner";
        var takeoverUntil = UtcNow + 3_000;
        Assert.True(await store.TryClaimTransferAsync(staleSnapshot.Id, staleSnapshot.Version, nextToken, takeoverUntil));
        var takenOver = (await store.FindTransferAsync(staleSnapshot.Id))!;
        Assert.Equal(nextToken, takenOver.LeaseToken);
        Assert.Equal(staleSnapshot.Version + 1, takenOver.Version);

        staleSnapshot.Version = takenOver.Version;
        staleSnapshot.CheckpointJson = "{\"writer\":\"old-owner\"}";
        Assert.False(await store.TryUpdateTransferAsync(staleSnapshot, takenOver.Version, OwnerToken));

        var oldValue = CreateObject(staleSnapshot);
        Assert.False(await store.TryPublishAsync(oldValue, 0, staleSnapshot, takenOver.Version, OwnerToken));

        var unchanged = (await store.FindTransferAsync(staleSnapshot.Id))!;
        Assert.Equal(nextToken, unchanged.LeaseToken);
        Assert.Equal(takeoverUntil, unchanged.LeaseUntil);
        Assert.Equal("{}", unchanged.CheckpointJson);
        Assert.Null(await store.FindObjectAsync(oldValue.Id));
    }

    private static async Task<FileTestEnvironment> CreateEnvironmentAsync(bool freeSql)
    {
        var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        return env;
    }

    internal static async Task<RemoteDriveTransfer> CreateTransferAsync(IRemoteDriveStateStore store)
    {
        var now = UtcNow;
        var storeName = "remote-lease-" + Guid.NewGuid().ToString("N");
        var connection = new RemoteDriveConnection
        {
            Id = Guid.NewGuid(),
            TemplateName = "lease-test",
            Provider = "aliyun-drive",
            StoreName = storeName,
            ScopeKind = StorageScopeKind.Host,
            ScopeKey = "lease-test-scope",
            Ownership = RemoteDriveOwnership.Shared,
            OwnerId = "admin",
            DisplayName = storeName,
            RemoteAccountId = "account-a",
            DriveId = "drive-a",
            RootFolderId = "root",
            EncryptedCredentials = "protected",
            EncryptedAuthorization = "",
            State = RemoteDriveConnectionState.Active,
            Version = 1,
            CreatedAt = now
        };
        Assert.True(await store.TryCreateConnectionAsync(connection));

        var transfer = new RemoteDriveTransfer
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            StoreName = storeName,
            ScopeKey = connection.ScopeKey,
            Container = "attachments",
            ObjectKey = "lease-object-" + Guid.NewGuid().ToString("N"),
            StageStoreName = "stage-v1",
            StageContainer = "stage",
            StageObjectKey = Guid.NewGuid().ToString("N"),
            StageMetadataJson = "{}",
            MetadataJson = "{}",
            ManifestJson = "[]",
            CheckpointJson = "{}",
            ExpectedObjectVersion = 0,
            ContentType = "application/octet-stream",
            Length = 12,
            PartSize = 5,
            State = RemoteDriveTransferState.Verified,
            ExpiresAt = now + 60_000,
            CreatedAt = now,
            Version = 1
        };
        Assert.True(await store.TryCreateTransferAsync(transfer));
        return transfer;
    }

    internal static RemoteDriveObject CreateObject(RemoteDriveTransfer transfer) => new()
    {
        Id = RemoteDriveObject.Key(transfer.StoreName, transfer.ScopeKey, new FileAddress(transfer.Container, transfer.ObjectKey)),
        ConnectionId = transfer.ConnectionId,
        TransferId = transfer.Id,
        StoreName = transfer.StoreName,
        ScopeKey = transfer.ScopeKey,
        Container = transfer.Container,
        ObjectKey = transfer.ObjectKey,
        RemoteFileJson = "{}",
        MetadataJson = transfer.MetadataJson,
        ContentType = transfer.ContentType,
        Sha256 = new string('c', 64),
        Length = transfer.Length,
        Version = 1
    };

    private static long UtcNow => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static async Task WaitPastAsync(long leaseUntil)
    {
        using var timeout = new CancellationTokenSource(LeaseWaitTimeout);
        var stopwatch = Stopwatch.StartNew();
        while (UtcNow <= leaseUntil)
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }

        Assert.True(stopwatch.Elapsed < LeaseWaitTimeout, "租约未能在诊断超时内自然到期。");
    }
}
