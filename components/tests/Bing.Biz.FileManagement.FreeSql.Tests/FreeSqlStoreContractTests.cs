using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.FreeSql;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.FreeSql.Tests;

public sealed class FreeSqlStoreContractTests
{
    [Fact]
    public async Task CreateAndFind_AreAtomicAndRequestKeyIsIdempotent()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var entry = Entry("request-a");
        entry.Bindings.Add(Binding(entry.File.Id, "orders", "order-1"));
        var operation = Operation(entry.File.Id, FileOperationState.Pending, nextAttemptAt: 1);

        Assert.True(await fixture.Store.TryCreateAsync(entry, new[] { operation }));
        var byId = await fixture.Store.FindAsync(entry.File.Id);
        Assert.NotNull(byId);
        Assert.Equal(entry.File.RequestKey, byId.File.RequestKey);
        Assert.Equal(entry.Session!.Id, (await fixture.Store.FindSessionAsync(entry.Session.Id))!.Session!.Id);
        Assert.Equal(entry.File.Id, (await fixture.Store.FindRequestAsync(entry.File.RequestKey))!.File.Id);
        Assert.Single(byId.Bindings);

        var duplicateRequest = Entry(entry.File.RequestKey);
        Assert.False(await fixture.Store.TryCreateAsync(duplicateRequest));
        Assert.Null(await fixture.Store.FindAsync(duplicateRequest.File.Id));
        Assert.Single(await fixture.Store.ListDueOperationsAsync(10, 10));
    }

    [Fact]
    public async Task Update_CasReplacesBindingsUpsertsPartsAndRollsBackStaleChildren()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var entry = Entry("cas-a");
        entry.Bindings.Add(Binding(entry.File.Id, "orders", "old"));
        Assert.True(await fixture.Store.TryCreateAsync(entry));

        var first = (await fixture.Store.FindAsync(entry.File.Id))!;
        first.File.FileName = "version-2.txt";
        first.Bindings = new List<FileBinding> { Binding(entry.File.Id, "orders", "new") };
        var firstPart = Part(first.Session!.Id, 1, "etag-1");
        var firstOperation = Operation(entry.File.Id, FileOperationState.Pending, nextAttemptAt: 1);
        Assert.True(await fixture.Store.TryUpdateAsync(first, first.File.Version, new[] { firstPart }, new[] { firstOperation }));
        Assert.Equal(2, first.File.Version);
        Assert.Equal(2, first.Session.Version);

        var stale = (await fixture.Store.FindAsync(entry.File.Id))!;
        var current = (await fixture.Store.FindAsync(entry.File.Id))!;
        current.File.FileName = "version-3.txt";
        Assert.True(await fixture.Store.TryUpdateAsync(current, current.File.Version,
            new[] { Part(current.Session!.Id, 2, "etag-2") }));
        Assert.Equal(3, current.File.Version);

        stale.File.FileName = "stale.txt";
        Assert.False(await fixture.Store.TryUpdateAsync(stale, stale.File.Version,
            new[] { Part(stale.Session!.Id, 3, "etag-3") },
            new[] { Operation(entry.File.Id, FileOperationState.Pending, nextAttemptAt: 2) }));

        var saved = (await fixture.Store.FindAsync(entry.File.Id))!;
        Assert.Equal("version-3.txt", saved.File.FileName);
        Assert.Equal(3, saved.Session!.Version);
        Assert.Equal("new", Assert.Single(saved.Bindings).BusinessId);
        Assert.Equal(new[] { 1, 2 }, (await fixture.Store.ListPartsAsync(saved.Session.Id)).Select(part => part.Number));
        Assert.Single(await fixture.Store.ListDueOperationsAsync(10, 10));
    }

    [Fact]
    public async Task Lists_FilterScopeOwnerExpiryAndRetryability()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var first = Entry("list-a", "scope-a", "user-a", createdAt: 100,
            sessionState: FileState.Pending, expiresAt: 50, absoluteExpiresAt: 500);
        var second = Entry("list-b", "scope-a", "user-a", createdAt: 300,
            sessionState: FileState.Uploading, expiresAt: 200, absoluteExpiresAt: 60);
        var third = Entry("list-c", "scope-a", "user-b", createdAt: 200,
            sessionState: FileState.Ready, expiresAt: 1, absoluteExpiresAt: 1);
        var fourth = Entry("list-d", "scope-b", "user-a", createdAt: 400,
            sessionState: FileState.Uploading, expiresAt: 500, absoluteExpiresAt: 600);

        Assert.True(await fixture.Store.TryCreateAsync(first,
            new[] { Operation(first.File.Id, FileOperationState.Pending, nextAttemptAt: 40) }));
        Assert.True(await fixture.Store.TryCreateAsync(second,
            new[] { Operation(second.File.Id, FileOperationState.Running, nextAttemptAt: 0, leaseUntil: 60) }));
        Assert.True(await fixture.Store.TryCreateAsync(third,
            new[] { Operation(third.File.Id, FileOperationState.Failed, nextAttemptAt: 0) }));
        Assert.True(await fixture.Store.TryCreateAsync(fourth,
            new[] { Operation(fourth.File.Id, FileOperationState.Pending, nextAttemptAt: 200) }));

        var ownFiles = await fixture.Store.ListFilesAsync("scope-a", "user-a", 0, 10);
        Assert.Equal(new[] { second.File.Id, first.File.Id }, ownFiles.Select(file => file.Id));
        Assert.Equal(new[] { first.File.Id }, (await fixture.Store.ListFilesAsync("scope-a", "user-a", 1, 1))
            .Select(file => file.Id));
        Assert.Equal(3, (await fixture.Store.ListFilesAsync("scope-a", null, 0, 10)).Count);

        var expired = await fixture.Store.ListExpiredSessionsAsync(100, 10);
        Assert.Equal(new[] { first.Session!.Id, second.Session!.Id }.OrderBy(id => id), expired.OrderBy(id => id));
        var due = await fixture.Store.ListDueOperationsAsync(100, 10);
        Assert.Equal(new[] { first.File.Id, second.File.Id }.OrderBy(id => id), due.Select(operation => operation.FileId).OrderBy(id => id));
    }

    [Fact]
    public async Task LeaseClaimRenewFinish_UsesVersionAndCurrentToken()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var entry = Entry("lease-a");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var operation = Operation(entry.File.Id, FileOperationState.Pending, nextAttemptAt: now - 1);
        Assert.True(await fixture.Store.TryCreateAsync(entry, new[] { operation }));

        var candidate = Assert.Single(await fixture.Store.ListDueOperationsAsync(now, 10));
        Assert.True(await fixture.Store.TryClaimAsync(candidate.Id, candidate.Version, "worker-a", now, now + 10_000));
        Assert.False(await fixture.Store.TryClaimAsync(candidate.Id, candidate.Version, "worker-b", now, now + 10_000));
        Assert.Empty(await fixture.Store.ListDueOperationsAsync(now, 10));

        var expired = Assert.Single(await fixture.Store.ListDueOperationsAsync(now + 11_000, 10));
        Assert.Equal(1, expired.Attempts);
        Assert.Equal(candidate.Version + 1, expired.Version);
        Assert.True(await fixture.Store.TryClaimAsync(expired.Id, expired.Version, "worker-b", now + 11_000, now + 30_000));
        Assert.False(await fixture.Store.FinishOperationAsync(expired.Id, "worker-a", FileOperationState.Succeeded, 0, null));
        Assert.True(await fixture.Store.RenewLeaseAsync(expired.Id, "worker-b", now + 40_000));
        Assert.True(await fixture.Store.FinishOperationAsync(expired.Id, "worker-b", FileOperationState.Succeeded, 0, null));
        Assert.Empty(await fixture.Store.ListDueOperationsAsync(now + 60_000, 10));

        var saved = await fixture.Orm.Select<FileOperation>().Where(item => item.Id == operation.Id).FirstAsync();
        Assert.Equal(FileOperationState.Succeeded, saved.State);
        Assert.Null(saved.LeaseToken);
        Assert.Equal(0, saved.LeaseUntil);
        Assert.Equal(5, saved.Version);
    }

    [Fact]
    public async Task ImportUpsert_DoesNotRewriteImportedRecord()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var import = new LegacyFileImport
        {
            Id = Guid.NewGuid(), Source = "legacy", LegacyId = Guid.NewGuid(), FileId = Guid.NewGuid(),
            Status = "Preview", Issue = "needs review", UpdatedAt = 1
        };
        await fixture.Store.SaveImportAsync(import);
        import.Issue = "reviewed";
        import.UpdatedAt = 2;
        await fixture.Store.SaveImportAsync(import);
        Assert.Equal("reviewed", (await fixture.Store.FindImportAsync(import.Id))!.Issue);

        import.Status = "Imported";
        import.Issue = null;
        import.UpdatedAt = 3;
        await fixture.Store.SaveImportAsync(import);
        import.Status = "Failed";
        import.Issue = "late retry";
        import.UpdatedAt = 4;
        await fixture.Store.SaveImportAsync(import);

        var saved = (await fixture.Store.FindImportAsync(import.Id))!;
        Assert.Equal("Imported", saved.Status);
        Assert.Equal(3, saved.UpdatedAt);
        Assert.Null(saved.Issue);
    }

    [Fact]
    public async Task Registration_BorrowsClientAndDoesNotSynchronizeSchema()
    {
        var path = NewDatabasePath();
        var orm = CreateOrm(path);
        try
        {
            var factoryCalls = 0;
            var services = new ServiceCollection();
            services.AddBingFileManagementFreeSql(_ =>
            {
                factoryCalls++;
                return orm;
            });
            await using (var provider = services.BuildServiceProvider())
            {
                var store = provider.GetRequiredService<IFileManagementStore>();
                Assert.Same(store, provider.GetRequiredService<IFileManagementStore>());
                Assert.Equal(1, factoryCalls);
                var exception = await Assert.ThrowsAnyAsync<Exception>(() => store.FindAsync(Guid.NewGuid()));
                Assert.Contains("BingFileRecords", exception.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            FileManagementFreeSqlSchema.SyncStructure(orm);
            Assert.Null(await new FreeSqlFileManagementStore(orm).FindAsync(Guid.NewGuid()));
        }
        finally
        {
            orm.Dispose();
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task StoreTransaction_CommitsBeforeUnrelatedUnitOfWorkRollsBack()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var entry = Entry("ambient-a");
        Assert.True(await fixture.Store.TryCreateAsync(entry));

        using var unrelated = fixture.Orm.CreateUnitOfWork();
        unrelated.GetOrBeginTransaction();
        unrelated.Rollback();

        Assert.NotNull(await fixture.Store.FindAsync(entry.File.Id));
    }

    private static FileEntry Entry(string requestKey, string scope = "scope-a", string creator = "user-a",
        long createdAt = 100, FileState sessionState = FileState.Uploading, long expiresAt = 10_000,
        long absoluteExpiresAt = 20_000)
    {
        var fileId = Guid.NewGuid();
        return new FileEntry
        {
            File = new FileRecord
            {
                Id = fileId, RequestKey = requestKey, ScopeKey = scope, ScopeKind = Bing.FileStorage.StorageScopeKind.Host,
                StoreName = "local-v1", Container = "attachments", ObjectKey = fileId.ToString("N"),
                FileName = requestKey + ".txt", Length = 12, ContentType = "text/plain", Visibility = FileVisibility.Private,
                State = FileState.Uploading, CreatorId = creator, CreatedAt = createdAt, Version = 1
            },
            Session = new FileUploadSession
            {
                Id = Guid.NewGuid(), FileId = fileId, OwnerId = creator, TotalLength = 12, PartSize = 5,
                State = sessionState, WriteMode = Bing.FileStorage.FileWriteMode.Overwrite,
                ExpiresAt = expiresAt, AbsoluteExpiresAt = absoluteExpiresAt, LastActivityAt = createdAt,
                ClientLastModified = 0, Version = 1
            },
            Bindings = new List<FileBinding>()
        };
    }

    private static FileBinding Binding(Guid fileId, string type, string businessId) => new()
    {
        Id = Guid.NewGuid(), FileId = fileId, BusinessType = type, BusinessId = businessId,
        CreatorId = "user-a", CreatedAt = 100
    };

    private static FileUploadPart Part(Guid sessionId, int number, string etag) => new()
    {
        SessionId = sessionId, Number = number, Length = 5, Sha256 = Convert.ToBase64String(new byte[32]), ETag = etag
    };

    private static FileOperation Operation(Guid fileId, FileOperationState state, long nextAttemptAt, long leaseUntil = 0) => new()
    {
        Id = Guid.NewGuid(), FileId = fileId, Kind = FileOperationKind.Complete, State = state,
        Payload = "{}", Attempts = 0, NextAttemptAt = nextAttemptAt, LeaseUntil = leaseUntil, Version = 1
    };

    private static string NewDatabasePath() => Path.Combine(Path.GetTempPath(), $"bing-file-management-freesql-{Guid.NewGuid():N}.db");

    private static IFreeSql CreateOrm(string path) => new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, "Data Source=" + path)
        .UseAutoSyncStructure(false)
        .Build();

    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private sealed class StoreFixture : IAsyncDisposable
    {
        private readonly string _path;

        private StoreFixture(string path, IFreeSql orm)
        {
            _path = path;
            Orm = orm;
            Store = new FreeSqlFileManagementStore(orm);
        }

        internal IFreeSql Orm { get; }
        internal FreeSqlFileManagementStore Store { get; }

        internal static Task<StoreFixture> CreateAsync()
        {
            var path = NewDatabasePath();
            var orm = CreateOrm(path);
            FileManagementFreeSqlSchema.SyncStructure(orm);
            return Task.FromResult(new StoreFixture(path, orm));
        }

        public ValueTask DisposeAsync()
        {
            Orm.Dispose();
            DeleteDatabase(_path);
            return ValueTask.CompletedTask;
        }
    }
}
