using System.Reflection;
using Bing.Biz.FileManagement;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.FileManagement.Tests;

public sealed class BusinessFileBatchQueryTests
{
    private readonly ITestOutputHelper _output;

    public BusinessFileBatchQueryTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListBusinessFiles_UsesBoundedBatchReadsAndPreservesVisibleOffset(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        env.Authorizer.Granted = true;
        var store = env.Get<IFileManagementStore>();
        var entries = new List<FileEntry>();
        for (var index = 0; index < 205; index++)
            entries.Add(await AddBusinessFileAsync(store, 10_000 - index, "order-1"));

        var proxy = CreateBatchProxy(store);
        var page = await CreateService(env, proxy.Store).ListBusinessFilesAsync("orders", "order-1", skip: 120, take: 50);

        Assert.Equal(entries.Skip(120).Take(50).Select(entry => entry.File.Id), page.Select(file => file.Id));
        Assert.Equal(2, proxy.Probe.QueryReads);
        Assert.Equal(2, proxy.Probe.BatchReads);
        Assert.Equal(new[] { 100, 100 }, proxy.Probe.BatchSizes);
        Assert.Equal(0, proxy.Probe.FindReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListBusinessFiles_RechecksStateBindingAndPersonalOwnerAfterCandidateQuery(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        env.Authorizer.Granted = true;
        var store = env.Get<IFileManagementStore>();
        var stateChanged = await AddBusinessFileAsync(store, 30, "order-2");
        var bindingChanged = await AddBusinessFileAsync(store, 20, "order-2");
        var ownedByAnotherUser = await AddBusinessFileAsync(store, 10, "order-2", RemoteDriveOwnership.Personal, "user-b");
        var proxy = CreateBatchProxy(store);
        proxy.Probe.BeforeRead = async ct =>
        {
            var changedState = (await store.FindAsync(stateChanged.File.Id, ct))!;
            changedState.File.State = FileState.DeletePending;
            Assert.True(await store.TryUpdateAsync(changedState, changedState.File.Version, ct: ct));

            var changedBinding = (await store.FindAsync(bindingChanged.File.Id, ct))!;
            changedBinding.Bindings.Clear();
            Assert.True(await store.TryUpdateAsync(changedBinding, changedBinding.File.Version, ct: ct));
        };

        var page = await CreateService(env, proxy.Store).ListBusinessFilesAsync("orders", "order-2");

        Assert.Empty(page);
        Assert.Equal(1, proxy.Probe.BatchReads);
        Assert.Equal(0, proxy.Probe.FindReads);
        Assert.Contains(ownedByAnotherUser.File.Id, proxy.Probe.LastBatchIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListBusinessFiles_PropagatesCancellationFromBatchRead(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        env.Authorizer.Granted = true;
        var store = env.Get<IFileManagementStore>();
        await AddBusinessFileAsync(store, 1, "order-3");
        using var cancellation = new CancellationTokenSource();
        var proxy = CreateBatchProxy(store);
        proxy.Probe.BeforeRead = _ =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(env, proxy.Store).ListBusinessFilesAsync("orders", "order-3", ct: cancellation.Token));

        Assert.Equal(1, proxy.Probe.BatchReads);
        Assert.Equal(0, proxy.Probe.FindReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListBusinessFiles_LegacyStoreWithoutBatchCapabilityKeepsOrderedFallback(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        env.Authorizer.Granted = true;
        var store = env.Get<IFileManagementStore>();
        var entries = new List<FileEntry>();
        for (var index = 0; index < 25; index++)
            entries.Add(await AddBusinessFileAsync(store, 1_000 - index, "order-4"));
        var proxy = CreateLegacyProxy(store);

        var page = await CreateService(env, proxy.Store).ListBusinessFilesAsync("orders", "order-4", skip: 5, take: 10);

        Assert.Equal(entries.Skip(5).Take(10).Select(entry => entry.File.Id), page.Select(file => file.Id));
        Assert.Equal(1, proxy.Probe.QueryReads);
        Assert.Equal(15, proxy.Probe.FindReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListBusinessFiles_BatchCapabilityReducesSqlSelectsFrom151To3(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        env.Authorizer.Granted = true;
        var store = env.Get<IFileManagementStore>();
        for (var index = 0; index < 50; index++)
            await AddBusinessFileAsync(store, 10_000 - index, "order-sql-count");
        using var capture = new FileReadBatchSqlCapture(env.Provider, Path.Combine(env.Root, "files.db"));

        var legacy = CreateLegacyProxy(store);
        var legacyPage = await CreateService(env, legacy.Store).ListBusinessFilesAsync("orders", "order-sql-count");
        var legacySelects = capture.SelectStatements.Count;
        capture.Clear();

        var batchPage = await env.Get<FileManagementService>().ListBusinessFilesAsync("orders", "order-sql-count");
        var batchSelects = capture.SelectStatements.Count;

        Assert.Equal(legacyPage.Select(file => file.Id), batchPage.Select(file => file.Id));
        Assert.Equal(50, batchPage.Count);
        Assert.Equal(151, legacySelects);
        Assert.Equal(3, batchSelects);
        _output.WriteLine($"ORM={(freeSql ? "FreeSql" : "EF Core")}; business-files=50; legacy-selects={legacySelects}; batch-selects={batchSelects}");
    }

    private static async Task<FileEntry> AddBusinessFileAsync(IFileManagementStore store, long createdAt,
        string businessId, RemoteDriveOwnership ownership = RemoteDriveOwnership.Shared, string creatorId = "user-a")
    {
        var id = Guid.NewGuid();
        var scope = StorageScope.ForTenant("tenant-a");
        var entry = new FileEntry
        {
            File = new FileRecord
            {
                Id = id,
                RequestKey = id.ToString("N"),
                ScopeKey = scope.Key,
                ScopeKind = scope.Kind,
                TenantId = scope.TenantId,
                StoreName = "local-v1",
                Ownership = ownership,
                Container = "attachments",
                ObjectKey = id.ToString("N"),
                FileName = "business-attachment.txt",
                Length = 1,
                ContentType = "text/plain",
                State = FileState.Ready,
                CreatorId = creatorId,
                CreatedAt = createdAt,
                Version = 1
            },
            Bindings =
            {
                new FileBinding
                {
                    Id = Guid.NewGuid(),
                    FileId = id,
                    BusinessType = "orders",
                    BusinessId = businessId,
                    CreatorId = creatorId,
                    CreatedAt = createdAt
                }
            }
        };
        Assert.True(await store.TryCreateAsync(entry));
        return entry;
    }

    private static (IFileManagementStore Store, BatchBusinessFileStoreProxy Probe) CreateBatchProxy(IFileManagementStore inner)
    {
        var store = DispatchProxy.Create<IFileManagementStore, BatchBusinessFileStoreProxy>();
        var probe = (BatchBusinessFileStoreProxy)(object)store;
        probe.Inner = inner;
        return (store, probe);
    }

    private static (IFileManagementStore Store, LegacyBusinessFileStoreProxy Probe) CreateLegacyProxy(IFileManagementStore inner)
    {
        var store = DispatchProxy.Create<IFileManagementStore, LegacyBusinessFileStoreProxy>();
        var probe = (LegacyBusinessFileStoreProxy)(object)store;
        probe.Inner = inner;
        return (store, probe);
    }

    private static FileManagementService CreateService(FileTestEnvironment env, IFileManagementStore store)
        => new(store, env.Get<IFileStorageFactory>(), env.Identity, env.Identity,
            env.Get<IFilePermissionChecker>(), env.Authorizer, env.Get<FileManagementOptions>());
}

public class LegacyBusinessFileStoreProxy : DispatchProxy
{
    public IFileManagementStore Inner { get; set; } = null!;
    public int QueryReads { get; private set; }
    public int FindReads { get; private set; }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == nameof(IFileManagementStore.QueryFilesAsync)) QueryReads++;
        if (method.Name == nameof(IFileManagementStore.FindAsync)) FindReads++;
        try { return method.Invoke(Inner, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}

public class BatchBusinessFileStoreProxy : LegacyBusinessFileStoreProxy, IFileReadBatchStore
{
    public Func<CancellationToken, Task>? BeforeRead { get; set; }
    public int BatchReads { get; private set; }
    public IReadOnlyCollection<Guid> LastBatchIds { get; private set; } = Array.Empty<Guid>();
    public List<int> BatchSizes { get; } = new();

    public async Task<IReadOnlyList<FileReadSnapshot>> ReadReadyFilesAsync(string scopeKey,
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        BatchReads++;
        LastBatchIds = ids.ToArray();
        BatchSizes.Add(ids.Count);
        if (BeforeRead != null) await BeforeRead(ct);
        return await ((IFileReadBatchStore)Inner).ReadReadyFilesAsync(scopeKey, ids, ct);
    }
}
