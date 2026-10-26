using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Bing.FileStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>使用真实仓储和对象 IO 验证审查发现的故障窗口，故障只注入到提交或调度边界。</summary>
public sealed class ReviewRecoveryTests
{
    [Theory]
    [InlineData(false, "ready")] [InlineData(true, "ready")]
    [InlineData(false, "failure")] [InlineData(true, "failure")]
    [InlineData(false, "retry-limit")] [InlineData(true, "retry-limit")]
    public async Task LostLease_CannotCommitReadyOrFailNewOwnersFile(bool freeSql, string outcome)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var service = env.Get<FileManagementService>();
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "lease" });
        await env.RunAsync(); await service.CompleteUploadAsync(session.Id);
        var store = env.Get<IFileManagementStore>();
        var proxy = ReviewStoreProxy.Wrap(store);
        env.Get<FileManagementOptions>().MaxOperationAttempts = 1;
        Guid operationId = default;
        proxy.OnUpdate = async (entry, commit) =>
        {
            if (entry.File.State != FileState.Ready) return await commit();
            var now = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
            var operation = Assert.Single(await store.ListDueOperationsAsync(now, 20)); operationId = operation.Id;
            Assert.True(await store.TryClaimAsync(operation.Id, operation.Version, "new-owner", now, now + 120000));
            if (outcome != "ready") throw new FileStorageException(outcome == "retry-limit" ? FileStorageError.Unavailable : FileStorageError.InvalidChecksum, "late IO error");
            return await commit();
        };
        await Processor(env, proxy).RunBatchAsync();
        var current = (await store.FindAsync(session.FileId))!;
        Assert.Equal(FileState.Completing, current.File.State);
        var pending = await store.ListDueOperationsAsync(DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds(), 20);
        Assert.DoesNotContain(pending, x => x.Kind == FileOperationKind.Abort);
        Assert.Equal("new-owner", Assert.Single(pending).LeaseToken);
        var backend = env.Get<IFileStorageFactory>().GetStorage(current.File.StoreName, current.File.Scope());
        Assert.Null(await backend.GetPropertiesAsync(current.File.Address()));
        var retired = Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env));
        Assert.Equal(FileTransferState.Cleaned, retired.State);
        Assert.Null(await backend.GetPropertiesAsync(new(retired.Container, retired.ObjectKey)));
        Assert.True(await store.FinishOperationAsync(operationId, "new-owner", FileOperationState.Pending, 0, null));
        await env.RunAsync(); Assert.Equal(FileState.Ready, (await service.GetSessionAsync(session.Id)).State);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task TerminalFailure_CommitFailureOrLostResponse_RemainsAtomic(bool freeSql, bool afterCommit)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var service = env.Get<FileManagementService>();
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "terminal" });
        await env.RunAsync(); await service.CompleteUploadAsync(session.Id);
        var store = env.Get<IFileManagementStore>();
        var proxy = ReviewStoreProxy.Wrap(store);
        proxy.OnUpdate = (entry, commit) => entry.File.State == FileState.Ready
            ? Task.FromException<bool>(new FileStorageException(FileStorageError.InvalidChecksum, "failure")) : commit();
        proxy.OnFinish = async (state, commit) =>
        {
            if (state != FileOperationState.Failed) return await commit();
            if (afterCommit) await commit();
            throw new ReviewDbException();
        };
        await Assert.ThrowsAsync<ReviewDbException>(() => Processor(env, proxy).RunBatchAsync());
        var entry = (await store.FindAsync(session.FileId))!;
        if (afterCommit)
        {
            Assert.Equal(FileState.Failed, entry.File.State); Assert.Equal(FileState.Failed, entry.Session!.State);
            Assert.Equal(FileOperationKind.Abort, Assert.Single(await store.ListDueOperationsAsync(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(), 20)).Kind);
            await env.RunAsync();
            Assert.Null(await env.Get<IFileStorageFactory>().GetStorage(entry.File.StoreName, entry.File.Scope()).GetPropertiesAsync(entry.File.Address()));
        }
        else
        {
            Assert.Equal(FileState.Completing, entry.File.State);
            var now = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
            var retry = Assert.Single(await store.ListDueOperationsAsync(now, 20));
            Assert.True(await store.TryClaimAsync(retry.Id, retry.Version, "recover", now, now + 120000));
            Assert.True(await store.FinishOperationAsync(retry.Id, "recover", FileOperationState.Pending, 0, null));
            await env.RunAsync(); Assert.Equal(FileState.Ready, (await service.GetSessionAsync(session.Id)).State);
        }
    }

    [Theory]
    [InlineData(false, "completing")] [InlineData(true, "completing")]
    [InlineData(false, "completed")] [InlineData(true, "completed")]
    public async Task LocalCommittedObject_ReconcilesManifestAndRemovesParts(bool freeSql, string state)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var service = env.Get<FileManagementService>(); var bytes = new byte[] { 1, 2, 3 };
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "recover", Length = bytes.Length });
        await env.RunAsync();
        await service.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(bytes)) });
        await service.UploadPartAsync(session.Id, 1, new MemoryStream(bytes));
        await service.CompleteUploadAsync(session.Id);
        var entry = (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!;
        var storage = env.Get<IFileStorageFactory>().GetStorage(entry.File.StoreName, entry.File.Scope());
        var multipart = (IMultipartFileStorage)storage;
        var manifestPath = Directory.GetFiles(env.Root, "manifest.json", SearchOption.AllDirectories).Single();
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath, Encoding.UTF8))!; manifest["State"] = state;
        var partPath = Directory.GetFiles(env.Root, "*.part", SearchOption.AllDirectories).Single();
        var partBytes = await File.ReadAllBytesAsync(partPath);
        await multipart.CompleteMultipartAsync(entry.Upload(), await multipart.ListPartsAsync(entry.Upload()));
        // 恢复 Commit 后退出时的磁盘状态，不更改产品代码或模拟文件 IO。
        await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString(), Encoding.UTF8); await File.WriteAllBytesAsync(partPath, partBytes);
        await env.RunAsync(); await service.CompleteUploadAsync(session.Id); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await service.GetSessionAsync(session.Id)).State);
        Assert.Empty(Directory.GetFiles(env.Root, "*.part", SearchOption.AllDirectories));
        Assert.Equal("completed", JsonNode.Parse(await File.ReadAllTextAsync(manifestPath, Encoding.UTF8))!["State"]!.GetValue<string>());
        using var output = new MemoryStream(); await storage.CopyToAsync(entry.File.Address(), output); Assert.Equal(bytes, output.ToArray());
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, false)]
    [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(false, false, true)] [InlineData(true, false, true)]
    [InlineData(false, true, true)] [InlineData(true, true, true)]
    public async Task TerminalFailure_InsideTransactionErrorOrRootConflict_RollsBackWholeCommit(bool freeSql, bool casConflict, bool initialize)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var service = env.Get<FileManagementService>(); var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "atomic" });
        if (!initialize) { await env.RunAsync(); await service.CompleteUploadAsync(session.Id); }
        var store = env.Get<IFileManagementStore>(); var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var operation = Assert.Single(await store.ListDueOperationsAsync(now, 20));
        Assert.True(await store.TryClaimAsync(operation.Id, operation.Version, "owner", now, now + 120000));
        // 真实 SQLite 在任务行已写入后阻止根状态更新：分别制造数据库错误与零行 CAS。
        await ReviewRows.SqlAsync(env, "CREATE TRIGGER review_failure BEFORE UPDATE OF State ON BingFileRecords WHEN NEW.State=7 BEGIN SELECT RAISE(" +
            (casConflict ? "IGNORE" : "ABORT,'injected file update failure'") + "); END");
        if (casConflict) Assert.False(await store.FinishOperationAsync(operation.Id, "owner", FileOperationState.Failed, 0, "injected"));
        else Assert.NotNull(await Record.ExceptionAsync(() => store.FinishOperationAsync(operation.Id, "owner", FileOperationState.Failed, 0, "injected")));
        var entry = (await store.FindAsync(session.FileId))!;
        Assert.Equal(initialize ? FileState.Pending : FileState.Completing, entry.File.State);
        var tasks = await ReviewRows.ReadAsync<FileOperation>(env); var failedTask = tasks.Single(x => x.Id == operation.Id);
        Assert.Equal(FileOperationState.Running, failedTask.State); Assert.Equal("owner", failedTask.LeaseToken);
        Assert.DoesNotContain(tasks, x => x.Kind == FileOperationKind.Abort);
        await ReviewRows.SqlAsync(env, "DROP TRIGGER review_failure");
        await env.RestartAsync(); store = env.Get<IFileManagementStore>();
        Assert.True(await store.FinishOperationAsync(operation.Id, "owner", FileOperationState.Failed, 0, "injected"));
        entry = (await store.FindAsync(session.FileId))!; Assert.Equal(FileState.Failed, entry.File.State); Assert.Equal(FileState.Failed, entry.Session!.State);
        Assert.Equal(FileOperationKind.Abort, Assert.Single(await store.ListDueOperationsAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 20)).Kind);
        await env.RunAsync(); Assert.Empty(await store.ListDueOperationsAsync(now + 3600000, 20));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExpiredLeaseWithoutTakeover_CannotCommitBusinessOrFailure(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.Get<FileManagementService>().CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "expired" });
        var store = env.Get<IFileManagementStore>(); var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var operation = Assert.Single(await store.ListDueOperationsAsync(now, 20));
        Assert.True(await store.TryClaimAsync(operation.Id, operation.Version, "expired-owner", now, now + 120000));
        await ReviewRows.SqlAsync(env, "UPDATE BingFileOperations SET LeaseUntil=0");
        var entry = (await store.FindAsync(session.FileId))!;
        entry.File.State = entry.Session!.State = FileState.Uploading;
        Assert.False(await store.TryUpdateAsync(entry, entry.File.Version, lease: new(operation.Id, "expired-owner")));
        Assert.False(await store.FinishOperationAsync(operation.Id, "expired-owner", FileOperationState.Failed, 0, "late"));
        Assert.Equal(FileState.Pending, (await store.FindAsync(session.FileId))!.File.State);
        Assert.DoesNotContain(await ReviewRows.ReadAsync<FileOperation>(env), x => x.Kind == FileOperationKind.Abort);
        await env.RunAsync(); Assert.Equal(FileState.Uploading, (await store.FindAsync(session.FileId))!.File.State);
    }

    private static FileOperationProcessor Processor(FileTestEnvironment env, ReviewStoreProxy proxy) =>
        new((IFileManagementStore)(object)proxy, env.Get<IFileStorageFactory>(), env.Get<FileMigrationService>(), env.Get<FileManagementOptions>(), NullLogger<FileOperationProcessor>.Instance);
}

public sealed class ReviewDbException : DbException { }
public class ReviewStoreProxy : DispatchProxy
{
    private IFileManagementStore _inner = null!;
    public Func<FileEntry, Func<Task<bool>>, Task<bool>>? OnUpdate { get; set; }
    public Func<Func<Task<bool>>, Task<bool>>? OnCreate { get; set; }
    public Func<Func<Task<FileTransferAttempt?>>, Task<FileTransferAttempt?>>? OnRetire { get; set; }
    public Func<FileOperationState, Func<Task<bool>>, Task<bool>>? OnFinish { get; set; }
    public static ReviewStoreProxy Wrap(IFileManagementStore inner)
    { var result = (ReviewStoreProxy)Create<IFileManagementStore, ReviewStoreProxy>(); result._inner = inner; return result; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == nameof(IFileManagementStore.TryUpdateAsync) && OnUpdate != null)
            return OnUpdate((FileEntry)args![0]!, () => (Task<bool>)method.Invoke(_inner, args)!);
        if (method.Name == nameof(IFileManagementStore.FinishOperationAsync) && OnFinish != null)
            return OnFinish((FileOperationState)args![2]!, () => (Task<bool>)method.Invoke(_inner, args)!);
        if (method.Name == nameof(IFileManagementStore.TryCreateAsync) && OnCreate != null)
            return OnCreate(() => (Task<bool>)method.Invoke(_inner, args)!);
        if (method.Name == nameof(IFileManagementStore.RetireTransferAsync) && OnRetire != null)
            return OnRetire(() => (Task<FileTransferAttempt?>)method.Invoke(_inner, args)!);
        return method.Invoke(_inner, args);
    }
}
