using System.Reflection;
using System.Security.Cryptography;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed partial class RemoteDriveWorkflowTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MigrationQueue_RejectsPublicFileForPrivateOnlyDrive_WithoutChangingSource(bool freeSql, bool bindSource)
    {
        using var client = new DriveFixture();
        await using var env = Setup(freeSql, client);
        await env.InitializeAsync();
        var connection = await Connect(env, RemoteDriveOwnership.Shared);
        var bytes = new byte[] { 3, 1, 4, 1, 5 };
        var uploaded = await env.UploadAsync(bytes, "public");
        if (bindSource)
        {
            env.Authorizer.Granted = true;
            await env.Get<FileManagementService>().BindAsync(uploaded.FileId, "orders", "migration-limit");
        }

        var store = env.Get<IFileManagementStore>();
        var before = (await store.FindAsync(uploaded.FileId))!;
        var target = env.Get<IFileStorageFactory>().GetStorage(connection.StoreName, connection.Scope());
        var targetLimits = await ((IFileStorageLimitProvider)target).GetLimitsAsync();
        Assert.True(targetLimits!.PrivateContainersOnly);

        var error = await Assert.ThrowsAsync<FileManagementException>(() => env.Get<FileMigrationService>().QueueMigrationAsync(uploaded.FileId, connection.StoreName));

        Assert.Equal("invalid", error.Code);
        var after = (await store.FindAsync(uploaded.FileId))!;
        Assert.Equal(before.File.Version, after.File.Version);
        Assert.Equal(before.File.StoreName, after.File.StoreName);
        Assert.Equal(before.File.ObjectKey, after.File.ObjectKey);
        Assert.Equal(FileVisibility.Public, after.File.Visibility);
        Assert.Equal(FileState.Ready, after.File.State);
        Assert.Equal(before.Bindings.Select(x => x.Id), after.Bindings.Select(x => x.Id));
        Assert.Empty(await store.QueryOperationsAsync(new() { ScopeKey = after.File.ScopeKey, FileId = uploaded.FileId, Kind = FileOperationKind.Migrate }));
        Assert.Empty(await store.QueryOperationsAsync(new() { ScopeKey = after.File.ScopeKey, FileId = uploaded.FileId, Kind = FileOperationKind.DeleteReplica }));
        Assert.Equal(0, RemoteFileCount(client));
        Assert.Equal(bytes, await ReadFileAsync(env, after.File));
    }

    [Theory]
    [InlineData(false, "private-only")]
    [InlineData(false, "maximum-length")]
    [InlineData(true, "private-only")]
    [InlineData(true, "maximum-length")]
    public async Task MigrationWorker_RechecksChangedDestinationLimits_AndKeepsSource(bool freeSql, string changedLimit)
    {
        using var client = new DriveFixture();
        await using var env = Setup(freeSql, client);
        await env.InitializeAsync();
        var connection = await Connect(env, RemoteDriveOwnership.Shared);
        var bytes = new byte[] { 8, 6, 7, 5, 3 };
        var isPublic = changedLimit == "private-only";
        var uploaded = await env.UploadAsync(bytes, isPublic ? "public" : "attachments");
        env.Authorizer.Granted = true;
        var binding = await env.Get<FileManagementService>().BindAsync(uploaded.FileId, "orders", "worker-limit");

        var store = env.Get<IFileManagementStore>();
        var limits = new MutableMigrationLimits { MaximumFileLength = bytes.Length, PrivateContainersOnly = false };
        var factory = new MigrationLimitStorageFactory(env.Get<IFileStorageFactory>(), connection.StoreName, limits);
        var migration = new FileMigrationService(store, factory, env.Get<FileManagementOptions>());
        var operationId = await migration.QueueMigrationAsync(uploaded.FileId, connection.StoreName);
        var queued = (await store.FindAsync(uploaded.FileId))!;
        limits.PrivateContainersOnly = changedLimit == "private-only";
        limits.MaximumFileLength = changedLimit == "maximum-length" ? bytes.Length - 1 : bytes.Length;

        var processor = new FileOperationProcessor(store, factory, migration, env.Get<FileManagementOptions>(),
            NullLogger<FileOperationProcessor>.Instance);
        await processor.RunBatchAsync();

        var operation = (await store.FindOperationAsync(operationId))!;
        Assert.Equal(FileOperationState.Failed, operation.State);
        Assert.Equal("files:invalid", operation.LastError);
        var after = (await store.FindAsync(uploaded.FileId))!;
        Assert.Equal(queued.File.Version, after.File.Version);
        Assert.Equal(queued.File.StoreName, after.File.StoreName);
        Assert.Equal(queued.File.ObjectKey, after.File.ObjectKey);
        Assert.Equal(queued.File.Visibility, after.File.Visibility);
        Assert.Equal(FileState.Ready, after.File.State);
        Assert.Contains(after.Bindings, x => x.Id == binding.Id);
        Assert.Empty(await store.QueryOperationsAsync(new() { ScopeKey = after.File.ScopeKey, FileId = uploaded.FileId, Kind = FileOperationKind.DeleteReplica }));
        Assert.Equal(0, RemoteFileCount(client));
        Assert.Equal(bytes, await ReadFileAsync(env, after.File));

        await processor.RunBatchAsync();
        Assert.Equal(FileOperationState.Failed, (await store.FindOperationAsync(operationId))!.State);
        Assert.Equal(queued.File.ObjectKey, (await store.FindAsync(uploaded.FileId))!.File.ObjectKey);
        Assert.Equal(0, RemoteFileCount(client));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_AllowsPrivateOnlyLengthBoundary_AndRejectsOversize(bool freeSql)
    {
        using var client = new DriveFixture();
        await using var env = Setup(freeSql, client);
        await env.InitializeAsync();
        var connection = await Connect(env, RemoteDriveOwnership.Shared);
        var store = env.Get<IFileManagementStore>();
        var limits = new MutableMigrationLimits { MaximumFileLength = 5, PrivateContainersOnly = true };
        var factory = new MigrationLimitStorageFactory(env.Get<IFileStorageFactory>(), connection.StoreName, limits);
        var migration = new FileMigrationService(store, factory, env.Get<FileManagementOptions>());

        var boundaryBytes = new byte[] { 2, 4, 6, 8, 10 };
        var boundary = await env.UploadAsync(boundaryBytes);
        var oversizeBytes = new byte[] { 2, 4, 6, 8, 10, 12 };
        var oversize = await env.UploadAsync(oversizeBytes);
        var oversizeBefore = (await store.FindAsync(oversize.FileId))!;
        var operationId = await migration.QueueMigrationAsync(boundary.FileId, connection.StoreName);
        var boundaryQueued = (await store.FindAsync(boundary.FileId))!;

        var error = await Assert.ThrowsAsync<FileManagementException>(() => migration.QueueMigrationAsync(oversize.FileId, connection.StoreName));

        Assert.Equal("invalid", error.Code);
        var oversizeAfter = (await store.FindAsync(oversize.FileId))!;
        Assert.Equal(oversizeBefore.File.Version, oversizeAfter.File.Version);
        Assert.Equal(oversizeBefore.File.ObjectKey, oversizeAfter.File.ObjectKey);
        Assert.Empty(await store.QueryOperationsAsync(new() { ScopeKey = oversizeAfter.File.ScopeKey, FileId = oversize.FileId, Kind = FileOperationKind.Migrate }));
        Assert.Equal(0, RemoteFileCount(client));

        await new FileOperationProcessor(store, factory, migration, env.Get<FileManagementOptions>(), NullLogger<FileOperationProcessor>.Instance).RunBatchAsync();

        Assert.Equal(FileOperationState.Succeeded, (await store.FindOperationAsync(operationId))!.State);
        var migrated = (await store.FindAsync(boundary.FileId))!;
        Assert.Equal(connection.StoreName, migrated.File.StoreName);
        Assert.Equal(boundaryQueued.File.Version + 1, migrated.File.Version);
        Assert.Equal(boundaryBytes, await ReadFileAsync(env, migrated.File));
        Assert.Equal(1, RemoteFileCount(client));
        Assert.Equal(oversizeBytes, await ReadFileAsync(env, oversizeAfter.File));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_AllowsPublicFileOnStorageWithoutPrivateOnlyLimit(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        var bytes = new byte[] { 7, 7, 2 };
        var uploaded = await env.UploadAsync(bytes, "public");

        await env.Get<FileMigrationService>().QueueMigrationAsync(uploaded.FileId, "local-v2");
        await env.RunAsync();

        var migrated = (await env.Get<IFileManagementStore>().FindAsync(uploaded.FileId))!;
        Assert.Equal("local-v2", migrated.File.StoreName);
        Assert.Equal(FileVisibility.Public, migrated.File.Visibility);
        Assert.NotNull(await env.Get<FileManagementService>().AuthorizeReadAsync(uploaded.FileId, publicAccess: true));
        Assert.Equal(bytes, await ReadFileAsync(env, migrated.File));
    }

    private static async Task<byte[]> ReadFileAsync(FileTestEnvironment env, FileRecord file)
    {
        byte[]? bytes = null;
        await env.Get<IFileStorageFactory>().GetStorage(file.StoreName, file.Scope()).ReadAsync(file.Address(), async (input, ct) =>
        {
            using var output = new MemoryStream();
            await input.CopyToAsync(output, ct);
            bytes = output.ToArray();
        });
        return bytes ?? throw new InvalidOperationException("文件读取回调没有执行。");
    }

    private static int RemoteFileCount(DriveFixture client)
    {
        var files = typeof(DriveFixture).GetField("_files", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client);
        return (int)files!.GetType().GetProperty("Count")!.GetValue(files)!;
    }

    private sealed class MutableMigrationLimits
    {
        public long MaximumFileLength { get; set; }
        public bool PrivateContainersOnly { get; set; }
        public FileStorageLimits Snapshot() => new() { MaximumFileLength = MaximumFileLength, PrivateContainersOnly = PrivateContainersOnly };
    }

    private sealed class MigrationLimitStorageFactory : IFileStorageFactory
    {
        private readonly IFileStorageFactory _inner;
        private readonly string _limitedStoreName;
        private readonly MutableMigrationLimits _limits;

        public MigrationLimitStorageFactory(IFileStorageFactory inner, string limitedStoreName, MutableMigrationLimits limits)
        { _inner = inner; _limitedStoreName = limitedStoreName; _limits = limits; }

        public string DefaultStore => _inner.DefaultStore;
        public IFileStorage GetStorage(string storeName, StorageScope scope)
        {
            var storage = _inner.GetStorage(storeName, scope);
            return storeName == _limitedStoreName ? new MigrationLimitStorage(storage, _limits) : storage;
        }
    }

    private sealed class MigrationLimitStorage : IFileStorage, IFileStorageLimitProvider
    {
        private readonly IFileStorage _inner;
        private readonly MutableMigrationLimits _limits;

        public MigrationLimitStorage(IFileStorage inner, MutableMigrationLimits limits) { _inner = inner; _limits = limits; }
        public string Name => _inner.Name;
        public StorageScope Scope => _inner.Scope;
        public FileStorageCapabilities Capabilities => _inner.Capabilities;
        public Task<FileStorageLimits?> GetLimitsAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<FileStorageLimits?>(_limits.Snapshot()); }
        public Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default)
            => _inner.SaveAsync(request, source, cancellationToken);
        public Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(address, consume, cancellationToken);
        public Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default)
            => _inner.GetPropertiesAsync(address, cancellationToken);
        public Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(address, cancellationToken);
    }
}
