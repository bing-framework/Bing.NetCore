using Bing.Biz.FileManagement.EntityFrameworkCore;
using FreeSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class FileListStoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListReadyFilesAsync_FiltersBeforePagingAndPreservesStoreContracts(bool freeSql)
    {
        await using var fixture = await FileManagementStoreTestEnvironment.CreateAsync(freeSql);
        var rows = CreateRows();
        await SeedAsync(fixture, rows);

        const string scopeKey = "scope-ready-list";
        var firstPage = await fixture.Store.ListReadyFilesAsync(scopeKey, "creator-a", 0, 2);
        Assert.Equal(new[] { Id(4), Id(1) }, firstPage.Select(file => file.Id));

        var secondPage = await fixture.Store.ListReadyFilesAsync(scopeKey, "creator-a", 2, 2);
        Assert.Equal(new[] { Id(2), Id(3) }, secondPage.Select(file => file.Id));
        Assert.Empty(firstPage.Select(file => file.Id).Intersect(secondPage.Select(file => file.Id)));

        var creatorResults = await fixture.Store.ListReadyFilesAsync(scopeKey, "creator-a", 0, 100);
        Assert.Equal(4, creatorResults.Count);
        Assert.All(creatorResults, file => Assert.Equal("creator-a", file.CreatorId));

        var managementResults = await fixture.Store.ListReadyFilesAsync(scopeKey, null, 0, 100);
        Assert.Equal(5, managementResults.Count);
        Assert.Contains(managementResults, file => file.Id == Id(2000));
        Assert.DoesNotContain(managementResults, file => file.Id == Id(3000));
        Assert.Empty(await fixture.Store.ListReadyFilesAsync("missing-scope", null, 0, 10));
        Assert.Equal(Id(3000), Assert.Single(await fixture.Store.ListReadyFilesAsync("scope-other", null, 0, 10)).Id);

        var snapshot = await fixture.Store.ListReadyFilesAsync(scopeKey, "creator-a", 0, 1);
        snapshot[0].FileName = "mutated-snapshot";
        Assert.NotEqual("mutated-snapshot",
            (await fixture.Store.ListReadyFilesAsync(scopeKey, "creator-a", 0, 1))[0].FileName);

        var emptyPage = await fixture.Store.ListReadyFilesAsync(scopeKey, null, 0, 0);
        Assert.Empty(emptyPage);
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await fixture.Store.ListReadyFilesAsync(null!, null, 0, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await fixture.Store.ListReadyFilesAsync(scopeKey, null, -1, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await fixture.Store.ListReadyFilesAsync(scopeKey, null, 0, -1));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await fixture.Store.ListReadyFilesAsync(scopeKey, null, 0, 1, cancellation.Token));

        var allStates = await fixture.Store.ListFilesAsync(scopeKey, null, 0, 100);
        Assert.Equal(69, allStates.Count);
        Assert.Equal(64, allStates.Count(file => file.State != FileState.Ready));
        foreach (var state in Enum.GetValues<FileState>().Where(state => state != FileState.Ready))
            Assert.Equal(8, allStates.Count(file => file.State == state));
    }

    private static async Task SeedAsync(FileManagementStoreTestEnvironment fixture, IReadOnlyCollection<FileRecord> rows)
    {
        if (fixture.Orm is { } orm)
        {
            foreach (var batch in rows.Chunk(100))
                await orm.Insert(batch).ExecuteAffrowsAsync();
            return;
        }

        await using var db = await fixture.Provider.GetRequiredService<IDbContextFactory<FileManagementDbContext>>()
            .CreateDbContextAsync();
        db.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static IReadOnlyCollection<FileRecord> CreateRows()
    {
        var nonReadyStates = new[]
        {
            FileState.Pending,
            FileState.Uploading,
            FileState.Completing,
            FileState.DeletePending,
            FileState.Deleted,
            FileState.Aborted,
            FileState.Failed,
            FileState.Quarantined
        };
        var rows = Enumerable.Range(0, 64)
            .Select(index => Record(Id(100 + index), "scope-ready-list", "creator-a",
                nonReadyStates[index % nonReadyStates.Length], 1_000 - index))
            .ToList();

        rows.Add(Record(Id(4), "scope-ready-list", "creator-a", FileState.Ready, 101));
        rows.Add(Record(Id(1), "scope-ready-list", "creator-a", FileState.Ready, 100));
        rows.Add(Record(Id(2), "scope-ready-list", "creator-a", FileState.Ready, 100));
        rows.Add(Record(Id(3), "scope-ready-list", "creator-a", FileState.Ready, 100));
        rows.Add(Record(Id(2000), "scope-ready-list", "creator-b", FileState.Ready, 99));
        rows.Add(Record(Id(3000), "scope-other", "creator-a", FileState.Ready, 200));
        return rows;
    }

    private static FileRecord Record(Guid id, string scopeKey, string creatorId, FileState state, long createdAt) => new()
    {
        Id = id,
        RequestKey = "list-" + id.ToString("N"),
        ScopeKey = scopeKey,
        ScopeKind = Bing.FileStorage.StorageScopeKind.Host,
        StoreName = "local-v1",
        Container = "attachments",
        ObjectKey = id.ToString("N"),
        FileName = id.ToString("N") + ".txt",
        Length = 1,
        ContentType = "text/plain",
        Visibility = FileVisibility.Private,
        State = state,
        CreatorId = creatorId,
        CreatedAt = createdAt,
        Version = 1
    };

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:x12}");
}
