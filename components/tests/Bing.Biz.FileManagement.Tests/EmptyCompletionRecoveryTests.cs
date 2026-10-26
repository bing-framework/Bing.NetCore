using Bing.FileStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>在真实 SQLite 和对象存储上重放空文件完成、接管、删除及提交结果未知的窗口。</summary>
public sealed class EmptyCompletionRecoveryTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public Task EmptyCompletion_LateWriter_ProtectsReadyAndDeleted(bool freeSql, bool deleteWinner)
        => OverlapAsync(freeSql, deleteWinner, false);

    [MinioUploadTheory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public Task MinioEmptyCompletion_LateWriter_ProtectsReadyAndDeleted(bool freeSql, bool deleteWinner)
        => OverlapAsync(freeSql, deleteWinner, true);

    private static async Task OverlapAsync(bool freeSql, bool deleteWinner, bool minio)
    {
        await using var env = new FileTestEnvironment(freeSql, minio); await env.InitializeAsync();
        var session = await PrepareAsync(env);
        var store = env.Get<IFileManagementStore>(); var factory = env.Get<IFileStorageFactory>();
        var initial = (await store.FindAsync(session.FileId))!.File;
        FileRecord? winner = null;
        var writes = 0;
        var hooked = new ReviewStorageFactory(factory, initial.StoreName, async (request, commit) =>
        {
            Assert.Equal(1, Interlocked.Increment(ref writes));
            var attempt = Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env));
            Assert.Equal(FileTransferState.Active, attempt.State);
            Assert.Equal(request.Address.Key, attempt.ObjectKey);
            Assert.NotEqual(initial.ObjectKey, attempt.ObjectKey);
            // 用逻辑时间接管租约，真实 Worker 发布新的独立对象，随后再恢复旧 IO。
            await RequeueAsync(store);
            await env.RunAsync(); winner = (await store.FindAsync(session.FileId))!.File;
            Assert.Equal(FileState.Ready, winner.State);
            Assert.NotEqual(attempt.ObjectKey, winner.ObjectKey);
            if (deleteWinner)
            {
                await env.Get<FileManagementService>().DeleteAsync(session.FileId);
                await env.RunAsync();
                Assert.Null(await factory.GetStorage(winner.StoreName, winner.Scope()).GetPropertiesAsync(winner.Address()));
            }
            return await commit(); // 保留实际取消令牌，不通过忽略取消来伪造竞态。
        });
        await Processor(env, store, hooked).RunBatchAsync();
        await env.RestartAsync(); await env.RunAsync();
        store = env.Get<IFileManagementStore>(); factory = env.Get<IFileStorageFactory>();
        var current = (await store.FindAsync(session.FileId))!.File;
        Assert.Equal(deleteWinner ? FileState.Deleted : FileState.Ready, current.State);
        Assert.Equal(winner!.ObjectKey, current.ObjectKey);
        var attempts = await ReviewRows.ReadAsync<FileTransferAttempt>(env);
        Assert.Equal(2, attempts.Count);
        var published = Assert.Single(attempts, x => x.State == FileTransferState.Published);
        var retired = Assert.Single(attempts, x => x.State == FileTransferState.Cleaned);
        Assert.Equal(current.ObjectKey, published.ObjectKey);
        var backend = factory.GetStorage(current.StoreName, current.Scope());
        Assert.Null(await backend.GetPropertiesAsync(new(retired.Container, retired.ObjectKey)));
        Assert.Null(await backend.GetPropertiesAsync(initial.Address()));
        await AssertContentAndDeleteAsync(env, current, deleteWinner);
        var stats = await store.GetStatisticsAsync();
        Assert.Equal(0, stats.PendingCleanup);
        Assert.Empty(await store.ListDueOperationsAsync(DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds(), 20));
    }

    [Theory]
    [InlineData(false, "before")] [InlineData(true, "before")]
    [InlineData(false, "after")] [InlineData(true, "after")]
    [InlineData(false, "restart")] [InlineData(true, "restart")]
    public Task EmptyCompletion_DatabaseFaults_RecoverAfterRestart(bool freeSql, string fault)
        => DatabaseFaultAsync(freeSql, fault, false);

    [MinioUploadTheory]
    [InlineData(false, "before")] [InlineData(true, "before")]
    [InlineData(false, "after")] [InlineData(true, "after")]
    [InlineData(false, "restart")] [InlineData(true, "restart")]
    public Task MinioEmptyCompletion_DatabaseFaults_RecoverAfterRestart(bool freeSql, string fault)
        => DatabaseFaultAsync(freeSql, fault, true);

    private static async Task DatabaseFaultAsync(bool freeSql, string fault, bool minio)
    {
        await using var env = new FileTestEnvironment(freeSql, minio); await env.InitializeAsync();
        var session = await PrepareAsync(env);
        var store = env.Get<IFileManagementStore>(); var proxy = ReviewStoreProxy.Wrap(store);
        var factory = env.Get<IFileStorageFactory>();
        var initial = (await store.FindAsync(session.FileId))!.File;
        proxy.OnUpdate = async (entry, commit) =>
        {
            Assert.Equal(FileState.Ready, entry.File.State);
            var attempt = Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env));
            Assert.True(attempt.WriterCompleted);
            Assert.Equal(entry.File.ObjectKey, attempt.ObjectKey);
            Assert.Equal(0L, (await factory.GetStorage(entry.File.StoreName, entry.File.Scope()).GetPropertiesAsync(entry.File.Address()))!.Length);
            if (fault == "after") Assert.True(await commit());
            throw new ReviewDbException();
        };
        if (fault == "restart") proxy.OnRetire = _ => Task.FromException<FileTransferAttempt?>(new ReviewDbException());
        await Processor(env, (IFileManagementStore)(object)proxy, factory).RunBatchAsync();
        var first = Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env));
        var expectedState = fault == "after" ? FileTransferState.Published : fault == "restart" ? FileTransferState.Active : FileTransferState.Cleaned;
        Assert.Equal(expectedState, first.State);
        Assert.Equal(fault == "after" ? FileState.Ready : FileState.Completing, (await store.FindAsync(session.FileId))!.File.State);
        if (fault == "restart") await ReviewRows.SqlAsync(env, "UPDATE BingFileTransferAttempts SET LeaseUntil=0,NextCheckAt=0");
        await ReviewRows.SqlAsync(env, "UPDATE BingFileOperations SET NextAttemptAt=0");
        await env.RestartAsync(); await env.RunAsync();
        store = env.Get<IFileManagementStore>(); factory = env.Get<IFileStorageFactory>();
        var ready = (await store.FindAsync(session.FileId))!.File;
        Assert.Equal(FileState.Ready, ready.State);
        Assert.Equal(FileState.Ready, (await env.Get<FileManagementService>().GetSessionAsync(session.Id)).State);
        var attempts = await ReviewRows.ReadAsync<FileTransferAttempt>(env);
        Assert.Equal(fault == "after" ? 1 : 2, attempts.Count);
        Assert.Equal(ready.ObjectKey, Assert.Single(attempts, x => x.State == FileTransferState.Published).ObjectKey);
        var backend = factory.GetStorage(ready.StoreName, ready.Scope());
        foreach (var attempt in attempts.Where(x => x.State != FileTransferState.Published))
        {
            Assert.Equal(FileTransferState.Cleaned, attempt.State);
            Assert.Null(await backend.GetPropertiesAsync(new(attempt.Container, attempt.ObjectKey)));
        }
        Assert.Null(await backend.GetPropertiesAsync(initial.Address()));
        // 重复完成不能创建第二个正式对象，也不能回收已提交但响应丢失的胜者。
        await env.Get<FileManagementService>().CompleteUploadAsync(session.Id); await env.RunAsync();
        Assert.Equal(attempts.Count, (await ReviewRows.ReadAsync<FileTransferAttempt>(env)).Count);
        await AssertContentAndDeleteAsync(env, ready, false);
        Assert.Equal(0, (await store.GetStatisticsAsync()).PendingCleanup);
        Assert.Empty(await store.ListDueOperationsAsync(DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds(), 20));
    }

    private static async Task<UploadSessionView> PrepareAsync(FileTestEnvironment env)
    {
        var service = env.Get<FileManagementService>();
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "empty.txt", Length = 0 });
        await env.RunAsync(); await service.CompleteUploadAsync(session.Id);
        return session;
    }

    private static async Task RequeueAsync(IFileManagementStore store)
    {
        var now = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
        var operation = Assert.Single(await store.ListDueOperationsAsync(now, 20));
        Assert.True(await store.TryClaimAsync(operation.Id, operation.Version, "empty-takeover", now, now + 120000));
        Assert.True(await store.FinishOperationAsync(operation.Id, "empty-takeover", FileOperationState.Pending, 0, null));
    }

    private static async Task AssertContentAndDeleteAsync(FileTestEnvironment env, FileRecord file, bool alreadyDeleted)
    {
        var backend = env.Get<IFileStorageFactory>().GetStorage(file.StoreName, file.Scope());
        if (!alreadyDeleted)
        {
            Assert.Equal(0L, (await backend.GetPropertiesAsync(file.Address()))!.Length);
            using var content = new MemoryStream(); await backend.CopyToAsync(file.Address(), content);
            Assert.Empty(content.ToArray());
            await env.Get<FileManagementService>().DeleteAsync(file.Id); await env.RunAsync();
        }
        Assert.Equal(FileState.Deleted, (await env.Get<IFileManagementStore>().FindAsync(file.Id))!.File.State);
        Assert.Null(await backend.GetPropertiesAsync(file.Address()));
    }

    private static FileOperationProcessor Processor(FileTestEnvironment env, IFileManagementStore store, IFileStorageFactory factory)
        => new(store, factory, env.Get<FileMigrationService>(), env.Get<FileManagementOptions>(), NullLogger<FileOperationProcessor>.Instance);
}
