using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Bing.Biz.FileManagement.EntityFrameworkCore;

public sealed partial class EfFileManagementStore
{
    private const int MaximumStorePageSize = 1000;

    public async Task<IReadOnlyList<FileRecord>> QueryFilesAsync(FileCandidateQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.ScopeKey) || query.Take is < 1 or > 100 ||
            query.CreatedFrom > query.CreatedTo || (query.AfterCreatedAt.HasValue != (query.AfterKey != null)))
            throw new ArgumentException("文件查询条件无效。", nameof(query));

        await using var db = await _factory.CreateDbContextAsync(ct);
        var records = db.Set<FileRecord>().AsNoTracking().Where(x => x.ScopeKey == query.ScopeKey && x.State == FileState.Ready);
        if (query.CreatorId != null) records = records.Where(x => x.CreatorId == query.CreatorId);
        if (query.Container != null) records = records.Where(x => x.Container == query.Container);
        if (query.Name != null) records = records.Where(x => x.FileName.Contains(query.Name));
        if (query.ContentType != null) records = records.Where(x => x.ContentType.Contains(query.ContentType));
        if (query.StoreName != null) records = records.Where(x => x.StoreName == query.StoreName);
        if (query.CreatedFrom.HasValue) records = records.Where(x => x.CreatedAt >= query.CreatedFrom.Value);
        if (query.CreatedTo.HasValue) records = records.Where(x => x.CreatedAt <= query.CreatedTo.Value);
        if (query.BusinessType != null || query.BusinessId != null)
        {
            var businessType = query.BusinessType;
            var businessId = query.BusinessId;
            records = records.Where(file => db.Set<FileBinding>().Any(binding => binding.FileId == file.Id &&
                (businessType == null || binding.BusinessType == businessType) &&
                (businessId == null || binding.BusinessId == businessId)));
        }
        if (query.AfterCreatedAt.HasValue)
        {
            var afterCreatedAt = query.AfterCreatedAt.Value;
            var afterKey = query.AfterKey!;
            records = records.Where(x => x.CreatedAt < afterCreatedAt ||
                x.CreatedAt == afterCreatedAt && string.Compare(x.RequestKey, afterKey) > 0);
        }
        return await records.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.RequestKey)
            .Take(query.Take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UploadSessionSummary>> QuerySessionsAsync(string scopeKey, string ownerId,
        FileState? state, bool includeExpired, long now, int skip, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("所有者不能为空。", nameof(ownerId));
        ValidatePage(skip, take, 100);
        if (take == 0) return Array.Empty<UploadSessionSummary>();

        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = from session in db.Set<FileUploadSession>().AsNoTracking()
                    join file in db.Set<FileRecord>().AsNoTracking() on session.FileId equals file.Id
                    where file.ScopeKey == scopeKey && session.OwnerId == ownerId
                    select new { session, file };
        if (state.HasValue) query = query.Where(x => x.session.State == state.Value);
        else query = query.Where(x => x.session.State == FileState.Pending || x.session.State == FileState.Uploading ||
                                      x.session.State == FileState.Completing || x.session.State == FileState.Quarantined);
        if (!includeExpired) query = query.Where(x =>
            (x.session.State != FileState.Pending && x.session.State != FileState.Uploading) ||
            x.session.ExpiresAt > now && x.session.AbsoluteExpiresAt > now);
        return await query.OrderByDescending(x => x.file.CreatedAt).ThenBy(x => x.session.Id)
            .Skip(skip).Take(take)
            .Select(x => new UploadSessionSummary
            {
                Id = x.session.Id,
                FileId = x.file.Id,
                FileName = x.file.FileName,
                Container = x.file.Container,
                ContentType = x.file.ContentType,
                Length = x.file.Length,
                LastModified = x.session.ClientLastModified,
                ExpiresAt = x.session.ExpiresAt,
                State = x.session.State
            }).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FileOperation>> QueryOperationsAsync(FileOperationQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.ScopeKey) || query.Skip < 0 || query.Take is < 1 or > 100 ||
            query.CreatedFrom > query.CreatedTo)
            throw new ArgumentException("任务查询条件无效。", nameof(query));

        await using var db = await _factory.CreateDbContextAsync(ct);
        var operations = db.Set<FileOperation>().AsNoTracking().Where(operation =>
            db.Set<FileRecord>().Any(file => file.Id == operation.FileId && file.ScopeKey == query.ScopeKey) &&
            !db.Set<FileHistoryArchive>().Any(archive => archive.ScopeKey == query.ScopeKey &&
                archive.Kind == FileHistoryKind.Operation && archive.EntityId == operation.Id && archive.SourceVersion == operation.Version));
        if (query.FileId.HasValue) operations = operations.Where(x => x.FileId == query.FileId.Value);
        if (query.Kind.HasValue) operations = operations.Where(x => x.Kind == query.Kind.Value);
        if (query.State.HasValue) operations = operations.Where(x => x.State == query.State.Value);
        if (query.CreatedFrom.HasValue) operations = operations.Where(x => x.CreatedAt >= query.CreatedFrom.Value);
        if (query.CreatedTo.HasValue) operations = operations.Where(x => x.CreatedAt <= query.CreatedTo.Value);
        return await operations.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip(query.Skip).Take(query.Take).ToListAsync(ct);
    }

    public async Task<FileOperation?> FindOperationAsync(Guid id, string? scopeKey = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.Set<FileOperation>().AsNoTracking().Where(x => x.Id == id);
        if (scopeKey != null)
            query = query.Where(operation => db.Set<FileRecord>().Any(file =>
                file.Id == operation.FileId && file.ScopeKey == scopeKey));
        return await query.SingleOrDefaultAsync(ct);
    }

    public async Task<FileOperationAudit?> FindRecoveryAsync(string requestKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestKey)) throw new ArgumentException("请求键不能为空。", nameof(requestKey));
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileOperationAudit>().AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == requestKey, ct);
    }

    public async Task<IReadOnlyList<FileOperationAudit>> ListOperationAuditAsync(string scopeKey, Guid operationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileOperationAudit>().AsNoTracking()
            .Where(x => x.ScopeKey == scopeKey && x.OperationId == operationId)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public async Task<bool> TryRecoverOperationAsync(FileOperation original, long fileVersion,
        FileOperation replacement, FileOperationAudit audit, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(audit);
        if (fileVersion < 0 || original.State != FileOperationState.Failed || original.RecoveryOperationId.HasValue ||
            original.FileId != replacement.FileId || replacement.ParentOperationId != original.Id ||
            audit.OperationId != original.Id || audit.ReplacementId != replacement.Id || audit.FileId != original.FileId)
            return false;

        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var operation = await db.Set<FileOperation>().SingleOrDefaultAsync(x => x.Id == original.Id, ct);
            var file = await db.Set<FileRecord>().SingleOrDefaultAsync(x => x.Id == original.FileId, ct);
            if (operation == null || file == null || operation.State != FileOperationState.Failed ||
                operation.Version != original.Version || operation.RecoveryOperationId.HasValue ||
                file.Version != fileVersion || file.ScopeKey != audit.ScopeKey)
                return false;

            // 事务内短暂递增再复原，获得跨数据库可识别的版本 CAS 和行锁，提交后的文件版本不变。
            var fileEntry = db.Entry(file);
            fileEntry.Property(x => x.Version).OriginalValue = fileVersion;
            file.Version = checked(fileVersion + 1);
            fileEntry.Property(x => x.Version).IsModified = true;
            await db.SaveChangesAsync(ct);
            file.Version = fileVersion;
            fileEntry.Property(x => x.Version).OriginalValue = checked(fileVersion + 1);
            fileEntry.Property(x => x.Version).IsModified = true;
            await db.SaveChangesAsync(ct);
            operation.RecoveryOperationId = replacement.Id;
            operation.Version++;

            var linkedItems = await (from item in db.Set<FileMigrationBatchItem>()
                                     join batch in db.Set<FileMigrationBatch>() on item.BatchId equals batch.Id
                                     where item.OperationId == original.Id &&
                                           (item.State == FileMigrationItemState.Failed || item.State == FileMigrationItemState.Queued) &&
                                           batch.ScopeKey == audit.ScopeKey
                                     select item).ToListAsync(ct);
            foreach (var item in linkedItems)
            {
                item.OperationId = replacement.Id;
                item.State = FileMigrationItemState.Queued;
                item.ErrorCode = null;
                item.Version = checked(item.Version + 1);
            }
            var batchIds = linkedItems.Select(x => x.BatchId).Distinct().ToArray();
            if (batchIds.Length != 0)
            {
                var completedBatches = await db.Set<FileMigrationBatch>()
                    .Where(x => Enumerable.Contains(batchIds, x.Id) && x.ScopeKey == audit.ScopeKey &&
                                x.State == FileMigrationBatchState.Completed).ToListAsync(ct);
                foreach (var batch in completedBatches)
                {
                    batch.State = FileMigrationBatchState.Running;
                    batch.Version = checked(batch.Version + 1);
                }
            }
            db.Add(replacement);
            db.Add(audit);
            PrepareOperation(replacement, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(ct);
            return false;
        }
    }

    public async Task<FileMigrationBatch?> FindBatchAsync(Guid id, string? scopeKey = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.Set<FileMigrationBatch>().AsNoTracking().Where(x => x.Id == id);
        if (scopeKey != null) query = query.Where(x => x.ScopeKey == scopeKey);
        return await query.SingleOrDefaultAsync(ct);
    }

    public async Task<FileMigrationBatch?> FindBatchRequestAsync(string requestKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestKey)) throw new ArgumentException("请求键不能为空。", nameof(requestKey));
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileMigrationBatch>().AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == requestKey, ct);
    }

    public async Task<bool> TryCreateBatchAsync(FileMigrationBatch batch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.Add(batch);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(ct);
            return false;
        }
    }

    public async Task<IReadOnlyList<FileMigrationBatch>> ListActiveBatchesAsync(int take, CancellationToken ct = default)
    {
        ValidateTake(take, MaximumStorePageSize);
        if (take == 0) return Array.Empty<FileMigrationBatch>();
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileMigrationBatch>().AsNoTracking().Where(x => x.State == FileMigrationBatchState.Running)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FileMigrationBatchItem>> ListBatchItemsToReconcileAsync(int take, CancellationToken ct = default)
    {
        ValidateTake(take, MaximumStorePageSize);
        ct.ThrowIfCancellationRequested();
        if (take == 0) return Array.Empty<FileMigrationBatchItem>();
        await using var db = await _factory.CreateDbContextAsync(ct);
        var items = db.Set<FileMigrationBatchItem>().AsNoTracking();
        var batches = db.Set<FileMigrationBatch>().AsNoTracking();
        var operations = db.Set<FileOperation>().AsNoTracking();
        var query = from item in items
                    join batch in batches on item.BatchId equals batch.Id
                    where item.State == FileMigrationItemState.Queued &&
                          (batch.State == FileMigrationBatchState.Running || batch.State == FileMigrationBatchState.Paused) &&
                          operations.Any(operation => item.OperationId.HasValue && operation.Id == item.OperationId.Value &&
                              operation.FileId == item.FileId &&
                              (operation.State == FileOperationState.Succeeded || operation.State == FileOperationState.Failed))
                    orderby batch.CreatedAt, batch.Id, item.FileId
                    select item;
        return await query.Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FileMigrationBatch>> ListBatchesAsync(string scopeKey, int skip, int take,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        ValidatePage(skip, take, MaximumStorePageSize);
        if (take == 0) return Array.Empty<FileMigrationBatch>();
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileMigrationBatch>().AsNoTracking().Where(x => x.ScopeKey == scopeKey &&
                !db.Set<FileHistoryArchive>().Any(archive => archive.ScopeKey == scopeKey &&
                    archive.Kind == FileHistoryKind.MigrationBatch && archive.EntityId == x.Id && archive.SourceVersion == x.Version))
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<bool> TryUpdateBatchAsync(FileMigrationBatch batch, long expectedVersion,
        IReadOnlyList<FileMigrationBatchItem>? additions = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (expectedVersion < 0 || additions?.Any(x => x.BatchId != batch.Id) == true)
            throw new ArgumentException("批次版本或新增条目无效。", nameof(batch));
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var originalVersion = batch.Version;
        try
        {
            batch.Version = checked(expectedVersion + 1);
            db.Update(batch);
            db.Entry(batch).Property(x => x.Version).OriginalValue = expectedVersion;
            if (additions != null) db.AddRange(additions);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            batch.Version = originalVersion;
            return false;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(ct);
            batch.Version = originalVersion;
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            batch.Version = originalVersion;
            throw;
        }
    }

    public async Task<IReadOnlyList<FileMigrationBatchItem>> ListBatchItemsAsync(Guid batchId,
        FileMigrationItemState? state, int skip, int take, CancellationToken ct = default)
    {
        ValidatePage(skip, take, MaximumStorePageSize);
        if (take == 0) return Array.Empty<FileMigrationBatchItem>();
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.Set<FileMigrationBatchItem>().AsNoTracking().Where(x => x.BatchId == batchId);
        if (state.HasValue) query = query.Where(x => x.State == state.Value);
        return await query.OrderBy(x => x.FileId).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<FileMigrationBatchView> GetBatchProgressAsync(Guid batchId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var batch = await db.Set<FileMigrationBatch>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == batchId, ct)
            ?? throw new FileManagementException("not_found", "迁移批次不存在。");
        var items = db.Set<FileMigrationBatchItem>().AsNoTracking().Where(x => x.BatchId == batchId);
        return new FileMigrationBatchView
        {
            Batch = batch,
            Total = await items.LongCountAsync(ct),
            Bytes = await items.SumAsync(x => x.Length, ct),
            Pending = await items.LongCountAsync(x => x.State == FileMigrationItemState.Pending, ct),
            Queued = await items.LongCountAsync(x => x.State == FileMigrationItemState.Queued, ct),
            Succeeded = await items.LongCountAsync(x => x.State == FileMigrationItemState.Succeeded, ct),
            Failed = await items.LongCountAsync(x => x.State == FileMigrationItemState.Failed, ct),
            Conflicts = await items.LongCountAsync(x => x.State == FileMigrationItemState.Conflict, ct)
        };
    }

    public async Task<bool> TryQueueBatchItemAsync(FileMigrationBatch batch, FileMigrationBatchItem item,
        FileOperation operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(operation);
        if (item.BatchId != batch.Id || operation.FileId != item.FileId || operation.Kind != FileOperationKind.Migrate ||
            operation.Id == Guid.Empty || operation.Payload == null || !HasExpectedMigrationVersion(operation.Payload, item.SourceVersion + 1))
            return false;

        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var originalBatchVersion = batch.Version;
        var originalItemVersion = item.Version;
        try
        {
            var storedBatch = await db.Set<FileMigrationBatch>().SingleOrDefaultAsync(x =>
                x.Id == batch.Id && x.ScopeKey == batch.ScopeKey && x.State == FileMigrationBatchState.Running && x.Version == batch.Version, ct);
            if (storedBatch == null) return false;
            storedBatch.Version++;
            var storedFile = await db.Set<FileRecord>().SingleOrDefaultAsync(x => x.Id == item.FileId &&
                x.ScopeKey == batch.ScopeKey && x.State == FileState.Ready && x.StoreName == batch.SourceStore &&
                x.Version == item.SourceVersion, ct);
            var storedItem = await db.Set<FileMigrationBatchItem>().SingleOrDefaultAsync(x =>
                x.BatchId == batch.Id && x.FileId == item.FileId && x.State == FileMigrationItemState.Pending &&
                x.SourceVersion == item.SourceVersion && x.Version == item.Version, ct);
            if (storedFile == null || storedItem == null) return false;
            storedFile.Version++;
            storedItem.State = FileMigrationItemState.Queued;
            storedItem.OperationId = operation.Id;
            storedItem.Version++;
            PrepareOperation(operation, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            db.Add(operation);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            batch.Version = storedBatch.Version;
            item.Version = storedItem.Version;
            item.State = storedItem.State;
            item.OperationId = operation.Id;
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            batch.Version = originalBatchVersion;
            item.Version = originalItemVersion;
            return false;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(ct);
            batch.Version = originalBatchVersion;
            item.Version = originalItemVersion;
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            batch.Version = originalBatchVersion;
            item.Version = originalItemVersion;
            throw;
        }
    }

    public async Task<bool> TryUpdateBatchItemAsync(FileMigrationBatchItem item, long expectedVersion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (expectedVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        var originalVersion = item.Version;
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            item.Version = checked(expectedVersion + 1);
            db.Update(item);
            db.Entry(item).Property(x => x.Version).OriginalValue = expectedVersion;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            item.Version = originalVersion;
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            item.Version = originalVersion;
            throw;
        }
    }

    public async Task<bool> TryAcquireIoLeaseAsync(FileIoLease lease, int limit, long now, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ValidateLease(lease.Id, lease.PoolKey, lease.Token, lease.ExpiresAt, limit, now);
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var lockId = ControlLockId("io", lease.PoolKey);
            if (!await TryBumpControlLockAsync(db, lockId, ct)) return false;
            // 回收到期槽时删除旧令牌；与续租并发时使用 ExpiresAt CAS，旧持有者不能重新占回已释放容量。
            var expired = await db.Set<FileIoLease>().Where(x => x.PoolKey == lease.PoolKey && x.ExpiresAt <= now).ToListAsync(ct);
            db.RemoveRange(expired);
            await db.SaveChangesAsync(ct);
            var active = await db.Set<FileIoLease>().LongCountAsync(x => x.PoolKey == lease.PoolKey && x.ExpiresAt > now, ct);
            if (active >= limit) return false;
            db.Add(lease);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(ct);
            return false;
        }
    }

    public async Task<bool> RenewIoLeaseAsync(Guid id, string token, long expiresAt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (expiresAt <= now) return false;
        await using var db = await _factory.CreateDbContextAsync(ct);
        var lease = await db.Set<FileIoLease>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (lease == null || lease.Token != token || lease.ExpiresAt <= now) return false;
        lease.ExpiresAt = expiresAt;
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task ReleaseIoLeaseAsync(Guid id, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        await using var db = await _factory.CreateDbContextAsync(ct);
        var leases = await db.Set<FileIoLease>().Where(x => x.Id == id && x.Token == token).ToListAsync(ct);
        db.RemoveRange(leases);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReleaseUploadReservationAsync(Guid fileId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var reservations = await db.Set<FileUploadReservation>().Where(x => x.FileId == fileId).ToListAsync(ct);
        db.RemoveRange(reservations);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FileOutboxEvent>> ListDueEventsAsync(long now, int take, CancellationToken ct = default)
    {
        ValidateTake(take, MaximumStorePageSize);
        if (take == 0) return Array.Empty<FileOutboxEvent>();
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileOutboxEvent>().AsNoTracking().Where(x => !x.Delivered && x.NextAttemptAt <= now &&
                (x.LeaseToken == null || x.LeaseUntil <= now))
            .OrderBy(x => x.NextAttemptAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(take).ToListAsync(ct);
    }

    public async Task<bool> TryClaimEventAsync(Guid id, long version, string token, long now, long leaseUntil,
        CancellationToken ct = default)
    {
        ValidateEventLease(token, now, leaseUntil);
        await using var db = await _factory.CreateDbContextAsync(ct);
        var value = await db.Set<FileOutboxEvent>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value == null || value.Version != version || value.Delivered || value.NextAttemptAt > now ||
            value.LeaseToken != null && value.LeaseUntil > now) return false;
        value.LeaseToken = token;
        value.LeaseUntil = leaseUntil;
        value.Attempts++;
        value.Version++;
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task<bool> RenewEventLeaseAsync(Guid id, string token, long leaseUntil, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (leaseUntil <= now) return false;
        await using var db = await _factory.CreateDbContextAsync(ct);
        var value = await db.Set<FileOutboxEvent>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value == null || value.Delivered || value.LeaseToken != token || value.LeaseUntil <= now) return false;
        value.LeaseUntil = leaseUntil;
        value.Version++;
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task<bool> FinishEventAsync(Guid id, string token, bool delivered, long nextAttemptAt,
        string? error, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        await using var db = await _factory.CreateDbContextAsync(ct);
        var value = await db.Set<FileOutboxEvent>().SingleOrDefaultAsync(x => x.Id == id, ct);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (value == null || value.Delivered || value.LeaseToken != token || value.LeaseUntil <= now) return false;
        value.Delivered = delivered;
        value.NextAttemptAt = nextAttemptAt;
        value.ErrorCode = error;
        value.LeaseToken = null;
        value.LeaseUntil = 0;
        value.Version++;
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task<FileOperationalSnapshot> GetOperationalSnapshotAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var operations = db.Set<FileOperation>().AsNoTracking();
        var due = operations.Where(x => x.State == FileOperationState.Pending && x.NextAttemptAt <= now ||
                                       x.State == FileOperationState.Running && x.LeaseUntil <= now);
        return new FileOperationalSnapshot
        {
            PendingOperations = await operations.LongCountAsync(x => x.State == FileOperationState.Pending, ct),
            RunningOperations = await operations.LongCountAsync(x => x.State == FileOperationState.Running, ct),
            FailedOperations = await operations.LongCountAsync(x => x.State == FileOperationState.Failed, ct),
            OldestDueAt = await due.Select(x => (long?)(x.State == FileOperationState.Pending ? x.NextAttemptAt : x.LeaseUntil)).MinAsync(ct) ?? 0,
            ActiveIoLeases = await db.Set<FileIoLease>().LongCountAsync(x => x.ExpiresAt > now, ct),
            ReservedBytes = await db.Set<FileUploadReservation>().SumAsync(x => (long?)x.Bytes, ct) ?? 0,
            PendingEvents = await db.Set<FileOutboxEvent>().LongCountAsync(x => !x.Delivered, ct)
        };
    }

    private static async Task<bool> TryReserveUploadAsync(FileManagementDbContext db, FileEntry entry,
        FileUploadAdmission admission, CancellationToken ct)
    {
        if (entry.Session == null) throw new ArgumentException("上传准入仅适用于包含会话的文件创建。", nameof(entry));
        if (admission.TenantSessions < 0 || admission.TenantBytes < 0 || admission.UserSessions < 0 || admission.UserBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(admission));
        if (entry.File.Length < 0) throw new ArgumentOutOfRangeException(nameof(entry), "预留长度不能为负数。");
        if (!await TryBumpControlLockAsync(db, ControlLockId("upload", entry.File.ScopeKey), ct)) return false;

        var current = db.Set<FileUploadReservation>().AsNoTracking().Where(x => x.ScopeKey == entry.File.ScopeKey);
        var tenantSessions = await current.LongCountAsync(ct);
        var tenantBytes = await current.SumAsync(x => (long?)x.Bytes, ct) ?? 0;
        var userReservations = current.Where(x => x.OwnerId == entry.File.CreatorId);
        var userSessions = await userReservations.LongCountAsync(ct);
        var userBytes = await userReservations.SumAsync(x => (long?)x.Bytes, ct) ?? 0;
        var requested = entry.File.Length;
        if (admission.TenantSessions > 0 && tenantSessions >= admission.TenantSessions ||
            admission.TenantBytes > 0 && (requested > admission.TenantBytes || tenantBytes > admission.TenantBytes - requested) ||
            admission.UserSessions > 0 && userSessions >= admission.UserSessions ||
            admission.UserBytes > 0 && (requested > admission.UserBytes || userBytes > admission.UserBytes - requested))
            throw new FileManagementException("capacity", "当前上传会话或预留空间已达到容量限制。");

        db.Add(new FileUploadReservation
        {
            FileId = entry.File.Id,
            ScopeKey = entry.File.ScopeKey,
            OwnerId = entry.File.CreatorId,
            Bytes = requested,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        return true;
    }

    private static async Task<bool> TryBumpControlLockAsync(FileManagementDbContext db, string id, CancellationToken ct)
    {
        var row = await db.Set<FileControlLock>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row == null)
        {
            db.Add(new FileControlLock { Id = id, Version = 1 });
            try { await db.SaveChangesAsync(ct); return true; }
            catch (DbUpdateConcurrencyException) { return false; }
        }
        var expected = row.Version;
        row.Version = checked(expected + 1);
        db.Entry(row).Property(x => x.Version).OriginalValue = expected;
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    private static FileOutboxEvent? CreateTransitionEvent(FileRecord file, FileState priorState, long now)
    {
        var kind = priorState != file.State && file.State == FileState.Ready ? "FileReady" :
            priorState != file.State && file.State == FileState.Deleted ? "FileDeleted" : null;
        return kind == null ? null : new FileOutboxEvent
        {
            Id = Guid.NewGuid(), FileId = file.Id, ScopeKey = file.ScopeKey, FileVersion = file.Version,
            Kind = kind, CreatedAt = now, NextAttemptAt = now, Version = 1
        };
    }

    private static FileOutboxEvent CreateReadyEvent(FileRecord file, long now) => new()
    {
        Id = Guid.NewGuid(), FileId = file.Id, ScopeKey = file.ScopeKey, FileVersion = file.Version,
        Kind = "FileReady", CreatedAt = now, NextAttemptAt = now, Version = 1
    };

    private static void PrepareOperation(FileOperation operation, long now)
    {
        if (operation.CreatedAt == 0) operation.CreatedAt = now;
        if (operation.State is FileOperationState.Pending or FileOperationState.Running) operation.FinishedAt = 0;
    }

    private static string ControlLockId(string purpose, string value)
        => purpose + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool HasExpectedMigrationVersion(string payload, long expectedVersion)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var property in document.RootElement.EnumerateObject())
                if (string.Equals(property.Name, "ExpectedVersion", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.TryGetInt64(out var actual)) return actual == expectedVersion;
            return false;
        }
        catch (JsonException) { return false; }
    }

    private static void ValidateLease(Guid id, string poolKey, string token, long expiresAt, int limit, long now)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(poolKey) || string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("租约标识、资源池和令牌不能为空。");
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        if (expiresAt <= now) throw new ArgumentOutOfRangeException(nameof(expiresAt));
    }

    private static void ValidateEventLease(string token, long now, long leaseUntil)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        if (leaseUntil <= now) throw new ArgumentOutOfRangeException(nameof(leaseUntil));
    }

    private static void ValidateTake(int take, int maximum)
    {
        if (take < 0 || take > maximum) throw new ArgumentOutOfRangeException(nameof(take));
    }

    private static void ValidatePage(int skip, int take, int maximum)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        ValidateTake(take, maximum);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            var typeName = current.GetType().FullName ?? current.GetType().Name;
            var code = ReadIntegerProperty(current, "SqliteExtendedErrorCode") ?? ReadIntegerProperty(current, "SqliteErrorCode") ??
                       ReadIntegerProperty(current, "Number") ?? ReadIntegerProperty(current, "ErrorCode");
            if (typeName.Contains("SqliteException", StringComparison.Ordinal) && code is 19 or 1555 or 2067 ||
                typeName.Contains("MySqlException", StringComparison.Ordinal) && code == 1062 ||
                typeName.Contains("PostgresException", StringComparison.Ordinal) && ReadTextProperty(current, "SqlState") == "23505" ||
                code is 2601 or 2627 || typeName.Contains("OracleException", StringComparison.Ordinal) && code == 1)
                return true;
        }
        return false;
    }

    private static int? ReadIntegerProperty(Exception exception, string name)
    {
        try { return exception.GetType().GetProperty(name)?.GetValue(exception) is { } value ? Convert.ToInt32(value) : null; }
        catch { return null; }
    }

    private static string? ReadTextProperty(Exception exception, string name)
    {
        try { return exception.GetType().GetProperty(name)?.GetValue(exception) as string; }
        catch { return null; }
    }
}
