using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.FreeSql;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Bing.Biz.FileManagement.FreeSql.Tests;

public sealed class RemoteDrivePersistenceTests
{
    [Fact]
    public async Task ConnectionClaim_UsesVersionAndExpiredLease_AndWritebackChecksPersistedToken()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var store = (IRemoteDriveStateStore)fixture.Store;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var connection = Connection("remote-claim", leaseToken: "expired-worker", leaseUntil: now - 1);
        Assert.True(await store.TryCreateConnectionAsync(connection));

        Assert.True(await store.TryClaimConnectionAsync(connection.Id, connection.Version, "worker-a", now + 60_000));
        Assert.False(await store.TryClaimConnectionAsync(connection.Id, connection.Version, "worker-b", now + 60_000));
        var claimed = (await store.FindConnectionAsync(connection.Id))!;
        Assert.Equal("worker-a", claimed.LeaseToken);
        Assert.Equal(connection.Version + 1, claimed.Version);

        var clearing = Connection("remote-token-writeback", leaseToken: "refresh-worker", leaseUntil: now + 60_000);
        Assert.True(await store.TryCreateConnectionAsync(clearing));
        clearing = (await store.FindConnectionAsync(clearing.Id))!;
        clearing.State = RemoteDriveConnectionState.ReauthorizationRequired;
        clearing.LeaseToken = null;
        clearing.LeaseUntil = 0;
        Assert.True(await store.TryUpdateConnectionAsync(clearing, clearing.Version, "refresh-worker"));
        Assert.Null((await store.FindConnectionAsync(clearing.Id))!.LeaseToken);
        Assert.Equal(2, clearing.Version);
    }

    [Fact]
    public async Task Publish_IsAtomicWithTransferAndConnection_AndTombstoneFencesStaleWork()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var store = (IRemoteDriveStateStore)fixture.Store;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var connection = Connection("remote-publish");
        Assert.True(await store.TryCreateConnectionAsync(connection));

        var transfer = Transfer(connection, "object-a", 0, now);
        transfer.State = RemoteDriveTransferState.Uploading;
        Assert.True(await store.TryCreateTransferAsync(transfer));
        Assert.True(await store.TryClaimTransferAsync(transfer.Id, transfer.Version, "publish-a", now + 60_000));
        transfer = (await store.FindTransferAsync(transfer.Id))!;
        transfer.State = RemoteDriveTransferState.Verified;
        var value = Object(transfer);
        Assert.True(await store.TryPublishAsync(value, 0, transfer, transfer.Version, "publish-a"));
        Assert.Equal(1, value.Version);

        var transferVersion = transfer.Version;
        Assert.True(await store.RenewTransferAsync(transfer.Id, "publish-a", now + 120_000));
        Assert.Equal(transferVersion, (await store.FindTransferAsync(transfer.Id))!.Version);
        Assert.True(await store.TryDeleteObjectAsync(value.Id, value.Version));
        var tombstone = (await store.FindObjectAsync(value.Id))!;
        Assert.True(tombstone.Deleted);
        Assert.False(await store.TryPublishAsync(Object(transfer), 0, transfer, transfer.Version, "publish-a"));
        Assert.True((await store.FindObjectAsync(value.Id))!.Deleted);

        var replacement = Transfer(connection, "object-a", tombstone.Version, now);
        Assert.True(await store.TryCreateTransferAsync(replacement));
        Assert.True(await store.TryClaimTransferAsync(replacement.Id, replacement.Version, "publish-b", now + 60_000));
        replacement = (await store.FindTransferAsync(replacement.Id))!;
        connection.State = RemoteDriveConnectionState.Disabled;
        Assert.True(await store.TryUpdateConnectionAsync(connection, connection.Version));
        Assert.False(await store.TryPublishAsync(Object(replacement), tombstone.Version, replacement, replacement.Version, "publish-b"));
        Assert.True((await store.FindObjectAsync(value.Id))!.Deleted);
    }

    [Fact]
    public async Task CleanupQueue_IncludesUncleanVerifiedAbortedAndExpiredTransfers()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var store = (IRemoteDriveStateStore)fixture.Store;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var connection = Connection("remote-cleanup");
        Assert.True(await store.TryCreateConnectionAsync(connection));
        var verified = Transfer(connection, "verified", 0, now);
        var aborted = Transfer(connection, "aborted", 0, now); aborted.State = RemoteDriveTransferState.Aborted;
        var expired = Transfer(connection, "expired", 0, now); expired.State = RemoteDriveTransferState.Receiving; expired.ExpiresAt = now - 1;
        var completed = Transfer(connection, "completed", 0, now); completed.State = RemoteDriveTransferState.Completed;
        var cleaned = Transfer(connection, "cleaned", 0, now); cleaned.CleanupCompleted = true;
        foreach (var transfer in new[] { verified, aborted, expired, completed, cleaned })
            Assert.True(await store.TryCreateTransferAsync(transfer));

        var pending = await store.ListCleanupAsync(now, 10);
        Assert.Equal(new[] { verified.Id, aborted.Id, expired.Id }.OrderBy(x => x), pending.Select(x => x.Id).OrderBy(x => x));
        Assert.True(await store.TryClaimTransferAsync(aborted.Id, aborted.Version, "cleanup-worker", now + 60_000));
    }

    [Fact]
    public async Task LargeMultipartManifestAndCheckpoint_RoundTripWithoutTruncation()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var store = (IRemoteDriveStateStore)fixture.Store;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var connection = Connection("remote-large-json");
        Assert.True(await store.TryCreateConnectionAsync(connection));
        var parts = Enumerable.Range(1, 10_000).Select(number => new RemoteDrivePartReceipt
        {
            Number = number, Length = 16L * 1024 * 1024, ETag = number.ToString("x8") + new string('e', 240), Sha256 = new string('a', 44)
        }).ToArray();
        var transfer = Transfer(connection, "large-manifest", 0, now);
        transfer.ManifestJson = JsonSerializer.Serialize(parts);
        transfer.CheckpointJson = JsonSerializer.Serialize(new RemoteDriveCheckpoint
        {
            Name = "large-manifest", Parts = parts.ToDictionary(x => x.Number, x => x.ETag)
        });
        Assert.True(await store.TryCreateTransferAsync(transfer));
        var saved = (await store.FindTransferAsync(transfer.Id))!;
        Assert.Equal(10_000, JsonSerializer.Deserialize<RemoteDrivePartReceipt[]>(saved.ManifestJson)!.Length);
        Assert.Equal(10_000, JsonSerializer.Deserialize<RemoteDriveCheckpoint>(saved.CheckpointJson)!.Parts.Count);
        Assert.Equal(transfer.ManifestJson, saved.ManifestJson);
        Assert.Equal(transfer.CheckpointJson, saved.CheckpointJson);
    }

    [Fact]
    public async Task PreferencesAreSeparatedByOwner_AndCopyRequestKeyIsUnique()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var targets = (IFileStorageTargetStore)fixture.Store;
        var shared = new FileStoragePreference { Id = new string('a', 64), ScopeKey = "scope-a", OwnerId = "", Container = "attachments", TargetId = "shared-target" };
        var personal = new FileStoragePreference { Id = new string('b', 64), ScopeKey = "scope-a", OwnerId = "user-a", Container = "attachments", TargetId = "personal-target" };
        Assert.True(await targets.SavePreferenceAsync(shared, 0));
        Assert.True(await targets.SavePreferenceAsync(personal, 0));
        Assert.Equal("shared-target", (await targets.FindPreferenceAsync("scope-a", "", "attachments"))!.TargetId);
        Assert.Equal("personal-target", (await targets.FindPreferenceAsync("scope-a", "user-a", "attachments"))!.TargetId);

        var source = Source("copy-source-1");
        await fixture.Orm.Insert(source).ExecuteAffrowsAsync();
        var first = Copy(source, "copy-request-1");
        Assert.True(await targets.CreateCopyAsync(first.Request, first.Target, first.Operation));
        var duplicate = Copy(source, "copy-request-1");
        Assert.False(await targets.CreateCopyAsync(duplicate.Request, duplicate.Target, duplicate.Operation));
        Assert.Equal(1, await fixture.Orm.Select<FileBindingCopyRequest>().CountAsync());
        Assert.Equal(1, await fixture.Orm.Select<FileOperation>().Where(x => x.Kind == FileOperationKind.CopyForBinding).CountAsync());
    }

    [Fact]
    public async Task CopyCancelAndPublish_OnlyOneRequestVersionCanCommit()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var targets = (IFileStorageTargetStore)fixture.Store;
        var store = fixture.Store;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var source = Source("copy-race-source");
        await fixture.Orm.Insert(source).ExecuteAffrowsAsync();

        var first = Copy(source, "copy-cancel-first");
        Assert.True(await targets.CreateCopyAsync(first.Request, first.Target, first.Operation));
        var firstSnapshots = await CopySnapshots(targets, store, first.Request.Id);
        await ClaimCopyOperation(store, first.Operation.Id, now);
        var firstTransfer = await AddTransferAsync(fixture.Orm, firstSnapshots.Target.File, now, "copy-cancel-first-object");
        var cancel = Clone(firstSnapshots.Request);
        cancel.State = FileBindingCopyState.Cancelled;
        var cancelTarget = Clone(firstSnapshots.Target);
        cancelTarget.File.State = FileState.DeletePending;
        Assert.True(await targets.UpdateCopyAsync(cancel, 1, cancelTarget, 1, new[] { DeleteOperation(cancelTarget.File.Id) }));
        var stalePublish = Clone(firstSnapshots.Request);
        stalePublish.State = FileBindingCopyState.Copied;
        var stalePublishTarget = Clone(firstSnapshots.Target);
        stalePublishTarget.File.State = FileState.Ready;
        stalePublishTarget.File.ObjectKey = firstTransfer.ObjectKey;
        Assert.False(await targets.UpdateCopyAsync(stalePublish, 1, stalePublishTarget, 1,
            operationLease: new FileOperationLease(first.Operation.Id, "copy-worker"),
            transferLease: new FileTransferLease(firstTransfer.Id, firstTransfer.LeaseToken)));
        Assert.Equal(FileBindingCopyState.Cancelled, (await targets.FindCopyAsync(first.Request.Id))!.State);
        Assert.Equal(FileTransferState.Active, (await fixture.Orm.Select<FileTransferAttempt>().Where(x => x.Id == firstTransfer.Id).FirstAsync())!.State);

        var second = Copy(source, "copy-publish-first");
        Assert.True(await targets.CreateCopyAsync(second.Request, second.Target, second.Operation));
        var secondSnapshots = await CopySnapshots(targets, store, second.Request.Id);
        await ClaimCopyOperation(store, second.Operation.Id, now);
        var secondTransfer = await AddTransferAsync(fixture.Orm, secondSnapshots.Target.File, now, "copy-publish-first-object");
        var publish = Clone(secondSnapshots.Request);
        publish.State = FileBindingCopyState.Copied;
        var publishTarget = Clone(secondSnapshots.Target);
        publishTarget.File.State = FileState.Ready;
        publishTarget.File.ObjectKey = secondTransfer.ObjectKey;
        Assert.True(await targets.UpdateCopyAsync(publish, 1, publishTarget, 1,
            operationLease: new FileOperationLease(second.Operation.Id, "copy-worker"),
            transferLease: new FileTransferLease(secondTransfer.Id, secondTransfer.LeaseToken), validateSource: true));
        var staleCancel = Clone(secondSnapshots.Request);
        staleCancel.State = FileBindingCopyState.Cancelled;
        var staleCancelTarget = Clone(secondSnapshots.Target);
        staleCancelTarget.File.State = FileState.DeletePending;
        Assert.False(await targets.UpdateCopyAsync(staleCancel, 1, staleCancelTarget, 1,
            new[] { DeleteOperation(staleCancelTarget.File.Id) }));
        Assert.Equal(FileBindingCopyState.Copied, (await targets.FindCopyAsync(second.Request.Id))!.State);
    }

    [Fact]
    public async Task CopyForBinding_UsesLongQueue_AndFailureMarksRequestAndSchedulesDelete()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var targets = (IFileStorageTargetStore)fixture.Store;
        var store = fixture.Store;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var source = Source("copy-failure-source");
        await fixture.Orm.Insert(source).ExecuteAffrowsAsync();
        var copy = Copy(source, "copy-failure-request");
        Assert.True(await targets.CreateCopyAsync(copy.Request, copy.Target, copy.Operation));

        Assert.Equal(copy.Operation.Id, Assert.Single(await store.ListDueOperationsAsync(now, 10, FileOperationQueue.Long)).Id);
        Assert.Empty(await store.ListDueOperationsAsync(now, 10, FileOperationQueue.Short));
        Assert.True(await store.TryClaimAsync(copy.Operation.Id, 1, "copy-worker", now, now + 60_000));
        Assert.True(await store.FinishOperationAsync(copy.Operation.Id, "copy-worker", FileOperationState.Failed, 0, "copy_error"));
        Assert.Equal(FileBindingCopyState.Failed, (await targets.FindCopyAsync(copy.Request.Id))!.State);
        var target = await store.FindAsync(copy.Request.CopyFileId);
        Assert.Equal(FileState.DeletePending, target!.File.State);
        var due = await store.ListDueOperationsAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 10, FileOperationQueue.Short);
        Assert.Contains(due, x => x.FileId == target.File.Id && x.Kind == FileOperationKind.Delete);
    }

    [Fact]
    public async Task RegistrationSharesOneScopedStoreForAllPorts()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var services = new ServiceCollection();
        services.AddBingFileManagementFreeSql(_ => fixture.Orm);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFileManagementStore>();
        Assert.Same(store, scope.ServiceProvider.GetRequiredService<IRemoteDriveStateStore>());
        Assert.Same(store, scope.ServiceProvider.GetRequiredService<IFileStorageTargetStore>());
    }

    private static RemoteDriveConnection Connection(string storeName, string? leaseToken = null, long leaseUntil = 0) => new()
    {
        Id = Guid.NewGuid(), TemplateName = "aliyun-test", Provider = "aliyun-drive", StoreName = storeName,
        ScopeKind = StorageScopeKind.Host, ScopeKey = "scope-a", Ownership = RemoteDriveOwnership.Shared, OwnerId = "admin",
        DisplayName = storeName, RemoteAccountId = "account-a", DriveId = "drive-a", RootFolderId = "root",
        State = RemoteDriveConnectionState.Active, LeaseToken = leaseToken, LeaseUntil = leaseUntil,
        EncryptedCredentials = "protected", EncryptedAuthorization = "", CreatedAt = 100, Version = 1
    };

    private static RemoteDriveTransfer Transfer(RemoteDriveConnection connection, string key, long objectVersion, long now) => new()
    {
        Id = Guid.NewGuid(), ConnectionId = connection.Id, StoreName = connection.StoreName, ScopeKey = connection.ScopeKey,
        Container = "attachments", ObjectKey = key, StageStoreName = "stage-v1", StageContainer = "stage", StageObjectKey = Guid.NewGuid().ToString("N"),
        StageMetadataJson = "{}", MetadataJson = "{}", ManifestJson = "[]", CheckpointJson = "{}", ExpectedObjectVersion = objectVersion,
        ContentType = "application/octet-stream", Length = 12, PartSize = 5, State = RemoteDriveTransferState.Verified,
        ExpiresAt = now + 60_000, CreatedAt = now, Version = 1
    };

    private static RemoteDriveObject Object(RemoteDriveTransfer transfer) => new()
    {
        Id = RemoteDriveObject.Key(transfer.StoreName, transfer.ScopeKey, new FileAddress(transfer.Container, transfer.ObjectKey)),
        ConnectionId = transfer.ConnectionId, TransferId = transfer.Id, StoreName = transfer.StoreName, ScopeKey = transfer.ScopeKey,
        Container = transfer.Container, ObjectKey = transfer.ObjectKey, RemoteFileJson = "{}", MetadataJson = transfer.MetadataJson,
        ContentType = transfer.ContentType, Sha256 = new string('c', 64), Length = transfer.Length, Version = 1
    };

    private static FileRecord Source(string requestKey) => new()
    {
        Id = Guid.NewGuid(), RequestKey = requestKey, ScopeKey = "scope-a", ScopeKind = StorageScopeKind.Host,
        StoreName = "personal-store", Container = "attachments", ObjectKey = Guid.NewGuid().ToString("N"),
        FileName = requestKey + ".txt", Length = 12, ContentType = "text/plain", Visibility = FileVisibility.Private,
        State = FileState.Ready, CreatorId = "user-a", Ownership = RemoteDriveOwnership.Personal, CreatedAt = 100, Version = 1
    };

    private static (FileBindingCopyRequest Request, FileEntry Target, FileOperation Operation) Copy(FileRecord source, string requestKey)
    {
        var id = Guid.NewGuid();
        var target = new FileRecord
        {
            Id = id, RequestKey = "target-" + requestKey, ScopeKey = source.ScopeKey, ScopeKind = source.ScopeKind,
            StoreName = "shared-store", Container = source.Container, ObjectKey = "copy-" + id.ToString("N"),
            FileName = source.FileName, Length = source.Length, ContentType = source.ContentType, Visibility = FileVisibility.Private,
            State = FileState.Pending, CreatorId = "user-a", Ownership = RemoteDriveOwnership.Shared, CreatedAt = 200, Version = 1
        };
        var request = new FileBindingCopyRequest
        {
            Id = Guid.NewGuid(), RequestKey = requestKey, ScopeKey = source.ScopeKey, OwnerId = source.CreatorId,
            SourceFileId = source.Id, SourceVersion = source.Version, CopyFileId = id, BusinessType = "orders", BusinessId = "order-1", CreatedAt = 200
        };
        var operation = new FileOperation { Id = Guid.NewGuid(), FileId = id, Kind = FileOperationKind.CopyForBinding,
            State = FileOperationState.Pending, Payload = request.Id.ToString("N"), NextAttemptAt = 0, Version = 1, CreatedAt = 200 };
        return (request, new FileEntry { File = target }, operation);
    }

    private static FileOperation DeleteOperation(Guid fileId) => new()
    {
        Id = Guid.NewGuid(), FileId = fileId, Kind = FileOperationKind.Delete, State = FileOperationState.Pending, Version = 1,
        Payload = "{}", NextAttemptAt = 0, CreatedAt = 300
    };

    private static FileBindingCopyRequest Clone(FileBindingCopyRequest value) => new()
    {
        Id = value.Id, RequestKey = value.RequestKey, ScopeKey = value.ScopeKey, OwnerId = value.OwnerId,
        SourceFileId = value.SourceFileId, SourceVersion = value.SourceVersion, CopyFileId = value.CopyFileId,
        BusinessType = value.BusinessType, BusinessId = value.BusinessId, State = value.State, ErrorCode = value.ErrorCode,
        CreatedAt = value.CreatedAt, Version = value.Version
    };

    private static FileEntry Clone(FileEntry value) => new()
    {
        File = Clone(value.File),
        Session = value.Session == null ? null : new FileUploadSession
        {
            Id = value.Session.Id, FileId = value.Session.FileId, OwnerId = value.Session.OwnerId,
            RequestedStorageTargetId = value.Session.RequestedStorageTargetId, BackendUploadId = value.Session.BackendUploadId,
            BackendMetadataJson = value.Session.BackendMetadataJson, TotalLength = value.Session.TotalLength, PartSize = value.Session.PartSize,
            State = value.Session.State, WriteMode = value.Session.WriteMode, ExpiresAt = value.Session.ExpiresAt,
            AbsoluteExpiresAt = value.Session.AbsoluteExpiresAt, LastActivityAt = value.Session.LastActivityAt,
            ClientLastModified = value.Session.ClientLastModified, Version = value.Session.Version
        },
        Bindings = value.Bindings.Select(binding => new FileBinding
        {
            Id = binding.Id, FileId = binding.FileId, BusinessType = binding.BusinessType,
            BusinessId = binding.BusinessId, CreatorId = binding.CreatorId, CreatedAt = binding.CreatedAt
        }).ToList()
    };

    private static FileRecord Clone(FileRecord value) => new()
    {
        Id = value.Id, RequestKey = value.RequestKey, ScopeKey = value.ScopeKey, TenantId = value.TenantId,
        ScopeKind = value.ScopeKind, StoreName = value.StoreName, ConnectionId = value.ConnectionId, Ownership = value.Ownership,
        Container = value.Container, ObjectKey = value.ObjectKey, FileName = value.FileName, Length = value.Length,
        ContentType = value.ContentType, Visibility = value.Visibility, State = value.State, CreatorId = value.CreatorId,
        CreatedAt = value.CreatedAt, Version = value.Version, Sha256 = value.Sha256, ContentPolicyJson = value.ContentPolicyJson
    };

    private static async Task<(FileBindingCopyRequest Request, FileEntry Target)> CopySnapshots(
        IFileStorageTargetStore targets, FreeSqlFileManagementStore files, Guid id)
    {
        var request = (await targets.FindCopyAsync(id))!;
        return (request, (await files.FindAsync(request.CopyFileId))!);
    }

    private static async Task ClaimCopyOperation(FreeSqlFileManagementStore store, Guid operationId, long now)
        => Assert.True(await store.TryClaimAsync(operationId, 1, "copy-worker", now, now + 60_000));

    private static async Task<FileTransferAttempt> AddTransferAsync(IFreeSql orm, FileRecord file, long now, string key)
    {
        var attempt = new FileTransferAttempt
        {
            Id = Guid.NewGuid(), FileId = file.Id, ScopeKey = file.ScopeKey, ScopeKind = file.ScopeKind, TenantId = file.TenantId,
            StoreName = file.StoreName, Container = file.Container, ObjectKey = key, LeaseToken = "transfer-worker",
            LeaseUntil = now + 60_000, NextCheckAt = now + 60_000, State = FileTransferState.Active, WriterCompleted = true, Version = 1
        };
        await orm.Insert(attempt).ExecuteAffrowsAsync();
        return attempt;
    }

    private sealed class StoreFixture : IAsyncDisposable
    {
        private readonly string _path;

        private StoreFixture(string path, IFreeSql orm)
        {
            _path = path;
            Orm = orm;
            Store = new FreeSqlFileManagementStore(orm);
        }

        internal IFreeSql Orm { get; }
        internal FreeSqlFileManagementStore Store { get; }

        internal static Task<StoreFixture> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"bing-file-management-remote-drive-{Guid.NewGuid():N}.db");
            var orm = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, "Data Source=" + path)
                .UseAutoSyncStructure(false).Build();
            FileManagementFreeSqlSchema.SyncStructure(orm);
            return Task.FromResult(new StoreFixture(path, orm));
        }

        public ValueTask DisposeAsync()
        {
            Orm.Dispose();
            if (File.Exists(_path)) File.Delete(_path);
            return ValueTask.CompletedTask;
        }
    }
}
