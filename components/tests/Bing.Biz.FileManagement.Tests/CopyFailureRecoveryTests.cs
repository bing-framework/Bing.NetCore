using System.Text;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>在真实 SQLite 和本地对象上验证失败复制的补偿，不以“已排队”代替“已删除”。</summary>
public sealed class CopyFailureRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedCopy_QueuesExecutableDeletion_AndKeepsSourceAndFailureHistory(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        var fixture = await CreateCopyAsync(env);
        var store = env.Get<IFileManagementStore>();
        var targets = env.Get<IFileStorageTargetStore>();
        await FailAsync(store, fixture.Operation);

        var failed = (await store.FindAsync(fixture.Target.File.Id))!;
        Assert.Equal(FileState.DeletePending, failed.File.State);
        var deletion = Assert.Single(await store.QueryOperationsAsync(new()
        { ScopeKey = failed.File.ScopeKey, FileId = failed.File.Id, Kind = FileOperationKind.Delete }));
        // 失效的执行者不能重复终结任务或再插入清理任务。
        Assert.False(await store.FinishOperationAsync(fixture.Operation.Id, "copy-worker", FileOperationState.Failed, 0, "late_error"));
        Assert.Single(await store.QueryOperationsAsync(new()
        { ScopeKey = failed.File.ScopeKey, FileId = failed.File.Id, Kind = FileOperationKind.Delete }));

        await env.RunAsync();
        Assert.Equal(FileOperationState.Succeeded, (await store.FindOperationAsync(deletion.Id, failed.File.ScopeKey))!.State);
        Assert.Equal(FileState.Deleted, (await store.FindAsync(failed.File.Id))!.File.State);
        var backend = env.Get<IFileStorageFactory>().GetStorage(failed.File.StoreName, failed.File.Scope());
        Assert.Null(await backend.GetPropertiesAsync(new(failed.File.Container, failed.File.ObjectKey)));
        var request = (await targets.FindCopyAsync(fixture.Request.Id))!;
        Assert.Equal(FileBindingCopyState.Failed, request.State);
        Assert.Equal("copy_error", request.ErrorCode);
        var source = (await store.FindAsync(fixture.Source.File.Id))!;
        Assert.Equal(FileState.Ready, source.File.State);
        Assert.Equal(fixture.Source.File.Version, source.File.Version);
        await AssertContentAsync(env, source.File, fixture.Bytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task LateCopyFailure_DoesNotDeletePublishedOrBoundCopy(bool freeSql, bool bind)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        var fixture = await CreateCopyAsync(env);
        var store = env.Get<IFileManagementStore>();
        var targets = env.Get<IFileStorageTargetStore>();
        fixture.Request.State = FileBindingCopyState.Copied;
        fixture.Target.File.State = FileState.Ready;
        Assert.True(await targets.UpdateCopyAsync(fixture.Request, 1, fixture.Target, 1));
        if (bind)
        {
            var request = (await targets.FindCopyAsync(fixture.Request.Id))!;
            var target = (await store.FindAsync(fixture.Target.File.Id))!;
            request.State = FileBindingCopyState.Bound;
            target.Bindings.Add(new() { Id = Guid.NewGuid(), FileId = target.File.Id, BusinessType = "orders",
                BusinessId = "order-1", CreatorId = "user-a", CreatedAt = 1 });
            Assert.True(await targets.UpdateCopyAsync(request, request.Version, target, target.File.Version));
        }
        await FailAsync(store, fixture.Operation);
        await env.RunAsync();
        var current = (await store.FindAsync(fixture.Target.File.Id))!;
        Assert.Equal(FileState.Ready, current.File.State);
        Assert.Equal(bind ? 1 : 0, current.Bindings.Count);
        Assert.Equal(bind ? FileBindingCopyState.Bound : FileBindingCopyState.Copied,
            (await targets.FindCopyAsync(fixture.Request.Id))!.State);
        Assert.Empty(await store.QueryOperationsAsync(new()
        { ScopeKey = current.File.ScopeKey, FileId = current.File.Id, Kind = FileOperationKind.Delete }));
        await AssertContentAsync(env, current.File, fixture.Bytes);
    }

    [Theory]
    [InlineData(FileOperationKind.Initialize, "new-upload")]
    [InlineData(FileOperationKind.Complete, "new-upload")]
    [InlineData(FileOperationKind.Inspect, "new-upload")]
    [InlineData(FileOperationKind.Abort, "retry-if-current")]
    [InlineData(FileOperationKind.Delete, "retry-if-current")]
    [InlineData(FileOperationKind.Migrate, "retry-if-current")]
    [InlineData(FileOperationKind.DeleteReplica, "retry-if-current")]
    [InlineData(FileOperationKind.CopyForBinding, "none")]
    [InlineData((FileOperationKind)999, "none")]
    public void RecoveryAction_OnlyAdvertisesSupportedOperations(FileOperationKind kind, string expected)
    {
        var operation = new FileOperation { Kind = kind, State = FileOperationState.Failed };
        Assert.Equal(expected, FileOperationAdministration.View(operation).RecoveryAction);
        foreach (var state in new[] { FileOperationState.Pending, FileOperationState.Running, FileOperationState.Succeeded })
        {
            operation.State = state;
            Assert.Equal("none", FileOperationAdministration.View(operation).RecoveryAction);
        }
        operation.State = FileOperationState.Failed;
        operation.RecoveryOperationId = Guid.NewGuid();
        Assert.Equal("none", FileOperationAdministration.View(operation).RecoveryAction);
    }

    private static async Task FailAsync(IFileManagementStore store, FileOperation operation)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.True(await store.TryClaimAsync(operation.Id, 1, "copy-worker", now, now + 60_000));
        Assert.True(await store.FinishOperationAsync(operation.Id, "copy-worker", FileOperationState.Failed, 0, "copy_error"));
    }

    private static async Task<(FileEntry Source, FileEntry Target, FileBindingCopyRequest Request, FileOperation Operation, byte[] Bytes)>
        CreateCopyAsync(FileTestEnvironment env)
    {
        var store = env.Get<IFileManagementStore>();
        var bytes = Encoding.UTF8.GetBytes("复制失败必须保留个人原件");
        var session = await env.UploadAsync(bytes);
        var source = (await store.FindAsync(session.FileId))!;
        source.File.Ownership = RemoteDriveOwnership.Personal;
        Assert.True(await store.TryUpdateAsync(source, source.File.Version));
        source = (await store.FindAsync(source.File.Id))!;
        var id = Guid.NewGuid();
        var target = new FileEntry { File = new FileRecord
        {
            Id = id, RequestKey = id.ToString("N"), ScopeKey = source.File.ScopeKey, ScopeKind = source.File.ScopeKind,
            TenantId = source.File.TenantId, StoreName = "local-v2", Container = "attachments", ObjectKey = id.ToString("N"),
            CreatorId = "user-a", FileName = "副本.txt", Length = bytes.Length, ContentType = "text/plain",
            State = FileState.Pending, Ownership = RemoteDriveOwnership.Shared, CreatedAt = 1, Version = 1
        } };
        var request = new FileBindingCopyRequest
        {
            Id = Guid.NewGuid(), RequestKey = "copy-" + id.ToString("N"), ScopeKey = source.File.ScopeKey, OwnerId = "user-a",
            SourceFileId = source.File.Id, SourceVersion = source.File.Version, CopyFileId = id,
            BusinessType = "orders", BusinessId = "order-1", CreatedAt = 1
        };
        var operation = new FileOperation { Id = Guid.NewGuid(), FileId = id, Kind = FileOperationKind.CopyForBinding,
            Payload = request.Id.ToString("N"), State = FileOperationState.Pending, CreatedAt = 1, Version = 1 };
        Assert.True(await env.Get<IFileStorageTargetStore>().CreateCopyAsync(request, target, operation));
        using var content = new MemoryStream(bytes);
        await env.Get<IFileStorageFactory>().GetStorage("local-v2", target.File.Scope()).SaveAsync(
            new(new(target.File.Container, target.File.ObjectKey), bytes.Length, FileWriteMode.Overwrite), content);
        return (source, target, request, operation, bytes);
    }

    private static async Task AssertContentAsync(FileTestEnvironment env, FileRecord file, byte[] expected)
    {
        await env.Get<IFileStorageFactory>().GetStorage(file.StoreName, file.Scope()).ReadAsync(
            new(file.Container, file.ObjectKey), async (stream, ct) =>
            {
                using var output = new MemoryStream();
                await stream.CopyToAsync(output, ct);
                Assert.Equal(expected, output.ToArray());
            });
    }
}
