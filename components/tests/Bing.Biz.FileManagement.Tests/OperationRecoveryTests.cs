using System.Data.Common;
using System.Reflection;
using Bing.FileStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class OperationRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_LosingCommit_DoesNotDeleteWinnersContent(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.UploadAsync(new byte[] { 7, 8, 9 });
        var inner = env.Get<IFileManagementStore>();
        var proxy = StoreFaultProxy.Wrap(inner);
        proxy.OnUpdate = async (entry, commit) =>
        {
            var result = await commit();
            // 对真实 SQLite 提交成功后报告 CAS 竞争，复现另一执行者已切换位置的窗口。
            return entry.File.StoreName == "local-v2" ? false : result;
        };
        var migration = new FileMigrationService((IFileManagementStore)(object)proxy, env.Get<IFileStorageFactory>(), env.Get<FileManagementOptions>());
        await migration.QueueMigrationAsync(session.FileId, "local-v2");
        var processor = new FileOperationProcessor((IFileManagementStore)(object)proxy, env.Get<IFileStorageFactory>(), migration,
            env.Get<FileManagementOptions>(), NullLogger<FileOperationProcessor>.Instance);
        await processor.RunBatchAsync();
        var current = (await inner.FindAsync(session.FileId))!;
        Assert.Equal("local-v2", current.File.StoreName);
        var storage = env.Get<IFileStorageFactory>().GetStorage(current.File.StoreName, current.File.Scope());
        await storage.ReadAsync(current.File.Address(), async (stream, ct) =>
        {
            using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes, ct);
            Assert.Equal(new byte[] { 7, 8, 9 }, bytes.ToArray());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completion_DatabaseFailureAfterObjectCommit_RemainsRecoverable(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var service = env.Get<FileManagementService>();
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "empty" });
        await env.RunAsync(); await service.CompleteUploadAsync(session.Id);
        var inner = env.Get<IFileManagementStore>();
        var proxy = StoreFaultProxy.Wrap(inner);
        var injected = false;
        proxy.OnUpdate = (entry, commit) =>
        {
            if (!injected && entry.File.State == FileState.Ready) { injected = true; throw new TestDbException(); }
            return commit();
        };
        var processor = new FileOperationProcessor((IFileManagementStore)(object)proxy, env.Get<IFileStorageFactory>(), env.Get<FileMigrationService>(),
            env.Get<FileManagementOptions>(), NullLogger<FileOperationProcessor>.Instance);
        await processor.RunBatchAsync();
        Assert.True(injected);
        Assert.Equal(FileState.Completing, (await inner.FindAsync(session.FileId))!.File.State);
        var retry = Assert.Single(await inner.ListDueOperationsAsync(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds(), 20));
        Assert.Equal(FileOperationState.Pending, retry.State);
        // 只推进此持久任务的重试时间，不等待全局轮询。
        Assert.True(await inner.TryClaimAsync(retry.Id, retry.Version, "test-advance", long.MaxValue / 2, long.MaxValue / 2 + 1000));
        Assert.True(await inner.FinishOperationAsync(retry.Id, "test-advance", FileOperationState.Pending, 0, null));
        await env.RunAsync();
        Assert.Equal(FileState.Ready, (await service.GetSessionAsync(session.Id)).State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartRequest_RejectsNullChecksum(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var service = env.Get<FileManagementService>();
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "part", Length = 1 });
        await env.RunAsync();
        Assert.Equal("invalid", (await Assert.ThrowsAsync<FileManagementException>(
            () => service.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = null! }))).Code);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_DatabaseFailureAfterCopy_RetriesWithoutChangingSource(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.UploadAsync(new byte[] { 10, 11 });
        var inner = env.Get<IFileManagementStore>();
        await env.Get<FileMigrationService>().QueueMigrationAsync(session.FileId, "local-v2");
        var proxy = StoreFaultProxy.Wrap(inner);
        proxy.OnUpdate = (entry, commit) => entry.File.StoreName == "local-v2" ? throw new TestDbException() : commit();
        var migration = new FileMigrationService((IFileManagementStore)(object)proxy, env.Get<IFileStorageFactory>(), env.Get<FileManagementOptions>());
        var processor = new FileOperationProcessor((IFileManagementStore)(object)proxy, env.Get<IFileStorageFactory>(), migration,
            env.Get<FileManagementOptions>(), NullLogger<FileOperationProcessor>.Instance);
        await processor.RunBatchAsync();
        var source = (await inner.FindAsync(session.FileId))!;
        Assert.Equal("local-v1", source.File.StoreName); Assert.Equal(FileState.Ready, source.File.State);
        Assert.NotNull(await env.Get<IFileStorageFactory>().GetStorage(source.File.StoreName, source.File.Scope()).GetPropertiesAsync(source.File.Address()));
        var retry = Assert.Single(await inner.ListDueOperationsAsync(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds(), 20));
        Assert.True(await inner.TryClaimAsync(retry.Id, retry.Version, "advance", long.MaxValue / 2, long.MaxValue / 2 + 1000));
        Assert.True(await inner.FinishOperationAsync(retry.Id, "advance", FileOperationState.Pending, 0, null));
        await env.RunAsync();
        Assert.Equal("local-v2", (await inner.FindAsync(session.FileId))!.File.StoreName);
    }
    private sealed class TestDbException : DbException { }
}

/// <summary>仅注入提交边界的故障，所有真实读写仍委托给 SQLite 仓储。</summary>
public class StoreFaultProxy : DispatchProxy
{
    private IFileManagementStore _inner = null!;
    public Func<FileEntry, Func<Task<bool>>, Task<bool>>? OnUpdate { get; set; }
    public static StoreFaultProxy Wrap(IFileManagementStore inner)
    {
        var instance = Create<IFileManagementStore, StoreFaultProxy>();
        var proxy = (StoreFaultProxy)instance; proxy._inner = inner; return proxy;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == nameof(IFileManagementStore.TryUpdateAsync) && OnUpdate != null)
            return OnUpdate((FileEntry)args![0]!, () => (Task<bool>)targetMethod.Invoke(_inner, args)!);
        return targetMethod.Invoke(_inner, args);
    }
}
