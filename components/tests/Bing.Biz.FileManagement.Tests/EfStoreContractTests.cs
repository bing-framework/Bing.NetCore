using Bing.Biz.FileManagement.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class EfStoreContractTests
{
    [Fact]
    public async Task VersionConflict_RollsBackPartsAndOperations()
    {
        await using var env = new FileTestEnvironment(); await env.InitializeAsync();
        var session = await env.Get<FileManagementService>().CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "cas" });
        var store = env.Get<IFileManagementStore>();
        var winner = (await store.FindSessionAsync(session.Id))!; var loser = (await store.FindSessionAsync(session.Id))!;
        winner.File.FileName = "winner";
        Assert.True(await store.TryUpdateAsync(winner, winner.File.Version));
        loser.File.FileName = "loser";
        Assert.False(await store.TryUpdateAsync(loser, loser.File.Version,
            new[] { new FileUploadPart { SessionId = session.Id, Number = 1, Sha256 = "test" } },
            new[] { new FileOperation { Id = Guid.NewGuid(), FileId = loser.File.Id, Kind = FileOperationKind.Delete, Version = 1 } }));
        Assert.Equal("winner", (await store.FindAsync(winner.File.Id))!.File.FileName);
        Assert.Empty(await store.ListPartsAsync(session.Id));
        Assert.Single(await store.ListDueOperationsAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 20));
    }
    [Fact]
    public async Task Lease_OneWinner_ExpiredTakeover_AndOldTokenCannotFinish()
    {
        await using var env = new FileTestEnvironment(); await env.InitializeAsync();
        await env.Get<FileManagementService>().CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "lease" });
        var store = env.Get<IFileManagementStore>(); var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var op = Assert.Single(await store.ListDueOperationsAsync(now, 10));
        Assert.True(await store.TryClaimAsync(op.Id, op.Version, "first", now, now + 10000));
        Assert.False(await store.TryClaimAsync(op.Id, op.Version, "second", now, now + 10000));
        Assert.Empty(await store.ListDueOperationsAsync(now, 10));
        op = Assert.Single(await store.ListDueOperationsAsync(now + 11000, 10));
        Assert.True(await store.TryClaimAsync(op.Id, op.Version, "second", now + 11000, now + 30000));
        Assert.False(await store.FinishOperationAsync(op.Id, "first", FileOperationState.Succeeded, 0, null));
        Assert.True(await store.RenewLeaseAsync(op.Id, "second", now + 40000));
        Assert.True(await store.FinishOperationAsync(op.Id, "second", FileOperationState.Succeeded, 0, null));
        Assert.Empty(await store.ListDueOperationsAsync(now + 60000, 10));
    }
    [Fact]
    public async Task DuplicateRequest_DoesNotDuplicateChildren()
    {
        await using var env = new FileTestEnvironment(); await env.InitializeAsync();
        var service = env.Get<FileManagementService>();
        var request = new CreateFileUpload { RequestId = Guid.NewGuid(), FileName = "same" };
        var first = await service.CreateUploadAsync(request);
        Assert.Equal(first.Id, (await service.CreateUploadAsync(request)).Id);
        await using var db = await env.Get<IDbContextFactory<FileManagementDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await db.Set<FileRecord>().CountAsync());
        Assert.Equal(1, await db.Set<FileUploadSession>().CountAsync());
        Assert.Equal(1, await db.Set<FileOperation>().CountAsync());
    }
}
