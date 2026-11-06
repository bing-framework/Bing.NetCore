using System.Security.Cryptography;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed partial class RemoteDriveWorkflowTests
{
    [Fact]
    public async Task HeartbeatDatabaseFailure_PreservesRecoverableOperationAndReleasesLease()
    {
        using var client = new DriveFixture();
        await using var env = Setup(false, client, TimeSpan.FromSeconds(30));
        var gate = new RenewalGate { BeforeEveryAttempt = (_, _) => throw new IOException("injected heartbeat database failure") };
        var configure = env.CustomizeServices;
        env.CustomizeServices = services => { configure!(services); RenewalGate.Register(services, env.Root, gate); };
        await env.InitializeAsync();
        var connection = await Connect(env);
        Guid transferId = default;
        client.BeforeUploadCheckpoint = async (checkpoint, _, ct) =>
        {
            transferId = Guid.ParseExact(checkpoint.Name[5..], "N");
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var upload = await PrepareHeartbeatUpload(env, connection, deadline.Token);
        await env.Get<FileOperationProcessor>().RunBatchAsync(deadline.Token);
        Assert.False(deadline.IsCancellationRequested);
        Assert.Equal(FileState.Completing, (await env.Get<IFileManagementStore>().FindAsync(upload.FileId))!.File.State);
        var operations = await OperationsFor(env, upload.FileId);
        var completion = Assert.Single(operations, x => x.Kind == FileOperationKind.Complete);
        Assert.Equal(FileOperationState.Pending, completion.State);
        Assert.Equal("storage:Unavailable", completion.LastError);
        Assert.DoesNotContain(operations, x => x.Kind == FileOperationKind.Abort);
        var transfer = (await env.Get<IRemoteDriveStateStore>().FindTransferAsync(transferId))!;
        Assert.Null(transfer.LeaseToken);
        Assert.Equal(0, transfer.LeaseUntil);
        Assert.Equal(RemoteDriveTransferState.Uploading, transfer.State);
    }

    [Fact]
    public async Task HeartbeatCheckpointRace_CompleteOperationPublishesWithoutAbort()
    {
        using var client = new DriveFixture();
        await using var env = Setup(false, client, TimeSpan.FromSeconds(30));
        var gate = new RenewalGate();
        var configure = env.CustomizeServices;
        env.CustomizeServices = services => { configure!(services); RenewalGate.Register(services, env.Root, gate); };
        await env.InitializeAsync();
        var connection = await Connect(env);
        var store = env.Get<IRemoteDriveStateStore>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        client.BeforeUploadCheckpoint = async (checkpoint, persist, ct) =>
        {
            await gate.Entered.Task.WaitAsync(ct);
            try
            {
                checkpoint.Parts[1] = "verified-before-renewal";
                await persist(checkpoint, ct);
                var current = (await store.FindTransferAsync(Guid.ParseExact(checkpoint.Name[5..], "N"), ct))!;
                Assert.True(current.LeaseUntil > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Assert.False(await store.TryClaimTransferAsync(current.Id, current.Version, "competitor",
                    DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds(), ct));
            }
            finally { gate.Resume.TrySetResult(true); }
            await gate.Renewed.Task.WaitAsync(ct);
        };
        var upload = await PrepareHeartbeatUpload(env, connection, deadline.Token);
        await env.Get<FileOperationProcessor>().RunBatchAsync(deadline.Token);
        Assert.False(deadline.IsCancellationRequested);
        Assert.Equal(1, gate.Conflicts);
        Assert.Equal(2, gate.Attempts);
        Assert.Equal(FileState.Ready, (await env.Get<IFileManagementStore>().FindAsync(upload.FileId))!.File.State);
        var operations = await OperationsFor(env, upload.FileId);
        Assert.Equal(FileOperationState.Succeeded, Assert.Single(operations, x => x.Kind == FileOperationKind.Complete).State);
        Assert.DoesNotContain(operations, x => x.Kind == FileOperationKind.Abort);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeartbeatLeaseLoss_StopsIoAndRetriesAfterRestartWithoutFailingFile(bool freeSql)
    {
        using var client = new DriveFixture();
        await using var env = Setup(freeSql, client, TimeSpan.FromSeconds(30));
        await env.InitializeAsync();
        var connection = await Connect(env);
        var store = env.Get<IRemoteDriveStateStore>();
        Guid transferId = default;
        var cancelledInternally = false;
        client.BeforeUploadCheckpoint = async (checkpoint, _, ct) =>
        {
            transferId = Guid.ParseExact(checkpoint.Name[5..], "N");
            var transfer = (await store.FindTransferAsync(transferId, ct))!;
            var oldToken = transfer.LeaseToken!;
            transfer.LeaseToken = null;
            transfer.LeaseUntil = 0;
            Assert.True(await store.TryUpdateTransferAsync(transfer, transfer.Version, oldToken, ct));
            Assert.True(await store.TryClaimTransferAsync(transfer.Id, transfer.Version, "successor",
                DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds(), ct));
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { cancelledInternally = true; throw; }
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var upload = await PrepareHeartbeatUpload(env, connection, deadline.Token);
        await env.Get<FileOperationProcessor>().RunBatchAsync(deadline.Token);
        Assert.True(cancelledInternally);
        Assert.False(deadline.IsCancellationRequested);
        var file = (await env.Get<IFileManagementStore>().FindAsync(upload.FileId))!;
        Assert.Equal(FileState.Completing, file.File.State);
        var operations = await OperationsFor(env, upload.FileId);
        var completion = Assert.Single(operations, x => x.Kind == FileOperationKind.Complete);
        Assert.Equal(FileOperationState.Pending, completion.State);
        Assert.Equal("storage:Conflict", completion.LastError);
        Assert.DoesNotContain(operations, x => x.Kind == FileOperationKind.Abort);
        Assert.Null(await store.FindObjectAsync(RemoteDriveObject.Key(connection.StoreName, connection.ScopeKey, file.File.Address())));
        var owned = (await store.FindTransferAsync(transferId))!;
        Assert.Equal("successor", owned.LeaseToken);
        Assert.Equal(RemoteDriveTransferState.Uploading, owned.State);
        Assert.NotNull(await env.Get<IFileStorageFactory>().GetStorage(owned.StageStoreName, connection.Scope())
            .GetPropertiesAsync(new(owned.StageContainer, owned.StageObjectKey)));

        // 后继执行者结束持有；重启后沿用持久检查点和中转对象完成同一任务。
        owned.LeaseToken = null;
        owned.LeaseUntil = 0;
        Assert.True(await store.TryUpdateTransferAsync(owned, owned.Version, "successor"));
        client.BeforeUploadCheckpoint = null;
        await env.RestartAsync();
        var wait = completion.NextAttemptAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (wait > 0) await Task.Delay(TimeSpan.FromMilliseconds(wait + 10), deadline.Token);
        await env.Get<FileOperationProcessor>().RunBatchAsync(deadline.Token);
        Assert.Equal(FileState.Ready, (await env.Get<IFileManagementStore>().FindAsync(upload.FileId))!.File.State);
        operations = await OperationsFor(env, upload.FileId);
        Assert.Equal(FileOperationState.Succeeded, Assert.Single(operations, x => x.Kind == FileOperationKind.Complete).State);
        Assert.DoesNotContain(operations, x => x.Kind == FileOperationKind.Abort);
    }

    private static async Task<UploadSessionView> PrepareHeartbeatUpload(FileTestEnvironment env, RemoteDriveConnection connection, CancellationToken ct)
    {
        var service = env.Get<FileManagementService>();
        var bytes = new byte[] { 1, 2, 3, 4 };
        var upload = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "heartbeat.bin",
            Length = bytes.Length, StorageTargetId = connection.Id.ToString("N") }, ct);
        await env.Get<FileOperationProcessor>().RunBatchAsync(ct);
        await service.CreatePartRequestAsync(upload.Id, new() { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(bytes)) }, ct);
        using var input = new MemoryStream(bytes);
        await service.UploadPartAsync(upload.Id, 1, input, ct);
        await service.CompleteUploadAsync(upload.Id, ct);
        return upload;
    }

    private static async Task<FileOperation[]> OperationsFor(FileTestEnvironment env, Guid fileId) =>
        (await env.Get<IFileManagementStore>().QueryOperationsAsync(new() { ScopeKey = StorageScope.ForTenant(env.Identity.Tenant!).Key, Take = 100 }))
        .Where(x => x.FileId == fileId).ToArray();
}
