using System.Reflection;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.FileStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>真实传输记录、SQLite 与 Local IO；仅在持久化/IO 边界控制时序或注入故障。</summary>
public sealed class FileTransferLeaseTests
{
    [Fact]
    public async Task Renewal_OverlapsWriterConfirmation_ReloadsAndCompletesCopy()
    {
        var gate = new TransferLeaseGate();
        await using var env = new FileTestEnvironment { ConfigureManagement = o => o.OperationLease = TimeSpan.FromSeconds(10) };
        env.CustomizeServices = s => s.AddSingleton<IDbContextFactory<FileManagementDbContext>>(new TransferLeaseFactory(env.Root, gate));
        await env.InitializeAsync();
        var (fileId, operationId) = await Prepare(env, "copy");
        var factory = new TransferIoFactory(env.Get<IFileStorageFactory>(), "local-v2")
        {
            AfterWrite = async ct => await gate.Entered.Task.WaitAsync(ct),
            BeforeProperties = async ct => await gate.Renewed.Task.WaitAsync(ct)
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await Processor(env, env.Get<IFileManagementStore>(), factory).RunBatchAsync(deadline.Token);
        Assert.False(deadline.IsCancellationRequested);
        Assert.Equal(1, gate.Conflicts);
        Assert.Equal(2, gate.Attempts);
        Assert.Equal(FileState.Ready, (await env.Get<IFileManagementStore>().FindAsync(fileId))!.File.State);
        Assert.Equal(FileOperationState.Succeeded, (await env.Get<IFileManagementStore>().FindOperationAsync(operationId))!.State);
        Assert.Equal(FileBindingCopyState.Copied, Assert.Single(await ReviewRows.ReadAsync<FileBindingCopyRequest>(env)).State);
        Assert.Equal(FileTransferState.Published, Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env)).State);
    }

    [Fact]
    public async Task Renewal_RepeatedWriterConflicts_IsBounded()
    {
        var gate = new TransferLeaseGate();
        await using var env = new FileTestEnvironment();
        env.CustomizeServices = s => s.AddSingleton<IDbContextFactory<FileManagementDbContext>>(new TransferLeaseFactory(env.Root, gate));
        await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>();
        var value = NewTransfer(); await store.CreateTransferAsync(value);
        gate.BeforeRenewal = (_, ct) => ReviewRows.SqlAsync(env, "UPDATE BingFileTransferAttempts SET Version=Version+1");
        Assert.False(await store.RenewTransferAsync(new(value.Id, value.LeaseToken), Now + 120000));
        Assert.Equal(3, gate.Attempts); Assert.Equal(3, gate.Conflicts);
        var saved = Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env));
        Assert.Equal(value.LeaseUntil, saved.LeaseUntil);
        Assert.Equal(value.LeaseToken, saved.LeaseToken);
    }

    [Theory]
    [InlineData("successor")] [InlineData("expired")] [InlineData("retired")] [InlineData("published")]
    public async Task Renewal_ConflictReloadRejectsChangedOwnerOrState(string change)
    {
        var gate = new TransferLeaseGate();
        await using var env = new FileTestEnvironment();
        env.CustomizeServices = s => s.AddSingleton<IDbContextFactory<FileManagementDbContext>>(new TransferLeaseFactory(env.Root, gate));
        await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>(); var value = NewTransfer();
        await store.CreateTransferAsync(value);
        var update = change switch
        {
            "successor" => "LeaseToken='successor'",
            "expired" => "LeaseUntil=0",
            "retired" => "State=" + (int)FileTransferState.Retired,
            _ => "State=" + (int)FileTransferState.Published
        };
        gate.BeforeRenewal = (_, _) => ReviewRows.SqlAsync(env, "UPDATE BingFileTransferAttempts SET Version=Version+1," + update);
        Assert.False(await store.RenewTransferAsync(new(value.Id, value.LeaseToken), Now + 120000));
        Assert.Equal(1, gate.Attempts); Assert.Equal(1, gate.Conflicts);
        var saved = Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env));
        Assert.Equal(change == "successor" ? "successor" : value.LeaseToken, saved.LeaseToken);
        Assert.Equal(change == "expired" ? 0 : value.LeaseUntil, saved.LeaseUntil);
    }

    [Theory]
    [InlineData(false, "expired")] [InlineData(true, "expired")]
    [InlineData(false, "successor")] [InlineData(true, "successor")]
    [InlineData(false, "retired")] [InlineData(true, "retired")]
    [InlineData(false, "published")] [InlineData(true, "published")]
    public async Task Renewal_RejectsExpiredOtherOwnerAndTerminal(bool freeSql, string state)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>(); var value = NewTransfer();
        if (state == "expired") value.LeaseUntil = 0;
        if (state == "retired") value.State = FileTransferState.Retired;
        if (state == "published") value.State = FileTransferState.Published;
        await store.CreateTransferAsync(value);
        Assert.False(await store.RenewTransferAsync(new(value.Id, state == "successor" ? "old-owner" : value.LeaseToken), Now + 120000));
        Assert.Equal(value.LeaseUntil, Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env)).LeaseUntil);
    }

    [Theory]
    [InlineData(false, "copy", "loss")] [InlineData(true, "copy", "loss")]
    [InlineData(false, "copy", "database")] [InlineData(true, "copy", "database")]
    [InlineData(false, "migration", "loss")] [InlineData(true, "migration", "loss")]
    [InlineData(false, "empty", "database")] [InlineData(true, "empty", "database")]
    public async Task Worker_InternalLeaseStop_RetriesAfterRestart(bool freeSql, string kind, string fault)
    {
        await using var env = new FileTestEnvironment(freeSql) { ConfigureManagement = o => o.OperationLease = TimeSpan.FromSeconds(10) };
        await env.InitializeAsync();
        var (fileId, operationId) = await Prepare(env, kind);
        var store = env.Get<IFileManagementStore>();
        var proxy = DispatchProxy.Create<ITransferLeaseTestStore, TransferLeaseStoreProxy>();
        ((TransferLeaseStoreProxy)proxy).Inner = store;
        ((TransferLeaseStoreProxy)proxy).FailRenewal = fault == "database";
        var cancelledInternally = false;
        var factory = new TransferIoFactory(env.Get<IFileStorageFactory>(), kind == "empty" ? "local-v1" : "local-v2")
        {
            AfterWrite = async ct =>
            {
                if (fault == "loss")
                    await ReviewRows.SqlAsync(env, "UPDATE BingFileTransferAttempts SET LeaseToken='successor',Version=Version+1");
                try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { cancelledInternally = true; throw; }
            }
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await Processor(env, proxy, factory).RunBatchAsync(deadline.Token);
        Assert.True(cancelledInternally); Assert.False(deadline.IsCancellationRequested);
        var operation = (await store.FindOperationAsync(operationId))!;
        Assert.Equal(FileOperationState.Pending, operation.State);
        Assert.Equal(fault == "loss" ? "storage:Conflict" : "storage:Unavailable", operation.LastError);
        Assert.NotEqual(FileState.Failed, (await store.FindAsync(fileId))!.File.State);
        if (kind == "copy")
        {
            var copy = Assert.Single(await ReviewRows.ReadAsync<FileBindingCopyRequest>(env));
            Assert.NotEqual(FileBindingCopyState.Failed, copy.State);
            Assert.Equal(FileState.Ready, (await store.FindAsync(copy.SourceFileId))!.File.State);
        }
        var previous = Assert.Single(await ReviewRows.ReadAsync<FileTransferAttempt>(env));
        if (fault == "loss")
        {
            Assert.Equal("successor", previous.LeaseToken);
            Assert.Equal(FileTransferState.Active, previous.State);
            Assert.NotNull(await env.Get<IFileStorageFactory>().GetStorage(previous.StoreName, StorageScope.ForTenant("tenant-a"))
                .GetPropertiesAsync(new(previous.Container, previous.ObjectKey)));
        }
        // 正常退避时间保持不变；等到已持久化的重试时间后重启执行。
        await env.RestartAsync();
        var delay = operation.NextAttemptAt - Now;
        if (delay > 0) await Task.Delay(TimeSpan.FromMilliseconds(delay + 20), deadline.Token);
        await env.Get<FileOperationProcessor>().RunBatchAsync(deadline.Token);
        store = env.Get<IFileManagementStore>();
        Assert.Equal(FileOperationState.Succeeded, (await store.FindOperationAsync(operationId))!.State);
        var file = (await store.FindAsync(fileId))!.File;
        Assert.Equal(FileState.Ready, file.State);
        using var bytes = new MemoryStream();
        await env.Get<IFileStorageFactory>().GetStorage(file.StoreName, file.Scope()).CopyToAsync(file.Address(), bytes);
        Assert.Equal(kind == "empty" ? Array.Empty<byte>() : new byte[] { 1, 2, 3, 4 }, bytes.ToArray());
        Assert.NotEqual(previous.ObjectKey, file.ObjectKey);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Worker_CallerCancellation_RemainsCancellation(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var (_, operationId) = await Prepare(env, "copy");
        using var cancellation = new CancellationTokenSource();
        var factory = new TransferIoFactory(env.Get<IFileStorageFactory>(), "local-v2")
        { AfterWrite = ct => { cancellation.Cancel(); ct.ThrowIfCancellationRequested(); return Task.CompletedTask; } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(env, env.Get<IFileManagementStore>(), factory).RunBatchAsync(cancellation.Token));
        Assert.NotEqual(FileOperationState.Failed, (await env.Get<IFileManagementStore>().FindOperationAsync(operationId))!.State);
    }

    private static FileOperationProcessor Processor(FileTestEnvironment env, IFileManagementStore store, IFileStorageFactory factory)
        => new(store, factory, new FileMigrationService(store, factory, env.Get<FileManagementOptions>()), env.Get<FileManagementOptions>(), NullLogger<FileOperationProcessor>.Instance);
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static FileTransferAttempt NewTransfer() => new() { Id = Guid.NewGuid(), FileId = Guid.NewGuid(), StoreName = "local-v2", Container = "attachments", ObjectKey = Guid.NewGuid().ToString("N"),
        ScopeKey = "host", LeaseToken = "owner", LeaseUntil = Now + 60000, Version = 1 };

    private static async Task<(Guid FileId, Guid OperationId)> Prepare(FileTestEnvironment env, string kind)
    {
        var store = env.Get<IFileManagementStore>();
        if (kind == "empty")
        {
            var service = env.Get<FileManagementService>();
            var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "empty", Length = 0 });
            await env.RunAsync(); await service.CompleteUploadAsync(session.Id);
            return (session.FileId, (await store.ListDueOperationsAsync(Now, 20)).Single(x => x.Kind == FileOperationKind.Complete).Id);
        }
        var uploaded = await env.UploadAsync(new byte[] { 1, 2, 3, 4 });
        if (kind == "migration")
        {
            await env.Get<FileMigrationService>().QueueMigrationAsync(uploaded.FileId, "local-v2");
            return (uploaded.FileId, (await store.ListDueOperationsAsync(Now, 20)).Single(x => x.Kind == FileOperationKind.Migrate).Id);
        }
        var source = (await store.FindAsync(uploaded.FileId))!;
        source.File.Ownership = Bing.FileStorage.RemoteDrive.RemoteDriveOwnership.Personal;
        Assert.True(await store.TryUpdateAsync(source, source.File.Version));
        var target = new FileRecord { Id = Guid.NewGuid(), RequestKey = Guid.NewGuid().ToString("N"), ScopeKind = source.File.ScopeKind,
            ScopeKey = source.File.ScopeKey, TenantId = source.File.TenantId, StoreName = "local-v2", Container = source.File.Container, ObjectKey = "pending", FileName = "copy",
            Length = source.File.Length, CreatorId = source.File.CreatorId, State = FileState.Pending, Version = 1 };
        var copy = new FileBindingCopyRequest { Id = Guid.NewGuid(), RequestKey = Guid.NewGuid().ToString("N"), ScopeKey = source.File.ScopeKey,
            OwnerId = source.File.CreatorId, SourceFileId = source.File.Id, SourceVersion = source.File.Version, CopyFileId = target.Id, BusinessType = "orders", BusinessId = "1" };
        var operation = new FileOperation { Id = Guid.NewGuid(), FileId = target.Id, Kind = FileOperationKind.CopyForBinding, State = FileOperationState.Pending, Payload = copy.Id.ToString("N") };
        Assert.True(await ((IFileStorageTargetStore)store).CreateCopyAsync(copy, new FileEntry { File = target }, operation));
        return (target.Id, operation.Id);
    }
}

public interface ITransferLeaseTestStore : IFileManagementStore, IFileStorageTargetStore { }
public class TransferLeaseStoreProxy : DispatchProxy
{
    public IFileManagementStore Inner { get; set; } = null!;
    public bool FailRenewal { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == nameof(IFileManagementStore.RenewTransferAsync) && FailRenewal)
            return Task.FromException<bool>(new IOException("injected transfer renewal failure"));
        return method.Invoke(Inner, args);
    }
}

internal sealed class TransferIoFactory : IFileStorageFactory
{
    private readonly IFileStorageFactory _inner; private readonly string _target;
    public TransferIoFactory(IFileStorageFactory inner, string target) { _inner = inner; _target = target; }
    public Func<CancellationToken, Task>? AfterWrite { get; init; }
    public Func<CancellationToken, Task>? BeforeProperties { get; init; }
    public string DefaultStore => _inner.DefaultStore;
    public IFileStorage GetStorage(string name, StorageScope scope)
        => name == _target ? new TransferIoStorage(_inner.GetStorage(name, scope), this) : _inner.GetStorage(name, scope);
    private sealed class TransferIoStorage : IFileStorage
    {
        private readonly IFileStorage _inner; private readonly TransferIoFactory _owner;
        public TransferIoStorage(IFileStorage inner, TransferIoFactory owner) { _inner = inner; _owner = owner; }
        public string Name => _inner.Name; public StorageScope Scope => _inner.Scope; public FileStorageCapabilities Capabilities => _inner.Capabilities;
        public async Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream stream, CancellationToken ct = default)
        { var result = await _inner.SaveAsync(request, stream, ct); if (_owner.AfterWrite != null) await _owner.AfterWrite(ct); return result; }
        public async Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken ct = default)
        { if (_owner.BeforeProperties != null) await _owner.BeforeProperties(ct); return await _inner.GetPropertiesAsync(address, ct); }
        public Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken ct = default) => _inner.ReadAsync(address, consume, ct);
        public Task DeleteAsync(FileAddress address, CancellationToken ct = default) => _inner.DeleteAsync(address, ct);
    }
}

internal sealed class TransferLeaseFactory : IDbContextFactory<FileManagementDbContext>
{
    private readonly DbContextOptions<FileManagementDbContext> _options;
    public TransferLeaseFactory(string root, TransferLeaseGate gate) => _options = new DbContextOptionsBuilder<FileManagementDbContext>()
        .UseSqlite("Data Source=" + Path.Combine(root, "files.db")).AddInterceptors(gate).Options;
    public FileManagementDbContext CreateDbContext() => new(_options);
    public Task<FileManagementDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
}

internal sealed class TransferLeaseGate : SaveChangesInterceptor
{
    public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Renewed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Func<FileTransferAttempt, CancellationToken, Task>? BeforeRenewal { get; set; }
    public int Attempts { get; private set; }
    public int Conflicts { get; private set; }
    private readonly HashSet<Guid> _contexts = new();
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var entry = data.Context!.ChangeTracker.Entries<FileTransferAttempt>().SingleOrDefault();
        if (entry?.State != EntityState.Modified || !entry.Property(x => x.LeaseUntil).IsModified) return result;
        Attempts++; _contexts.Add(data.Context.ContextId.InstanceId);
        if (BeforeRenewal != null) await BeforeRenewal(entry.Entity, ct);
        else if (Attempts == 1) { Entered.TrySetResult(true); await Resume.Task.WaitAsync(ct); }
        return result;
    }
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
    {
        if (_contexts.Remove(data.Context!.ContextId.InstanceId)) Renewed.TrySetResult(true);
        var entry = data.Context.ChangeTracker.Entries<FileTransferAttempt>().SingleOrDefault();
        if (entry?.Entity.WriterCompleted == true) Resume.TrySetResult(true);
        return ValueTask.FromResult(result);
    }
    public override Task SaveChangesFailedAsync(DbContextErrorEventData data, CancellationToken ct = default)
    { if (data.Exception is DbUpdateConcurrencyException && _contexts.Remove(data.Context!.ContextId.InstanceId)) Conflicts++; return Task.CompletedTask; }
}
