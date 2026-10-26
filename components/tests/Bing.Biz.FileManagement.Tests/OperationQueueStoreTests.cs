using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class OperationQueueStoreTests
{
    private const long Now = 1000;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListDueOperationsAsync_FiltersQueuesBeforeOrderingAndLimit(bool freeSql)
    {
        await using var fixture = await FileManagementStoreTestEnvironment.CreateAsync(freeSql);
        var entry = await fixture.CreateEntryAsync("queue-filtering");
        var operations = new List<FileOperation>();

        for (var i = 1; i <= 25; i++)
            operations.Add(Operation(entry.File.Id, FileOperationKind.Complete, FileOperationState.Pending, i));
        operations.Add(Operation(entry.File.Id, FileOperationKind.Migrate, FileOperationState.Pending, 26));
        operations.Add(Operation(entry.File.Id, FileOperationKind.Migrate, FileOperationState.Running, 5, leaseUntil: Now - 1));

        operations.Add(Operation(entry.File.Id, FileOperationKind.Initialize, FileOperationState.Pending, 100));
        operations.Add(Operation(entry.File.Id, FileOperationKind.Abort, FileOperationState.Pending, 101));
        operations.Add(Operation(entry.File.Id, FileOperationKind.Delete, FileOperationState.Pending, 102));
        operations.Add(Operation(entry.File.Id, FileOperationKind.DeleteReplica, FileOperationState.Pending, 103));

        operations.Add(Operation(entry.File.Id, FileOperationKind.Complete, FileOperationState.Pending, Now + 1));
        operations.Add(Operation(entry.File.Id, FileOperationKind.Initialize, FileOperationState.Running, 1, leaseUntil: Now + 1));
        operations.Add(Operation(entry.File.Id, FileOperationKind.Migrate, FileOperationState.Failed, 1));
        operations.Add(Operation(entry.File.Id, FileOperationKind.Delete, FileOperationState.Succeeded, 1));
        await fixture.SeedOperationsAsync(operations);

        var allLong = await fixture.Store.ListDueOperationsAsync(Now, 100, FileOperationQueue.Long);
        Assert.Equal(new[] { FileOperationKind.Complete, FileOperationKind.Migrate },
            allLong.Select(operation => operation.Kind).Distinct().OrderBy(kind => kind));
        Assert.Contains(allLong, operation => operation.State == FileOperationState.Running && operation.LeaseUntil < Now);

        var expectedLong = operations
            .Where(operation => operation.Kind is FileOperationKind.Complete or FileOperationKind.Migrate)
            .Where(operation => operation.State == FileOperationState.Pending && operation.NextAttemptAt <= Now ||
                                operation.State == FileOperationState.Running && operation.LeaseUntil <= Now)
            .OrderBy(operation => operation.NextAttemptAt).ThenBy(operation => operation.Id)
            .Take(20)
            .Select(operation => operation.Id)
            .ToArray();
        var longQueue = await fixture.Store.ListDueOperationsAsync(Now, 20, FileOperationQueue.Long);
        Assert.Equal(expectedLong, longQueue.Select(operation => operation.Id));
        Assert.Equal(20, longQueue.Count);
        Assert.Contains(longQueue, operation => operation.State == FileOperationState.Running && operation.LeaseUntil < Now);
        Assert.All(longQueue, operation => Assert.Contains(operation.Kind, new[] { FileOperationKind.Complete, FileOperationKind.Migrate }));

        var allShort = await fixture.Store.ListDueOperationsAsync(Now, 100, FileOperationQueue.Short);
        Assert.Equal(new[]
            {
                FileOperationKind.Initialize, FileOperationKind.Abort,
                FileOperationKind.Delete, FileOperationKind.DeleteReplica
            }.OrderBy(kind => kind),
            allShort.Select(operation => operation.Kind).Distinct().OrderBy(kind => kind));

        var expectedShort = operations
            .Where(operation => operation.Kind is not (FileOperationKind.Complete or FileOperationKind.Migrate))
            .Where(operation => operation.State == FileOperationState.Pending && operation.NextAttemptAt <= Now ||
                                operation.State == FileOperationState.Running && operation.LeaseUntil <= Now)
            .OrderBy(operation => operation.NextAttemptAt).ThenBy(operation => operation.Id)
            .Take(2)
            .Select(operation => operation.Id)
            .ToArray();
        var shortQueue = await fixture.Store.ListDueOperationsAsync(Now, 2, FileOperationQueue.Short);
        Assert.Equal(expectedShort, shortQueue.Select(operation => operation.Id));
        Assert.Equal(2, shortQueue.Count);
        Assert.All(shortQueue, operation => Assert.Contains(operation.Kind,
            new[] { FileOperationKind.Initialize, FileOperationKind.Abort, FileOperationKind.Delete, FileOperationKind.DeleteReplica }));

        var allDue = operations
            .Where(operation => operation.State == FileOperationState.Pending && operation.NextAttemptAt <= Now ||
                                operation.State == FileOperationState.Running && operation.LeaseUntil <= Now)
            .OrderBy(operation => operation.NextAttemptAt).ThenBy(operation => operation.Id)
            .Take(20)
            .Select(operation => operation.Id)
            .ToArray();
        var legacy = await fixture.Store.ListDueOperationsAsync(Now, 20);
        Assert.Equal(allDue, legacy.Select(operation => operation.Id));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fixture.Store.ListDueOperationsAsync(Now, 20, (FileOperationQueue)999));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fixture.Store.ListDueOperationsAsync(Now, -1, FileOperationQueue.Short));
        Assert.Empty(await fixture.Store.ListDueOperationsAsync(Now, 0, FileOperationQueue.Long));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Store.ListDueOperationsAsync(Now, 1, FileOperationQueue.Short, cancellation.Token));
    }

    private static FileOperation Operation(Guid fileId, FileOperationKind kind, FileOperationState state,
        long nextAttemptAt, long leaseUntil = 0) => new()
    {
        Id = Guid.NewGuid(),
        FileId = fileId,
        Kind = kind,
        State = state,
        Payload = "{}",
        Attempts = 0,
        NextAttemptAt = nextAttemptAt,
        LeaseUntil = leaseUntil,
        Version = 1
    };
}
