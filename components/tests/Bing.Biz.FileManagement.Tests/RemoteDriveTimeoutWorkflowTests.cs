using System.Security.Cryptography;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed partial class RemoteDriveWorkflowTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DownloadTimeout_ReleasesLongSlotAndResumesCheckpointAfterRestart(bool freeSql)
    {
        using var client = new DriveFixture();
        await using var env = Setup(freeSql, client, transportTimeouts: new()
        { ResponseHeadersTimeout = TimeSpan.FromMilliseconds(150), IdleTimeout = TimeSpan.FromMilliseconds(300) });
        env.ConfigureManagement = o => { o.LongOperationConcurrency = 1; o.OperationPollInterval = TimeSpan.FromMilliseconds(100); };
        await env.InitializeAsync();
        var connection = await Connect(env);
        var service = env.Get<FileManagementService>();
        var remote = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "remote.bin", Length = 1, StorageTargetId = connection.Id.ToString("N") });
        var local = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "empty.bin", Length = 0, StorageTargetId = "local-v1" });
        await env.RunAsync();
        var bytes = new byte[] { 42 };
        await service.CreatePartRequestAsync(remote.Id, new() { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(bytes)) });
        await service.UploadPartAsync(remote.Id, 1, new MemoryStream(bytes));
        await service.CompleteUploadAsync(remote.Id);
        await service.CompleteUploadAsync(local.Id);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.BeforeDownloadResponse = async ct => { entered.TrySetResult(true); await Task.Delay(Timeout.InfiniteTimeSpan, ct); };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var scheduler = env.Get<FileOperationScheduler>().RunAsync(stop.Token);
        var store = env.Get<IFileManagementStore>();
        try
        {
            await entered.Task.WaitAsync(deadline.Token);
            while ((await store.FindAsync(local.FileId, deadline.Token))!.File.State != FileState.Ready)
                await Task.Delay(20, deadline.Token);
        }
        finally { stop.Cancel(); await scheduler; }
        Assert.False(deadline.IsCancellationRequested);
        var operation = Assert.Single(await OperationsFor(env, remote.FileId), x => x.Kind == FileOperationKind.Complete);
        Assert.Equal(FileOperationState.Pending, operation.State);
        Assert.Equal("storage:OutcomeUnknown", operation.LastError);
        Assert.Equal(FileState.Completing, (await store.FindAsync(remote.FileId))!.File.State);
        var session = (await store.FindAsync(remote.FileId))!.Session!;
        var transfer = (await env.Get<IRemoteDriveStateStore>().FindTransferAsync(Guid.ParseExact(session.BackendUploadId!, "N")))!;
        var checkpoint = System.Text.Json.JsonSerializer.Deserialize<RemoteDriveCheckpoint>(transfer.CheckpointJson)!;
        Assert.Single(checkpoint.Parts); Assert.NotNull(checkpoint.FileId);
        Assert.Null(transfer.LeaseToken);
        Assert.NotNull(await env.Get<IFileStorageFactory>().GetStorage(transfer.StageStoreName, connection.Scope())
            .GetPropertiesAsync(new(transfer.StageContainer, transfer.StageObjectKey)));

        client.BeforeDownloadResponse = null;
        await env.RestartAsync();
        var delay = operation.NextAttemptAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (delay > 0) await Task.Delay(TimeSpan.FromMilliseconds(delay + 20), deadline.Token);
        await env.Get<FileOperationProcessor>().RunBatchAsync(deadline.Token);
        var ready = (await env.Get<IFileManagementStore>().FindAsync(remote.FileId))!.File;
        Assert.Equal(FileState.Ready, ready.State);
        Assert.Equal(FileOperationState.Succeeded, Assert.Single(await OperationsFor(env, remote.FileId), x => x.Kind == FileOperationKind.Complete).State);
        using var output = new MemoryStream();
        await env.Get<IFileStorageFactory>().GetStorage(ready.StoreName, ready.Scope()).CopyToAsync(ready.Address(), output, deadline.Token);
        Assert.Equal(bytes, output.ToArray());
    }
}
