using System.Reflection;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.FileStorage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class TransferRecoveryTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public Task Migration_LateWriter_CannotOverwriteWinnerOrResurrectDeleted(bool freeSql, bool deleteWinner)
        => MigrationOverlapAsync(freeSql, deleteWinner, false);

    [MinioUploadTheory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public Task MinioMigration_LateWriter_CannotOverwriteWinnerOrResurrectDeleted(bool freeSql, bool deleteWinner)
        => MigrationOverlapAsync(freeSql, deleteWinner, true);

    private static async Task MigrationOverlapAsync(bool freeSql, bool deleteWinner, bool minio)
    {
        await using var env = new FileTestEnvironment(freeSql, minio, minioDefault: false); await env.InitializeAsync();
        var bytes = new byte[] { 9, 8, 7, 6 }; var session = await env.UploadAsync(bytes);
        var target = minio ? "minio-v1" : "local-v2";
        var store = env.Get<IFileManagementStore>(); var factory = env.Get<IFileStorageFactory>();
        var migration = env.Get<FileMigrationService>(); await migration.QueueMigrationAsync(session.FileId, target);
        FileRecord? winner = null; var writes = 0;
        var hooked = new ReviewStorageFactory(factory, target, async (request, commit) =>
        {
            Assert.Equal(1, Interlocked.Increment(ref writes));
            var registered = Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env));
            Assert.Equal(request.Address.Key, registered.ObjectKey); Assert.Equal(FileTransferState.Active, registered.State);
            // 旧执行者停在真实目标写入前；新执行者接管同一任务并通过正常 Worker 完成。
            var now = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
            var operation = Assert.Single(await store.ListDueOperationsAsync(now, 20));
            Assert.True(await store.TryClaimAsync(operation.Id, operation.Version, "takeover", now, now + 120000));
            Assert.True(await store.FinishOperationAsync(operation.Id, "takeover", FileOperationState.Pending, 0, null));
            await env.RunAsync(); winner = (await store.FindAsync(session.FileId))!.File;
            Assert.Equal(target, winner.StoreName); Assert.NotEqual(request.Address.Key, winner.ObjectKey);
            if (deleteWinner) { await env.Get<FileManagementService>().DeleteAsync(session.FileId); await env.RunAsync(); }
            return await commit(); // 迟到的真实文件写入发生在胜者发布/删除之后。
        });
        var staleMigration = new FileMigrationService(store, hooked, env.Get<FileManagementOptions>());
        var staleWorker = new FileOperationProcessor(store, hooked, staleMigration, env.Get<FileManagementOptions>(), NullLogger<FileOperationProcessor>.Instance);
        await staleWorker.RunBatchAsync(); await env.RunAsync();
        var attempts = await ReviewRows.ReadAsync<FileTransferAttempt>(env);
        Assert.Equal(2, attempts.Count); Assert.Equal(2, attempts.Select(x => x.ObjectKey).Distinct().Count());
        var published = Assert.Single(attempts, x => x.State == FileTransferState.Published);
        var loser = Assert.Single(attempts, x => x.State == FileTransferState.Cleaned);
        var backend = factory.GetStorage(target, winner!.Scope());
        Assert.Null(await backend.GetPropertiesAsync(new(loser.Container, loser.ObjectKey)));
        Assert.Equal(winner!.ObjectKey, published.ObjectKey);
        var current = (await store.FindAsync(session.FileId))!.File;
        Assert.Equal(deleteWinner ? FileState.Deleted : FileState.Ready, current.State);
        if (deleteWinner) Assert.Null(await backend.GetPropertiesAsync(current.Address()));
        else
        {
            using var output = new MemoryStream(); await backend.CopyToAsync(current.Address(), output); Assert.Equal(bytes, output.ToArray());
            await env.Get<FileManagementService>().DeleteAsync(session.FileId); await env.RunAsync();
        }
        Assert.Empty(await store.ListDueOperationsAsync(DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds(), 20));
    }

    [Theory]
    [InlineData(false, "before")] [InlineData(true, "before")]
    [InlineData(false, "after")] [InlineData(true, "after")]
    [InlineData(false, "restart")] [InlineData(true, "restart")]
    [InlineData(false, "checksum")] [InlineData(true, "checksum")]
    public async Task Import_FaultsAndRestart_KeepOnePublishedObject(bool freeSql, string fault)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var bytes = new byte[] { 1, 3, 5 }; var path = Path.Combine(env.Root, "legacy.bin"); await File.WriteAllBytesAsync(path, bytes);
        var request = new LegacyImportRequest { Source = "review", LegacyId = Guid.NewGuid(), Scope = StorageScope.ForTenant("tenant-a"), OwnerId = "user-a",
            RootPath = env.Root, RelativePath = "legacy.bin", FileName = "legacy.bin", ExpectedLength = bytes.Length, StoreName = "local-v2" };
        var store = env.Get<IFileManagementStore>(); var proxy = ReviewStoreProxy.Wrap(store);
        var factory = env.Get<IFileStorageFactory>();
        var hooked = new ReviewStorageFactory(factory, "local-v2", async (write, commit) =>
        {
            Assert.Equal(write.Address.Key, Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env)).ObjectKey);
            var result = await commit();
            if (fault == "checksum") await factory.GetStorage("local-v2", request.Scope).SaveAsync(write, new MemoryStream(new byte[] { 5, 3, 1 }));
            return result;
        });
        proxy.OnCreate = async commit => { if (fault == "after") Assert.True(await commit()); throw new ReviewDbException(); };
        if (fault == "restart") proxy.OnRetire = _ => Task.FromException<FileTransferAttempt?>(new ReviewDbException());
        var migration = new FileMigrationService((IFileManagementStore)(object)proxy, hooked, env.Get<FileManagementOptions>());
        if (fault == "checksum") Assert.Equal("NeedsAttention", (await migration.ImportAsync(request, true)).Status);
        else await Assert.ThrowsAsync<ReviewDbException>(() => migration.ImportAsync(request, true));
        if (fault == "restart")
        {
            var active = Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env)); Assert.Equal(FileTransferState.Active, active.State);
            await ReviewRows.SqlAsync(env, "UPDATE BingFileTransferAttempts SET LeaseUntil=0,NextCheckAt=0");
        }
        await env.RestartAsync(); await env.RunAsync();
        var resultMap = await env.Get<FileMigrationService>().ImportAsync(request, true);
        Assert.Equal("Imported", resultMap.Status); Assert.Equal(request.LegacyId, resultMap.FileId);
        Assert.Equal(resultMap.Id, (await env.Get<FileMigrationService>().ImportAsync(request, true)).Id);
        var attempts = await ReviewRows.ReadAsync<FileTransferAttempt>(env);
        Assert.Single(attempts, x => x.State == FileTransferState.Published);
        Assert.All(attempts.Where(x => x.State != FileTransferState.Published), x => Assert.Equal(FileTransferState.Cleaned, x.State));
        var file = Assert.Single(await ReviewRows.ReadAsync<FileRecord>(env));
        using var output = new MemoryStream(); await env.Get<IFileStorageFactory>().GetStorage(file.StoreName, file.Scope()).CopyToAsync(file.Address(), output);
        Assert.Equal(bytes, output.ToArray()); Assert.Single(Directory.GetFiles(Path.Combine(env.Root, "other"), "*.blob", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Transfer_RetirementAndPublicationAreExclusive_RootConflictRollsBack(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var uploaded = await env.UploadAsync(new byte[] { 2 }); var store = env.Get<IFileManagementStore>();
        var entry = (await store.FindAsync(uploaded.FileId))!; var file = entry.File;
        var attempt = NewAttempt(file); var lease = new FileTransferLease(attempt.Id, attempt.LeaseToken);
        await store.CreateTransferAsync(attempt); await store.MarkTransferWrittenAsync(lease);
        file.ObjectKey = attempt.ObjectKey;
        Assert.False(await store.TryUpdateAsync(entry, file.Version - 1, transfer: lease));
        Assert.Equal(FileTransferState.Active, Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env)).State);
        Assert.NotNull(await store.RetireTransferAsync(attempt.Id, lease.Token));
        Assert.False(await store.TryUpdateAsync(entry, file.Version, transfer: lease));
        var second = NewAttempt(file); var secondLease = new FileTransferLease(second.Id, second.LeaseToken);
        await store.CreateTransferAsync(second); await store.MarkTransferWrittenAsync(secondLease); file.ObjectKey = second.ObjectKey;
        Assert.True(await store.TryUpdateAsync(entry, file.Version, transfer: secondLease));
        Assert.Null(await store.RetireTransferAsync(second.Id, secondLease.Token));
        var expired = NewAttempt(file); expired.LeaseUntil = 0; await store.CreateTransferAsync(expired);
        var expiredLease = new FileTransferLease(expired.Id, expired.LeaseToken); await store.MarkTransferWrittenAsync(expiredLease);
        file.ObjectKey = expired.ObjectKey;
        Assert.False(await store.TryUpdateAsync(entry, file.Version, transfer: expiredLease));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UnknownWriter_RetiredTargetIsRecheckedAfterLateWrite(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>(); var scope = StorageScope.ForTenant("tenant-a");
        var attempt = NewAttempt(new FileRecord { Id = Guid.NewGuid(), StoreName = "local-v2", Container = "attachments", ScopeKey = scope.Key, ScopeKind = scope.Kind, TenantId = scope.TenantId });
        attempt.LeaseUntil = attempt.NextCheckAt = 0; await store.CreateTransferAsync(attempt);
        await env.RestartAsync(); await env.RunAsync(); // 模拟崩溃后，尚无对象的第一次回收。
        var retired = Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env)); Assert.Equal(FileTransferState.Retired, retired.State);
        var backend = env.Get<IFileStorageFactory>().GetStorage(attempt.StoreName, scope); var address = new FileAddress(attempt.Container, attempt.ObjectKey);
        await backend.SaveAsync(new(address, 3, FileWriteMode.Overwrite), new MemoryStream(new byte[] { 6, 6, 6 }));
        store = env.Get<IFileManagementStore>();
        Assert.Equal(1, (await store.GetStatisticsAsync()).PendingCleanup);
        Assert.True(await store.FinishTransferCleanupAsync(retired.Id, retired.Version, 0));
        await env.RunAsync(); Assert.Null(await backend.GetPropertiesAsync(address));
        Assert.Equal(FileTransferState.Retired, Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env)).State);
        await store.MarkTransferWrittenAsync(new(attempt.Id, attempt.LeaseToken)); await env.RunAsync();
        Assert.Equal(FileTransferState.Cleaned, Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env)).State);
        Assert.Equal(0, (await store.GetStatisticsAsync()).PendingCleanup);
    }

    private static FileTransferAttempt NewAttempt(FileRecord file) => new()
    {
        Id = Guid.NewGuid(), FileId = file.Id, StoreName = file.StoreName, Container = file.Container, ObjectKey = Guid.NewGuid().ToString("N"),
        ScopeKey = file.ScopeKey, ScopeKind = file.ScopeKind, TenantId = file.TenantId, LeaseToken = Guid.NewGuid().ToString("N"),
        LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeMilliseconds(), Version = 1
    };
}

internal static class ReviewRows
{
    internal static async Task<List<T>> ReadAsync<T>(FileTestEnvironment env) where T : class
    {
        if (env.Provider.GetService<IFreeSql>() is { } orm) return await orm.Select<T>().ToListAsync();
        await using var db = await env.Get<IDbContextFactory<FileManagementDbContext>>().CreateDbContextAsync();
        return await db.Set<T>().AsNoTracking().ToListAsync();
    }
    internal static async Task SqlAsync(FileTestEnvironment env, string sql)
    {
        await using var db = new SqliteConnection("Data Source=" + Path.Combine(env.Root, "files.db")); await db.OpenAsync();
        await using var command = db.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
    }
}

internal sealed class ReviewStorageFactory : IFileStorageFactory
{
    private readonly IFileStorageFactory _inner; private readonly string _target;
    private readonly Func<FileWriteRequest, Func<Task<FileWriteResult>>, Task<FileWriteResult>> _save;
    internal ReviewStorageFactory(IFileStorageFactory inner, string target, Func<FileWriteRequest, Func<Task<FileWriteResult>>, Task<FileWriteResult>> save)
    { _inner = inner; _target = target; _save = save; }
    public string DefaultStore => _inner.DefaultStore;
    public IFileStorage GetStorage(string storeName, StorageScope scope)
    {
        var storage = _inner.GetStorage(storeName, scope);
        if (storeName != _target) return storage;
        var proxy = DispatchProxy.Create<IFileStorage, ReviewStorageProxy>(); var wrapper = (ReviewStorageProxy)proxy;
        wrapper.Inner = storage; wrapper.OnSave = _save; return proxy;
    }
}
public class ReviewStorageProxy : DispatchProxy
{
    public IFileStorage Inner { get; set; } = null!;
    public Func<FileWriteRequest, Func<Task<FileWriteResult>>, Task<FileWriteResult>> OnSave { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == nameof(IFileStorage.SaveAsync)
        ? OnSave((FileWriteRequest)args![0]!, () => (Task<FileWriteResult>)method.Invoke(Inner, args)!) : method.Invoke(Inner, args);
}
