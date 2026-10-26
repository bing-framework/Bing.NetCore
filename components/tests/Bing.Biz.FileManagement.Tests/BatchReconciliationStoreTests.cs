using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.Biz.FileManagement.FreeSql;
using FreeSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class BatchReconciliationStoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReconcileQuery_FiltersBeforeLimitAndReturnsOnlyMatchingTerminalOperationsInStableOrder(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();

        var irrelevantBatch = Batch(1, 1, FileMigrationBatchState.Running);
        var noWorkPausedBatch = Batch(2, 2, FileMigrationBatchState.Paused);
        var completedBatch = Batch(3, 3, FileMigrationBatchState.Completed);
        var runningBatch = Batch(4, 100, FileMigrationBatchState.Running);
        var pausedBatch = Batch(5, 100, FileMigrationBatchState.Paused);
        var batches = new[] { irrelevantBatch, noWorkPausedBatch, completedBatch, runningBatch, pausedBatch };
        var items = new List<FileMigrationBatchItem>();
        var operations = new List<FileOperation>();

        // 确保更早的未完成条目超过整页时，仍能查到后续终态结果。
        for (var index = 0; index < 1005; index++)
        {
            var fileId = Id(10_000 + index);
            var operation = Operation(20_000 + index, fileId, FileOperationState.Running);
            operations.Add(operation);
            items.Add(Item(irrelevantBatch.Id, fileId, operation.Id));
        }

        Add(runningBatch, 10, FileOperationState.Succeeded);
        Add(runningBatch, 11, FileOperationState.Failed);
        Add(pausedBatch, 20, FileOperationState.Succeeded);
        Add(pausedBatch, 21, FileOperationState.Failed);

        Add(runningBatch, 30, FileOperationState.Pending);
        Add(runningBatch, 31, FileOperationState.Running);
        Add(runningBatch, 32, FileOperationState.Succeeded, FileMigrationItemState.Pending);
        Add(runningBatch, 33, FileOperationState.Failed, FileMigrationItemState.Failed);

        var mismatchedFile = Id(40);
        var mismatchedOperation = Operation(30_000, mismatchedFile, FileOperationState.Succeeded);
        operations.Add(mismatchedOperation);
        items.Add(Item(runningBatch.Id, Id(39), mismatchedOperation.Id));
        items.Add(Item(runningBatch.Id, Id(41), Id(30_001))); // 缺少关联操作。

        var completedOperation = Operation(30_002, Id(42), FileOperationState.Succeeded);
        operations.Add(completedOperation);
        items.Add(Item(completedBatch.Id, Id(42), completedOperation.Id));

        await SeedAsync(env, batches, items, operations);
        var store = env.Get<IFileManagementStore>();

        Assert.Empty(await store.ListBatchItemsToReconcileAsync(0));
        var firstPage = await store.ListBatchItemsToReconcileAsync(1);
        Assert.Equal(Id(10), Assert.Single(firstPage).FileId);
        var all = await store.ListBatchItemsToReconcileAsync(1000);
        Assert.Equal(new[] { Id(10), Id(11), Id(20), Id(21) }, all.Select(item => item.FileId));

        // 修改返回快照不得影响数据库中的查询结果。
        firstPage[0].State = FileMigrationItemState.Failed;
        Assert.Equal(FileMigrationItemState.Queued,
            Assert.Single(await store.ListBatchItemsToReconcileAsync(1)).State);

        Assert.Equal(new[] { Id(1), Id(4) },
            (await store.ListActiveBatchesAsync(100)).Select(batch => batch.Id));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListBatchItemsToReconcileAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListBatchItemsToReconcileAsync(1001));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.ListBatchItemsToReconcileAsync(1, new CancellationToken(canceled: true)));

        void Add(FileMigrationBatch batch, int fileId, FileOperationState operationState,
            FileMigrationItemState itemState = FileMigrationItemState.Queued)
        {
            var file = Id(fileId);
            var operation = Operation(40_000 + operations.Count, file, operationState);
            operations.Add(operation);
            items.Add(Item(batch.Id, file, operation.Id, itemState));
        }
    }

    private static async Task SeedAsync(FileTestEnvironment env, IReadOnlyCollection<FileMigrationBatch> batches,
        IReadOnlyCollection<FileMigrationBatchItem> items, IReadOnlyCollection<FileOperation> operations)
    {
        var files = operations.Select(operation => TestFile(operation.FileId)).DistinctBy(file => file.Id).ToArray();
        if (env.Provider.GetService<IFreeSql>() is { } orm)
        {
            await orm.Insert<FileRecord>(files).ExecuteAffrowsAsync();
            await orm.Insert<FileMigrationBatch>(batches.ToArray()).ExecuteAffrowsAsync();
            foreach (var chunk in operations.Chunk(100))
                await orm.Insert<FileOperation>(chunk).ExecuteAffrowsAsync();
            foreach (var chunk in items.Chunk(100))
                await orm.Insert<FileMigrationBatchItem>(chunk).ExecuteAffrowsAsync();
            return;
        }

        await using var db = await env.Provider.GetRequiredService<IDbContextFactory<FileManagementDbContext>>()
            .CreateDbContextAsync();
        db.AddRange(files);
        await db.SaveChangesAsync();
        db.AddRange(batches);
        db.AddRange(operations);
        db.AddRange(items);
        await db.SaveChangesAsync();
    }

    private static FileRecord TestFile(Guid id) => new()
    {
        Id = id,
        RequestKey = "reconcile-file-" + id.ToString("N"),
        ScopeKey = "tenant:tenant-a",
        TenantId = "tenant-a",
        StoreName = "local-v1",
        Container = "attachments",
        ObjectKey = id.ToString("N"),
        FileName = id.ToString("N") + ".bin",
        ContentType = "application/octet-stream",
        Visibility = FileVisibility.Private,
        State = FileState.Ready,
        CreatorId = "test-user",
        CreatedAt = 1,
        Version = 1,
        ContentPolicyJson = "{}"
    };

    private static FileMigrationBatch Batch(int id, long createdAt, FileMigrationBatchState state) => new()
    {
        Id = Id(id),
        RequestKey = "reconcile-batch-" + id,
        ScopeKey = "tenant:tenant-a",
        ActorId = "test-operator",
        SourceStore = "local-v1",
        TargetStore = "local-v2",
        State = state,
        CreatedAt = createdAt,
        Version = 1
    };

    private static FileOperation Operation(int id, Guid fileId, FileOperationState state) => new()
    {
        Id = Id(id),
        FileId = fileId,
        Kind = FileOperationKind.Migrate,
        State = state,
        Version = 1,
        CreatedAt = id
    };

    private static FileMigrationBatchItem Item(Guid batchId, Guid fileId, Guid? operationId,
        FileMigrationItemState state = FileMigrationItemState.Queued) => new()
    {
        BatchId = batchId,
        FileId = fileId,
        SourceVersion = 1,
        Length = 1,
        OperationId = operationId,
        State = state,
        Version = 1
    };

    private static Guid Id(int value) => Guid.Parse("00000000-0000-0000-0000-" + value.ToString("D12"));
}
