using System.Reflection;
using Bing.FileStorage;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.FileManagement.Tests;

public sealed class FileReadQueryTests
{
    private readonly ITestOutputHelper _output;
    public FileReadQueryTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task FiftyFiles_BatchAndLegacyFallbackReturnSamePage_WithThreeInsteadOf151Selects(bool freeSql, bool search)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>();
        for (var i = 1; i <= 50; i++) await FileListTestData.AddAsync(store, FileState.Ready, i, businessType: "orders");
        env.Authorizer.Granted = true;
        using var capture = new FileReadBatchSqlCapture(env.Provider, Path.Combine(env.Root, "files.db"));
        var legacy = DispatchProxy.Create<IFileManagementStore, StatusStoreProxy>();
        ((StatusStoreProxy)(object)legacy).Inner = store;
        var oldPage = await Query(Create(env, legacy), search);
        var oldSelects = capture.SelectStatements.Count;
        capture.Clear();
        var page = await Query(env.Get<FileManagementService>(), search);
        var newSelects = capture.SelectStatements.Count;
        Assert.Equal(oldPage.Select(file => file.Id), page.Select(file => file.Id));
        Assert.Equal(50, page.Count);
        Assert.Equal(151, oldSelects);
        Assert.Equal(3, newSelects);
        Assert.DoesNotContain(capture.SelectStatements, statement => statement.Contains("BingFileUploadSessions"));
        _output.WriteLine($"ORM={(freeSql ? "FreeSql" : "EF Core")}; query={(search ? "Search" : "List")}; files=50; legacySelects={oldSelects}; batchSelects={newSelects}");
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task CandidateDeletedBeforeBatch_IsExcludedWithoutChangingOtherResults(bool freeSql, bool search)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>();
        var stale = await FileListTestData.AddAsync(store, FileState.Ready, 2);
        var valid = await FileListTestData.AddAsync(store, FileState.Ready, 1);
        var wrapper = DispatchProxy.Create<IFileManagementStore, BatchQueryStoreProxy>();
        var probe = (BatchQueryStoreProxy)(object)wrapper; probe.Inner = store;
        probe.BeforeRead = async () =>
        {
            stale.File.State = FileState.DeletePending;
            Assert.True(await store.TryUpdateAsync(stale, stale.File.Version));
        };
        var page = await Query(Create(env, wrapper), search);
        Assert.Equal(valid.File.Id, Assert.Single(page).Id);
        Assert.Equal(1, probe.BatchReads);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BatchAuthorization_PreservesDeniedCursorProgressCancellationAndErrors(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql);
        await env.InitializeAsync();
        var store = env.Get<IFileManagementStore>();
        for (var i = 1; i <= 105; i++) await FileListTestData.AddAsync(store, FileState.Ready, i, businessType: "unknown");
        var valid = await FileListTestData.AddAsync(store, FileState.Ready, 0);
        var result = await env.Get<FileManagementService>().SearchAsync(new() { Take = 1 });
        Assert.Equal(valid.File.Id, Assert.Single(result.Items).Id);
        Assert.NotNull(result.NextPosition);
        Assert.Empty((await env.Get<FileManagementService>().SearchAsync(new() { Take = 1 }, result.NextPosition)).Items);
        using var stop = new CancellationTokenSource();
        var checks = 0;
        var authorizer = new FileListTestAuthorizer((_, ct) =>
        {
            Assert.Equal(stop.Token, ct);
            if (++checks == 101) stop.Cancel();
            return Task.FromResult(false);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(env, store, authorizer).SearchAsync(new(), ct: stop.Token));
        Assert.Equal(101, checks);
        var unavailable = new FileListTestAuthorizer((_, _) => throw new IOException("authorization unavailable"));
        await Assert.ThrowsAsync<IOException>(() => Create(env, store, unavailable).SearchAsync(new()));
    }

    private static async Task<IReadOnlyList<FileView>> Query(FileManagementService service, bool search)
        => search ? (await service.SearchAsync(new() { Take = 50 })).Items : await service.ListAsync();

    private static FileManagementService Create(FileTestEnvironment env, IFileManagementStore store, IFileAccessAuthorizer? authorizer = null)
        => new(store, env.Get<IFileStorageFactory>(), env.Identity, env.Identity,
            env.Get<IFilePermissionChecker>(), authorizer ?? env.Authorizer, env.Get<FileManagementOptions>());
}

public class BatchQueryStoreProxy : StatusStoreProxy, IFileReadBatchStore
{
    public Func<Task>? BeforeRead { get; set; }
    public int BatchReads { get; private set; }
    public async Task<IReadOnlyList<FileReadSnapshot>> ReadReadyFilesAsync(string scopeKey, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        BatchReads++;
        if (BeforeRead != null) await BeforeRead();
        return await ((IFileReadBatchStore)Inner).ReadReadyFilesAsync(scopeKey, ids, ct);
    }
}
