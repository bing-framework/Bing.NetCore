using Microsoft.EntityFrameworkCore;

namespace Bing.Biz.FileManagement.EntityFrameworkCore;

public sealed partial class EfFileManagementStore
{
    private static async Task<bool> PublishTransferAsync(FileManagementDbContext db, FileTransferLease lease, FileRecord file, CancellationToken ct)
    {
        var attempt = await db.Set<FileTransferAttempt>().SingleOrDefaultAsync(x => x.Id == lease.Id, ct);
        if (attempt == null || attempt.State != FileTransferState.Active || !attempt.WriterCompleted || attempt.LeaseToken != lease.Token ||
            attempt.LeaseUntil <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() || attempt.FileId != file.Id || attempt.ScopeKey != file.ScopeKey ||
            attempt.StoreName != file.StoreName || attempt.Container != file.Container || attempt.ObjectKey != file.ObjectKey) return false;
        attempt.State = FileTransferState.Published; attempt.Version++;
        await db.SaveChangesAsync(ct); // 与根记录提交同一事务；清理不能退休已发布的目标。
        return true;
    }
    public async Task CreateTransferAsync(FileTransferAttempt attempt, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Add(attempt); await db.SaveChangesAsync(ct);
    }
    public async Task<bool> RenewTransferAsync(FileTransferLease lease, long leaseUntil, CancellationToken ct = default)
    {
        // 写入确认也推进 Version；同一持有者的正常写回不能直接被当作失租。
        for (var retry = 0; retry < 3; retry++)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var attempt = await db.Set<FileTransferAttempt>().SingleOrDefaultAsync(x => x.Id == lease.Id, ct);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (attempt == null || attempt.LeaseToken != lease.Token || attempt.State != FileTransferState.Active || attempt.LeaseUntil <= now || leaseUntil <= now) return false;
            attempt.LeaseUntil = attempt.NextCheckAt = Math.Max(attempt.LeaseUntil, leaseUntil); attempt.Version++;
            if (await SaveCasAsync(db, ct)) return true;
        }
        return false;
    }
    public async Task MarkTransferWrittenAsync(FileTransferLease lease, CancellationToken ct = default)
    {
        // 与续租的短 CAS 竞争可以重读，不能把已确认写入误记为未知。
        for (var i = 0; i < 8; i++)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var attempt = await db.Set<FileTransferAttempt>().SingleAsync(x => x.Id == lease.Id, ct);
            if (attempt.LeaseToken != lease.Token) throw new InvalidOperationException("复制所有者不匹配。");
            if (attempt.WriterCompleted) return;
            attempt.WriterCompleted = true; attempt.Version++;
            if (attempt.State == FileTransferState.Retired) attempt.NextCheckAt = 0;
            if (await SaveCasAsync(db, ct)) return;
        }
        throw new FileManagementException("conflict", "复制写入确认竞争。");
    }
    public async Task<IReadOnlyList<FileTransferAttempt>> ListTransfersToCleanAsync(long now, int take, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileTransferAttempt>().AsNoTracking().Where(x => x.NextCheckAt <= now &&
            (x.State == FileTransferState.Retired || x.State == FileTransferState.Active && x.LeaseUntil <= now))
            .OrderBy(x => x.NextCheckAt).ThenBy(x => x.Id).Take(take).ToListAsync(ct);
    }
    public async Task<FileTransferAttempt?> RetireTransferAsync(Guid id, string? token, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var attempt = await db.Set<FileTransferAttempt>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (attempt == null || attempt.State is FileTransferState.Published or FileTransferState.Cleaned) return null;
        if (attempt.State == FileTransferState.Retired) return attempt;
        if (token != null ? attempt.LeaseToken != token : attempt.LeaseUntil > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) return null;
        attempt.State = FileTransferState.Retired; attempt.NextCheckAt = 0; attempt.Version++;
        return await SaveCasAsync(db, ct) ? attempt : null;
    }
    public async Task<bool> FinishTransferCleanupAsync(Guid id, long expectedVersion, long nextCheckAt, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var attempt = await db.Set<FileTransferAttempt>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (attempt?.State != FileTransferState.Retired || attempt.Version != expectedVersion) return false;
        if (attempt.WriterCompleted) attempt.State = FileTransferState.Cleaned;
        attempt.NextCheckAt = nextCheckAt; attempt.Version++;
        return await SaveCasAsync(db, ct);
    }
}
