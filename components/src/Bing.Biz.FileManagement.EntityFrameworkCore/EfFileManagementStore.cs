using Microsoft.EntityFrameworkCore;

namespace Bing.Biz.FileManagement.EntityFrameworkCore;

/// <summary>可移植 CAS 和短事务；只把唯一约束冲突与并发冲突转为 false。</summary>
public sealed partial class EfFileManagementStore : IFileManagementStore
{
    private readonly IDbContextFactory<FileManagementDbContext> _factory;
    public EfFileManagementStore(IDbContextFactory<FileManagementDbContext> factory) { _factory = factory; }
    public async Task<FileManagementStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new FileManagementStatistics
        {
            ActiveSessions = await db.Set<FileUploadSession>().LongCountAsync(x => x.State == FileState.Pending || x.State == FileState.Uploading || x.State == FileState.Completing || x.State == FileState.Quarantined, ct),
            PendingCleanup = await db.Set<FileOperation>().LongCountAsync(x => (x.State == FileOperationState.Pending || x.State == FileOperationState.Running) && (x.Kind == FileOperationKind.Abort || x.Kind == FileOperationKind.Delete || x.Kind == FileOperationKind.DeleteReplica), ct)
                + await db.Set<FileTransferAttempt>().LongCountAsync(x => x.State == FileTransferState.Retired || x.State == FileTransferState.Active && x.LeaseUntil <= now, ct),
            FailedOperations = await db.Set<FileOperation>().LongCountAsync(x => x.State == FileOperationState.Failed, ct)
        };
    }
    public async Task<FileEntry?> FindAsync(Guid fileId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var file = await db.Set<FileRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId, ct);
        if (file == null) return null;
        var result = new FileEntry { File = file,
            Session = await db.Set<FileUploadSession>().AsNoTracking().SingleOrDefaultAsync(x => x.FileId == fileId, ct),
            Bindings = await db.Set<FileBinding>().AsNoTracking().Where(x => x.FileId == fileId).ToListAsync(ct) };
        await transaction.CommitAsync(ct); return result;
    }
    public async Task<FileEntry?> FindSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var id = await db.Set<FileUploadSession>().Where(x => x.Id == sessionId).Select(x => (Guid?)x.FileId).SingleOrDefaultAsync(ct);
        return id.HasValue ? await FindAsync(id.Value, ct) : null;
    }
    public async Task<FileEntry?> FindRequestAsync(string requestKey, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var id = await db.Set<FileRecord>().Where(x => x.RequestKey == requestKey).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return id.HasValue ? await FindAsync(id.Value, ct) : null;
    }
    public async Task<bool> TryCreateAsync(FileEntry entry, IReadOnlyList<FileOperation>? operations = null, CancellationToken ct = default, FileTransferLease? transfer = null)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = entry.Admission == null
            ? await db.Database.BeginTransactionAsync(ct)
            : await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            if (await db.Set<FileRecord>().AnyAsync(x => x.Id == entry.File.Id || x.RequestKey == entry.File.RequestKey, ct)) return false;
            if (entry.Admission != null && !await TryReserveUploadAsync(db, entry, entry.Admission, ct)) return false;
            if (transfer != null && !await PublishTransferAsync(db, transfer, entry.File, ct)) return false;
            db.Add(entry.File);
            if (entry.Session != null) db.Add(entry.Session);
            db.AddRange(entry.Bindings);
            if (operations != null)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                foreach (var operation in operations) PrepareOperation(operation, now);
                db.AddRange(operations);
            }
            if (entry.File.State == FileState.Ready) db.Add(CreateReadyEvent(entry.File, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true;
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); return false; }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(ct);
            await using var probe = await _factory.CreateDbContextAsync(ct);
            if (await probe.Set<FileRecord>().AnyAsync(x => x.Id == entry.File.Id || x.RequestKey == entry.File.RequestKey, ct)) return false;
            if (IsUniqueConstraintViolation(exception)) return false;
            throw;
        }
    }
    public async Task<bool> TryUpdateAsync(FileEntry entry, long expectedVersion, IReadOnlyList<FileUploadPart>? suppliedParts = null,
        IReadOnlyList<FileOperation>? operations = null, CancellationToken ct = default, FileOperationLease? lease = null, FileTransferLease? transfer = null)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var original = entry.File.Version;
        try
        {
            var priorState = await db.Set<FileRecord>().AsNoTracking().Where(x => x.Id == entry.File.Id)
                .Select(x => (FileState?)x.State).SingleOrDefaultAsync(ct);
            if (lease != null)
            {
                var operation = await db.Set<FileOperation>().SingleOrDefaultAsync(x => x.Id == lease.OperationId, ct);
                if (operation == null || operation.FileId != entry.File.Id || operation.LeaseToken != lease.Token ||
                    operation.State != FileOperationState.Running || operation.LeaseUntil <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) return false;
                // 先以版本 CAS 锁定租约行，接管/续租不能穿过随后的业务提交。
                operation.Version++;
                await db.SaveChangesAsync(ct);
            }
            if (transfer != null && !await PublishTransferAsync(db, transfer, entry.File, ct)) return false;
            entry.File.Version = expectedVersion + 1;
            db.Update(entry.File);
            db.Entry(entry.File).Property(x => x.Version).OriginalValue = expectedVersion;
            await db.SaveChangesAsync(ct); // 先取得根记录 CAS，再写子表。
            if (entry.Session != null) { entry.Session.Version = entry.File.Version; db.Update(entry.Session); }
            if (entry.File.State == FileState.Ready)
                await db.Set<FileUploadReservation>().Where(x => x.FileId == entry.File.Id).LoadAsync(ct);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (operations != null)
                foreach (var operation in operations) PrepareOperation(operation, now);
            if (priorState.HasValue && CreateTransitionEvent(entry.File, priorState.Value, now) is { } transition)
                db.Add(transition);
            if (entry.File.State == FileState.Ready)
            {
                var reservations = db.ChangeTracker.Entries<FileUploadReservation>()
                    .Where(x => x.Entity.FileId == entry.File.Id).Select(x => x.Entity).ToArray();
                db.RemoveRange(reservations);
            }
            var bindings = await db.Set<FileBinding>().Where(x => x.FileId == entry.File.Id).ToListAsync(ct);
            var wanted = entry.Bindings.ToDictionary(x => x.Id);
            foreach (var old in bindings)
            {
                if (!wanted.Remove(old.Id, out var updated)) db.Remove(old);
                else db.Entry(old).CurrentValues.SetValues(updated);
            }
            db.AddRange(wanted.Values);
            if (suppliedParts?.Count > 0)
            {
                if (entry.Session == null || suppliedParts.Any(p => p.SessionId != entry.Session.Id)) throw new ArgumentException("分片会话不一致。");
                var numbers = suppliedParts.Select(p => p.Number).ToArray();
                var stored = await db.Set<FileUploadPart>().Where(x => x.SessionId == entry.Session.Id && Enumerable.Contains(numbers, x.Number)).ToDictionaryAsync(x => x.Number, ct);
                foreach (var part in suppliedParts)
                {
                    if (stored.TryGetValue(part.Number, out var old)) db.Entry(old).CurrentValues.SetValues(part);
                    else db.Add(part);
                }
            }
            if (operations != null) db.AddRange(operations);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true;
        }
        catch (DbUpdateConcurrencyException)
        { await transaction.RollbackAsync(ct); entry.File.Version = original; return false; }
        catch
        { await transaction.RollbackAsync(ct); entry.File.Version = original; throw; }
    }
    public async Task<IReadOnlyList<FileUploadPart>> ListPartsAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileUploadPart>().AsNoTracking().Where(x => x.SessionId == sessionId).OrderBy(x => x.Number).ToListAsync(ct);
    }
    public async Task<FileUploadPart?> FindPartAsync(Guid sessionId, int number, CancellationToken ct = default)
    {
        if (number <= 0) throw new ArgumentOutOfRangeException(nameof(number));
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileUploadPart>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionId == sessionId && x.Number == number, ct);
    }
    public async Task<IReadOnlyList<FileRecord>> ListFilesAsync(string scopeKey, string? creatorId, int skip, int take, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.Set<FileRecord>().AsNoTracking().Where(x => x.ScopeKey == scopeKey);
        if (creatorId != null) query = query.Where(x => x.CreatorId == creatorId);
        return await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<FileRecord>> ListReadyFilesAsync(string scopeKey, string? creatorId, int skip, int take, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scopeKey);
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take < 0) throw new ArgumentOutOfRangeException(nameof(take));
        ct.ThrowIfCancellationRequested();
        if (take == 0) return Array.Empty<FileRecord>();

        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.Set<FileRecord>().AsNoTracking()
            .Where(x => x.ScopeKey == scopeKey && x.State == FileState.Ready);
        if (creatorId != null) query = query.Where(x => x.CreatorId == creatorId);
        return await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip(skip).Take(take).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<Guid>> ListExpiredSessionsAsync(long now, int take, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileUploadSession>().Where(x => (x.State == FileState.Pending || x.State == FileState.Uploading) &&
            (x.ExpiresAt <= now || x.AbsoluteExpiresAt <= now)).OrderBy(x => x.ExpiresAt).Select(x => x.Id).Take(take).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<FileOperation>> ListDueOperationsAsync(long now, int take, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileOperation>().AsNoTracking().Where(x =>
            x.State == FileOperationState.Pending && x.NextAttemptAt <= now || x.State == FileOperationState.Running && x.LeaseUntil <= now)
            .OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Take(take).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<FileOperation>> ListDueOperationsAsync(long now, int take, FileOperationQueue queue,
        CancellationToken ct = default)
    {
        if (take < 0) throw new ArgumentOutOfRangeException(nameof(take));
        if (queue is not (FileOperationQueue.Short or FileOperationQueue.Long))
            throw new ArgumentOutOfRangeException(nameof(queue));
        ct.ThrowIfCancellationRequested();
        if (take == 0) return Array.Empty<FileOperation>();

        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.Set<FileOperation>().AsNoTracking().Where(x =>
            x.State == FileOperationState.Pending && x.NextAttemptAt <= now || x.State == FileOperationState.Running && x.LeaseUntil <= now);
        query = queue == FileOperationQueue.Long
            ? query.Where(x => x.Kind == FileOperationKind.Complete || x.Kind == FileOperationKind.Migrate || x.Kind == FileOperationKind.Inspect)
            : query.Where(x => x.Kind != FileOperationKind.Complete && x.Kind != FileOperationKind.Migrate && x.Kind != FileOperationKind.Inspect);
        return await query.OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Take(take).ToListAsync(ct);
    }
    public async Task<bool> TryClaimAsync(Guid operationId, long expectedVersion, string token, long now, long leaseUntil, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var operation = await db.Set<FileOperation>().SingleOrDefaultAsync(x => x.Id == operationId, ct);
        if (operation == null || operation.Version != expectedVersion ||
            !(operation.State == FileOperationState.Pending && operation.NextAttemptAt <= now || operation.State == FileOperationState.Running && operation.LeaseUntil <= now)) return false;
        operation.LeaseToken = token; operation.LeaseUntil = leaseUntil; operation.State = FileOperationState.Running; operation.Attempts++; operation.Version++;
        return await SaveCasAsync(db, ct);
    }
    public async Task<bool> RenewLeaseAsync(Guid operationId, string token, long leaseUntil, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var operation = await db.Set<FileOperation>().SingleOrDefaultAsync(x => x.Id == operationId, ct);
        if (operation == null || operation.LeaseToken != token || operation.State != FileOperationState.Running ||
            operation.LeaseUntil <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) return false;
        operation.LeaseUntil = leaseUntil; operation.Version++;
        return await SaveCasAsync(db, ct);
    }
    public async Task<bool> FinishOperationAsync(Guid operationId, string token, FileOperationState state, long nextAttemptAt, string? error, CancellationToken ct = default)
    {
        if (state == FileOperationState.Running) throw new ArgumentException("结束任务不能保持 Running。", nameof(state));
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var operation = await db.Set<FileOperation>().SingleOrDefaultAsync(x => x.Id == operationId, ct);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (operation == null || operation.LeaseToken != token || operation.State != FileOperationState.Running || operation.LeaseUntil <= now) return false;
        operation.State = state; operation.NextAttemptAt = nextAttemptAt; operation.LastError = error;
        operation.FinishedAt = state is FileOperationState.Succeeded or FileOperationState.Failed
            ? now : 0;
        operation.LeaseToken = null; operation.LeaseUntil = 0; operation.Version++;
        try
        {
            await db.SaveChangesAsync(ct); // 锁住任务行；下面任何失败均回滚这次任务终态。
            if (state == FileOperationState.Failed && operation.Kind is FileOperationKind.Initialize or FileOperationKind.Complete or FileOperationKind.Inspect)
            {
                var file = await db.Set<FileRecord>().SingleAsync(x => x.Id == operation.FileId, ct);
                var expectedState = operation.Kind switch
                {
                    FileOperationKind.Initialize => FileState.Pending,
                    FileOperationKind.Complete => FileState.Completing,
                    _ => FileState.Quarantined
                };
                if (file.State == expectedState)
                {
                    file.State = FileState.Failed; file.Version++;
                    var session = await db.Set<FileUploadSession>().SingleOrDefaultAsync(x => x.FileId == file.Id, ct);
                    if (session != null) { session.State = FileState.Failed; session.Version = file.Version; }
                    db.Add(new FileOperation { Id = Guid.NewGuid(), FileId = file.Id, Kind = FileOperationKind.Abort,
                        State = FileOperationState.Pending, NextAttemptAt = now, Version = 1, CreatedAt = now });
                }
            }
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true;
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); return false; }
    }
    private static async Task<bool> SaveCasAsync(DbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }
    public async Task<LegacyFileImport?> FindImportAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<LegacyFileImport>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }
    public async Task SaveImportAsync(LegacyFileImport import, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var old = await db.Set<LegacyFileImport>().SingleOrDefaultAsync(x => x.Id == import.Id, ct);
        if (old == null) db.Add(import);
        else if (old.Status != "Imported") db.Entry(old).CurrentValues.SetValues(import);
        await db.SaveChangesAsync(ct);
    }
}
