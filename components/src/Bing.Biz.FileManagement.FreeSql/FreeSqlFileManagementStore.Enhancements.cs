using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bing.Biz.FileManagement;
using FreeSql;

namespace Bing.Biz.FileManagement.FreeSql;

public sealed partial class FreeSqlFileManagementStore
{
    private const int MaximumStorePageSize = 1000;

    public Task<IReadOnlyList<FileRecord>> QueryFilesAsync(FileCandidateQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.ScopeKey) || query.Take is < 1 or > 100 ||
            query.CreatedFrom > query.CreatedTo || (query.AfterCreatedAt.HasValue != (query.AfterKey != null)))
            throw new ArgumentException("文件查询条件无效。", nameof(query));
        return InTransactionAsync<IReadOnlyList<FileRecord>>(async (orm, transaction, token) =>
        {
            var records = orm.Select<FileRecord>().WithTransaction(transaction)
                .Where(x => x.ScopeKey == query.ScopeKey && x.State == FileState.Ready);
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
                records = records.Where(file => orm.Select<FileBinding>()
                    .Where(binding => binding.FileId == file.Id &&
                        (businessType == null || binding.BusinessType == businessType) &&
                        (businessId == null || binding.BusinessId == businessId)).Any());
            }
            if (query.AfterCreatedAt.HasValue)
            {
                var afterCreatedAt = query.AfterCreatedAt.Value;
                var afterKey = query.AfterKey!;
                records = records.Where(
                    "CreatedAt < @afterCreatedAt OR (CreatedAt = @afterCreatedAt AND RequestKey > @afterKey)",
                    new { afterCreatedAt, afterKey });
            }
            return await records.OrderByDescending(x => x.CreatedAt).OrderBy(x => x.RequestKey)
                .Limit(query.Take).ToListAsync(token).ConfigureAwait(false);
        }, ct);
    }

    public Task<IReadOnlyList<UploadSessionSummary>> QuerySessionsAsync(string scopeKey, string ownerId,
        FileState? state, bool includeExpired, long now, int skip, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("所有者不能为空。", nameof(ownerId));
        ValidatePage(skip, take, 100);
        if (take == 0) return Task.FromResult<IReadOnlyList<UploadSessionSummary>>(Array.Empty<UploadSessionSummary>());
        return InTransactionAsync<IReadOnlyList<UploadSessionSummary>>(async (orm, transaction, token) =>
        {
            var query = orm.Select<FileUploadSession, FileRecord>()
                .WithTransaction(transaction)
                .InnerJoin((session, file) => session.FileId == file.Id)
                .Where((session, file) => file.ScopeKey == scopeKey && session.OwnerId == ownerId);
            if (state.HasValue) query = query.Where((session, _) => session.State == state.Value);
            else query = query.Where((session, _) => session.State == FileState.Pending || session.State == FileState.Uploading ||
                                                      session.State == FileState.Completing || session.State == FileState.Quarantined);
            if (!includeExpired) query = query.Where((session, _) =>
                (session.State != FileState.Pending && session.State != FileState.Uploading) ||
                session.ExpiresAt > now && session.AbsoluteExpiresAt > now);
            return await query.OrderByDescending((_, file) => file.CreatedAt).OrderBy((session, _) => session.Id)
                .Skip(skip).Limit(take)
                .ToListAsync((session, file) => new UploadSessionSummary
                {
                    Id = session.Id,
                    FileId = file.Id,
                    FileName = file.FileName,
                    Container = file.Container,
                    ContentType = file.ContentType,
                    Length = file.Length,
                    LastModified = session.ClientLastModified,
                    ExpiresAt = session.ExpiresAt,
                    State = session.State
                }, token).ConfigureAwait(false);
        }, ct);
    }

    public Task<IReadOnlyList<FileOperation>> QueryOperationsAsync(FileOperationQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.ScopeKey) || query.Skip < 0 || query.Take is < 1 or > 100 ||
            query.CreatedFrom > query.CreatedTo)
            throw new ArgumentException("任务查询条件无效。", nameof(query));
        return InTransactionAsync<IReadOnlyList<FileOperation>>(async (orm, transaction, token) =>
        {
            var operations = orm.Select<FileOperation, FileRecord>().WithTransaction(transaction)
                .InnerJoin((operation, file) => operation.FileId == file.Id)
                .Where((_, file) => file.ScopeKey == query.ScopeKey)
                .Where((operation, _) => !orm.Select<FileHistoryArchive>()
                    .Where(archive => archive.ScopeKey == query.ScopeKey && archive.Kind == FileHistoryKind.Operation &&
                        archive.EntityId == operation.Id && archive.SourceVersion == operation.Version).Any());
            if (query.FileId.HasValue) operations = operations.Where((operation, _) => operation.FileId == query.FileId.Value);
            if (query.Kind.HasValue) operations = operations.Where((operation, _) => operation.Kind == query.Kind.Value);
            if (query.State.HasValue) operations = operations.Where((operation, _) => operation.State == query.State.Value);
            if (query.CreatedFrom.HasValue) operations = operations.Where((operation, _) => operation.CreatedAt >= query.CreatedFrom.Value);
            if (query.CreatedTo.HasValue) operations = operations.Where((operation, _) => operation.CreatedAt <= query.CreatedTo.Value);
            return await operations.OrderByDescending((operation, _) => operation.CreatedAt).OrderBy((operation, _) => operation.Id)
                .Skip(query.Skip).Limit(query.Take).ToListAsync((operation, _) => operation, token).ConfigureAwait(false);
        }, ct);
    }

    public Task<FileOperation?> FindOperationAsync(Guid id, string? scopeKey = null, CancellationToken ct = default)
        => InTransactionAsync<FileOperation?>(async (orm, transaction, token) =>
        {
            var query = orm.Select<FileOperation>().WithTransaction(transaction).Where(x => x.Id == id);
            if (scopeKey != null)
                query = query.Where(operation => orm.Select<FileRecord>()
                    .Where(file => file.Id == operation.FileId && file.ScopeKey == scopeKey).Any());
            return await query.FirstAsync(token).ConfigureAwait(false);
        }, ct);

    public Task<FileOperationAudit?> FindRecoveryAsync(string requestKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestKey)) throw new ArgumentException("请求键不能为空。", nameof(requestKey));
        return InTransactionAsync<FileOperationAudit?>(async (orm, transaction, token) =>
            await orm.Select<FileOperationAudit>().WithTransaction(transaction).Where(x => x.RequestKey == requestKey)
                .FirstAsync(token).ConfigureAwait(false), ct);
    }

    public Task<IReadOnlyList<FileOperationAudit>> ListOperationAuditAsync(string scopeKey, Guid operationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        return InTransactionAsync<IReadOnlyList<FileOperationAudit>>(async (orm, transaction, token) =>
            await orm.Select<FileOperationAudit>().WithTransaction(transaction)
                .Where(x => x.ScopeKey == scopeKey && x.OperationId == operationId)
                .OrderByDescending(x => x.CreatedAt).OrderBy(x => x.Id).ToListAsync(token).ConfigureAwait(false), ct);
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
        try
        {
            return await InTransactionAsync(async (orm, transaction, token) =>
            {
                var operation = await orm.Select<FileOperation>().WithTransaction(transaction)
                    .Where(x => x.Id == original.Id && x.State == FileOperationState.Failed &&
                                x.Version == original.Version && x.RecoveryOperationId == null)
                    .FirstAsync(token).ConfigureAwait(false);
                if (operation == null) return false;
                var file = await orm.Select<FileRecord>().WithTransaction(transaction)
                    .Where(x => x.Id == original.FileId && x.Version == fileVersion && x.ScopeKey == audit.ScopeKey)
                    .FirstAsync(token).ConfigureAwait(false);
                if (file == null) return false;
                var fileFence = await orm.Update<FileRecord>().Set(x => x.Version + 1)
                    .Where(x => x.Id == file.Id && x.Version == fileVersion && x.ScopeKey == audit.ScopeKey)
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (fileFence != 1) throw new StoreConcurrencyException();
                var restoreFence = await orm.Update<FileRecord>().Set(x => x.Version, fileVersion)
                    .Where(x => x.Id == file.Id && x.Version == checked(fileVersion + 1) && x.ScopeKey == audit.ScopeKey)
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (restoreFence != 1) throw new StoreConcurrencyException();
                var linked = await orm.Update<FileOperation>().Set(x => x.RecoveryOperationId, replacement.Id)
                    .Set(x => x.Version + 1)
                    .Where(x => x.Id == original.Id && x.State == FileOperationState.Failed &&
                                x.Version == original.Version && x.RecoveryOperationId == null)
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (linked != 1) throw new StoreConcurrencyException();

                var linkedItems = await orm.Select<FileMigrationBatchItem, FileMigrationBatch>()
                    .WithTransaction(transaction)
                    .InnerJoin((item, batch) => item.BatchId == batch.Id)
                    .Where((item, batch) => item.OperationId == original.Id &&
                        (item.State == FileMigrationItemState.Failed || item.State == FileMigrationItemState.Queued) &&
                        batch.ScopeKey == audit.ScopeKey)
                    .ToListAsync((item, _) => item, token).ConfigureAwait(false);
                var batchIds = linkedItems.Select(x => x.BatchId).Distinct().ToArray();
                foreach (var item in linkedItems)
                {
                    var itemChanged = await orm.Update<FileMigrationBatchItem>()
                        .Set(x => x.OperationId, replacement.Id)
                        .Set(x => x.State, FileMigrationItemState.Queued)
                        .Set(x => x.ErrorCode, (string?)null)
                        .Set(x => x.Version + 1)
                        .Where(x => x.BatchId == item.BatchId && x.FileId == item.FileId &&
                                    x.OperationId == original.Id && x.Version == item.Version &&
                                    (x.State == FileMigrationItemState.Failed || x.State == FileMigrationItemState.Queued))
                        .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    if (itemChanged != 1) throw new StoreConcurrencyException();
                }
                if (batchIds.Length != 0)
                    await orm.Update<FileMigrationBatch>().Set(x => x.State, FileMigrationBatchState.Running)
                        .Set(x => x.Version + 1)
                        .Where(x => Enumerable.Contains(batchIds, x.Id) && x.ScopeKey == audit.ScopeKey &&
                                    x.State == FileMigrationBatchState.Completed)
                        .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);

                PrepareOperation(replacement, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                await orm.Insert(replacement).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                await orm.Insert(audit).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                return true;
            }, ct).ConfigureAwait(false);
        }
        catch (StoreConcurrencyException) { return false; }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { return false; }
    }

    public Task<FileMigrationBatch?> FindBatchAsync(Guid id, string? scopeKey = null, CancellationToken ct = default)
        => InTransactionAsync<FileMigrationBatch?>(async (orm, transaction, token) =>
        {
            var query = orm.Select<FileMigrationBatch>().WithTransaction(transaction).Where(x => x.Id == id);
            if (scopeKey != null) query = query.Where(x => x.ScopeKey == scopeKey);
            return await query.FirstAsync(token).ConfigureAwait(false);
        }, ct);

    public Task<FileMigrationBatch?> FindBatchRequestAsync(string requestKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestKey)) throw new ArgumentException("请求键不能为空。", nameof(requestKey));
        return InTransactionAsync<FileMigrationBatch?>(async (orm, transaction, token) =>
            await orm.Select<FileMigrationBatch>().WithTransaction(transaction).Where(x => x.RequestKey == requestKey)
                .FirstAsync(token).ConfigureAwait(false), ct);
    }

    public async Task<bool> TryCreateBatchAsync(FileMigrationBatch batch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        try
        {
            await InTransactionAsync(async (orm, transaction, token) =>
            {
                await orm.Insert(batch).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                return true;
            }, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { return false; }
    }

    public Task<IReadOnlyList<FileMigrationBatch>> ListActiveBatchesAsync(int take, CancellationToken ct = default)
    {
        ValidateTake(take, MaximumStorePageSize);
        if (take == 0) return Task.FromResult<IReadOnlyList<FileMigrationBatch>>(Array.Empty<FileMigrationBatch>());
        return InTransactionAsync<IReadOnlyList<FileMigrationBatch>>(async (orm, transaction, token) =>
            await orm.Select<FileMigrationBatch>().WithTransaction(transaction)
                .Where(x => x.State == FileMigrationBatchState.Running)
                .OrderBy(x => x.CreatedAt).OrderBy(x => x.Id).Limit(take).ToListAsync(token).ConfigureAwait(false), ct);
    }

    public Task<IReadOnlyList<FileMigrationBatchItem>> ListBatchItemsToReconcileAsync(int take, CancellationToken ct = default)
    {
        ValidateTake(take, MaximumStorePageSize);
        ct.ThrowIfCancellationRequested();
        if (take == 0) return Task.FromResult<IReadOnlyList<FileMigrationBatchItem>>(Array.Empty<FileMigrationBatchItem>());
        return InTransactionAsync<IReadOnlyList<FileMigrationBatchItem>>(async (orm, transaction, token) =>
            await orm.Select<FileMigrationBatchItem, FileMigrationBatch>().WithTransaction(transaction)
                .InnerJoin((item, batch) => item.BatchId == batch.Id)
                .Where((item, batch) => item.State == FileMigrationItemState.Queued &&
                    (batch.State == FileMigrationBatchState.Running || batch.State == FileMigrationBatchState.Paused) &&
                    orm.Select<FileOperation>().Where(operation => item.OperationId == operation.Id &&
                        operation.FileId == item.FileId &&
                        (operation.State == FileOperationState.Succeeded || operation.State == FileOperationState.Failed)).Any())
                .OrderBy((item, batch) => batch.CreatedAt).OrderBy((item, batch) => batch.Id)
                .OrderBy((item, batch) => item.FileId).Limit(take)
                .ToListAsync((item, _) => item, token).ConfigureAwait(false), ct);
    }

    public Task<IReadOnlyList<FileMigrationBatch>> ListBatchesAsync(string scopeKey, int skip, int take,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        ValidatePage(skip, take, MaximumStorePageSize);
        if (take == 0) return Task.FromResult<IReadOnlyList<FileMigrationBatch>>(Array.Empty<FileMigrationBatch>());
        return InTransactionAsync<IReadOnlyList<FileMigrationBatch>>(async (orm, transaction, token) =>
            await orm.Select<FileMigrationBatch>().WithTransaction(transaction).Where(x => x.ScopeKey == scopeKey)
                .Where(batch => !orm.Select<FileHistoryArchive>().Where(archive => archive.ScopeKey == scopeKey &&
                    archive.Kind == FileHistoryKind.MigrationBatch && archive.EntityId == batch.Id &&
                    archive.SourceVersion == batch.Version).Any())
                .OrderByDescending(x => x.CreatedAt).OrderBy(x => x.Id).Skip(skip).Limit(take)
                .ToListAsync(token).ConfigureAwait(false), ct);
    }

    public async Task<bool> TryUpdateBatchAsync(FileMigrationBatch batch, long expectedVersion,
        IReadOnlyList<FileMigrationBatchItem>? additions = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (expectedVersion < 0 || additions?.Any(x => x.BatchId != batch.Id) == true)
            throw new ArgumentException("批次版本或新增条目无效。", nameof(batch));
        var originalVersion = batch.Version;
        batch.Version = checked(expectedVersion + 1);
        try
        {
            await InTransactionAsync(async (orm, transaction, token) =>
            {
                var changed = await orm.Update<FileMigrationBatch>().SetSource(batch)
                    .Where(x => x.Id == batch.Id && x.Version == expectedVersion)
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (changed != 1) throw new StoreConcurrencyException();
                if (additions != null)
                    foreach (var addition in additions)
                        await orm.Insert(addition).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                return true;
            }, ct).ConfigureAwait(false);
            return true;
        }
        catch (StoreConcurrencyException) { batch.Version = originalVersion; return false; }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { batch.Version = originalVersion; return false; }
        catch { batch.Version = originalVersion; throw; }
    }

    public Task<IReadOnlyList<FileMigrationBatchItem>> ListBatchItemsAsync(Guid batchId,
        FileMigrationItemState? state, int skip, int take, CancellationToken ct = default)
    {
        ValidatePage(skip, take, MaximumStorePageSize);
        if (take == 0) return Task.FromResult<IReadOnlyList<FileMigrationBatchItem>>(Array.Empty<FileMigrationBatchItem>());
        return InTransactionAsync<IReadOnlyList<FileMigrationBatchItem>>(async (orm, transaction, token) =>
        {
            var query = orm.Select<FileMigrationBatchItem>().WithTransaction(transaction).Where(x => x.BatchId == batchId);
            if (state.HasValue) query = query.Where(x => x.State == state.Value);
            return await query.OrderBy(x => x.FileId).Skip(skip).Limit(take).ToListAsync(token).ConfigureAwait(false);
        }, ct);
    }

    public async Task<FileMigrationBatchView> GetBatchProgressAsync(Guid batchId, CancellationToken ct = default)
        => await InTransactionAsync(async (orm, transaction, token) =>
        {
            var batch = await orm.Select<FileMigrationBatch>().WithTransaction(transaction)
                .Where(x => x.Id == batchId).FirstAsync(token).ConfigureAwait(false)
                ?? throw new FileManagementException("not_found", "迁移批次不存在。");
            return new FileMigrationBatchView
            {
                Batch = batch,
                Total = await orm.Select<FileMigrationBatchItem>().WithTransaction(transaction)
                    .Where(x => x.BatchId == batchId).CountAsync(token).ConfigureAwait(false),
                Bytes = Convert.ToInt64(await orm.Select<FileMigrationBatchItem>().WithTransaction(transaction)
                    .Where(x => x.BatchId == batchId).SumAsync(x => x.Length, token).ConfigureAwait(false)),
                Pending = await orm.Select<FileMigrationBatchItem>().WithTransaction(transaction)
                    .Where(x => x.BatchId == batchId && x.State == FileMigrationItemState.Pending).CountAsync(token).ConfigureAwait(false),
                Queued = await orm.Select<FileMigrationBatchItem>().WithTransaction(transaction)
                    .Where(x => x.BatchId == batchId && x.State == FileMigrationItemState.Queued).CountAsync(token).ConfigureAwait(false),
                Succeeded = await orm.Select<FileMigrationBatchItem>().WithTransaction(transaction)
                    .Where(x => x.BatchId == batchId && x.State == FileMigrationItemState.Succeeded).CountAsync(token).ConfigureAwait(false),
                Failed = await orm.Select<FileMigrationBatchItem>().WithTransaction(transaction)
                    .Where(x => x.BatchId == batchId && x.State == FileMigrationItemState.Failed).CountAsync(token).ConfigureAwait(false),
                Conflicts = await orm.Select<FileMigrationBatchItem>().WithTransaction(transaction)
                    .Where(x => x.BatchId == batchId && x.State == FileMigrationItemState.Conflict).CountAsync(token).ConfigureAwait(false)
            };
        }, ct).ConfigureAwait(false);

    public async Task<bool> TryQueueBatchItemAsync(FileMigrationBatch batch, FileMigrationBatchItem item,
        FileOperation operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(operation);
        if (item.BatchId != batch.Id || operation.FileId != item.FileId || operation.Kind != FileOperationKind.Migrate ||
            operation.Id == Guid.Empty || !HasExpectedMigrationVersion(operation.Payload, item.SourceVersion + 1))
            return false;
        var originalBatchVersion = batch.Version;
        var originalItemVersion = item.Version;
        try
        {
            await InTransactionAsync(async (orm, transaction, token) =>
            {
                var batchChanged = await orm.Update<FileMigrationBatch>().Set(x => x.Version + 1)
                    .Where(x => x.Id == batch.Id && x.ScopeKey == batch.ScopeKey && x.State == FileMigrationBatchState.Running &&
                                x.Version == batch.Version)
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (batchChanged != 1) throw new StoreConcurrencyException();

                var files = orm.Update<FileRecord>().Set(x => x.Version + 1)
                    .Where(x => x.Id == item.FileId && x.ScopeKey == batch.ScopeKey && x.State == FileState.Ready &&
                                x.StoreName == batch.SourceStore && x.Version == item.SourceVersion);
                if (batch.Container != null) files = files.Where(x => x.Container == batch.Container);
                if (await files.WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false) != 1)
                    throw new StoreConcurrencyException();

                var itemChanged = await orm.Update<FileMigrationBatchItem>()
                    .Set(x => x.State, FileMigrationItemState.Queued)
                    .Set(x => x.OperationId, operation.Id)
                    .Set(x => x.Version + 1)
                    .Where(x => x.BatchId == batch.Id && x.FileId == item.FileId &&
                                x.State == FileMigrationItemState.Pending && x.SourceVersion == item.SourceVersion &&
                                x.Version == item.Version)
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (itemChanged != 1) throw new StoreConcurrencyException();
                PrepareOperation(operation, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                await orm.Insert(operation).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                return true;
            }, ct).ConfigureAwait(false);
            batch.Version = checked(originalBatchVersion + 1);
            item.Version = checked(originalItemVersion + 1);
            item.State = FileMigrationItemState.Queued;
            item.OperationId = operation.Id;
            return true;
        }
        catch (StoreConcurrencyException)
        {
            batch.Version = originalBatchVersion;
            item.Version = originalItemVersion;
            return false;
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception))
        {
            batch.Version = originalBatchVersion;
            item.Version = originalItemVersion;
            return false;
        }
        catch
        {
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
        item.Version = checked(expectedVersion + 1);
        try
        {
            var affected = await _orm.Update<FileMigrationBatchItem>().SetSource(item)
                .Where(x => x.BatchId == item.BatchId && x.FileId == item.FileId && x.Version == expectedVersion)
                .ExecuteAffrowsAsync(ct).ConfigureAwait(false);
            if (affected == 1) return true;
            item.Version = originalVersion;
            return false;
        }
        catch
        {
            item.Version = originalVersion;
            throw;
        }
    }

    public async Task<bool> TryAcquireIoLeaseAsync(FileIoLease lease, int limit, long now, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ValidateLease(lease.Id, lease.PoolKey, lease.Token, lease.ExpiresAt, limit, now);
        try
        {
            return await InTransactionAsync(async (orm, transaction, token) =>
            {
                if (!await TryBumpControlLockAsync(orm, transaction, ControlLockId("io", lease.PoolKey), token).ConfigureAwait(false))
                    return false;
                await orm.Delete<FileIoLease>().Where(x => x.PoolKey == lease.PoolKey && x.ExpiresAt <= now)
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                var active = await orm.Select<FileIoLease>().WithTransaction(transaction)
                    .Where(x => x.PoolKey == lease.PoolKey && x.ExpiresAt > now).CountAsync(token).ConfigureAwait(false);
                if (active >= limit) return false;
                await orm.Insert(lease).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                return true;
            }, ct).ConfigureAwait(false);
        }
        catch (StoreConcurrencyException) { return false; }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { return false; }
    }

    public async Task<bool> RenewIoLeaseAsync(Guid id, string token, long expiresAt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (expiresAt <= now) return false;
        return await _orm.Update<FileIoLease>().Set(x => x.ExpiresAt, expiresAt)
            .Where(x => x.Id == id && x.Token == token && x.ExpiresAt > now)
            .ExecuteAffrowsAsync(ct).ConfigureAwait(false) == 1;
    }

    public async Task ReleaseIoLeaseAsync(Guid id, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        await _orm.Delete<FileIoLease>().Where(x => x.Id == id && x.Token == token).ExecuteAffrowsAsync(ct).ConfigureAwait(false);
    }

    public async Task ReleaseUploadReservationAsync(Guid fileId, CancellationToken ct = default)
        => await _orm.Delete<FileUploadReservation>().Where(x => x.FileId == fileId).ExecuteAffrowsAsync(ct).ConfigureAwait(false);

    public Task<IReadOnlyList<FileOutboxEvent>> ListDueEventsAsync(long now, int take, CancellationToken ct = default)
    {
        ValidateTake(take, MaximumStorePageSize);
        if (take == 0) return Task.FromResult<IReadOnlyList<FileOutboxEvent>>(Array.Empty<FileOutboxEvent>());
        return InTransactionAsync<IReadOnlyList<FileOutboxEvent>>(async (orm, transaction, token) =>
            await orm.Select<FileOutboxEvent>().WithTransaction(transaction)
                .Where(x => !x.Delivered && x.NextAttemptAt <= now && (x.LeaseToken == null || x.LeaseUntil <= now))
                .OrderBy(x => x.NextAttemptAt).OrderBy(x => x.CreatedAt).OrderBy(x => x.Id).Limit(take)
                .ToListAsync(token).ConfigureAwait(false), ct);
    }

    public Task<bool> TryClaimEventAsync(Guid id, long version, string token, long now, long leaseUntil,
        CancellationToken ct = default)
    {
        ValidateEventLease(token, now, leaseUntil);
        return InTransactionAsync(async (orm, transaction, cancellationToken) =>
            await orm.Update<FileOutboxEvent>().Set(x => x.LeaseToken, token).Set(x => x.LeaseUntil, leaseUntil)
                .Set(x => x.Attempts + 1).Set(x => x.Version + 1)
                .Where(x => x.Id == id && x.Version == version && !x.Delivered && x.NextAttemptAt <= now &&
                            (x.LeaseToken == null || x.LeaseUntil <= now))
                .WithTransaction(transaction).ExecuteAffrowsAsync(cancellationToken).ConfigureAwait(false) == 1, ct);
    }

    public Task<bool> RenewEventLeaseAsync(Guid id, string token, long leaseUntil, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (leaseUntil <= now) return Task.FromResult(false);
        return InTransactionAsync(async (orm, transaction, cancellationToken) =>
            await orm.Update<FileOutboxEvent>().Set(x => x.LeaseUntil, leaseUntil).Set(x => x.Version + 1)
                .Where(x => x.Id == id && x.LeaseToken == token && x.LeaseUntil > now && !x.Delivered)
                .WithTransaction(transaction).ExecuteAffrowsAsync(cancellationToken).ConfigureAwait(false) == 1, ct);
    }

    public Task<bool> FinishEventAsync(Guid id, string token, bool delivered, long nextAttemptAt,
        string? error, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return InTransactionAsync(async (orm, transaction, cancellationToken) =>
            await orm.Update<FileOutboxEvent>().Set(x => x.Delivered, delivered).Set(x => x.NextAttemptAt, nextAttemptAt)
                .Set(x => x.ErrorCode, error).Set(x => x.LeaseToken, (string?)null).Set(x => x.LeaseUntil, 0L)
                .Set(x => x.Version + 1)
                .Where(x => x.Id == id && x.LeaseToken == token && x.LeaseUntil > now && !x.Delivered)
                .WithTransaction(transaction).ExecuteAffrowsAsync(cancellationToken).ConfigureAwait(false) == 1, ct);
    }

    public async Task<FileOperationalSnapshot> GetOperationalSnapshotAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return await InTransactionAsync(async (orm, transaction, token) =>
        {
            var pending = await orm.Select<FileOperation>().WithTransaction(transaction)
                .Where(x => x.State == FileOperationState.Pending).CountAsync(token).ConfigureAwait(false);
            var running = await orm.Select<FileOperation>().WithTransaction(transaction)
                .Where(x => x.State == FileOperationState.Running).CountAsync(token).ConfigureAwait(false);
            var failed = await orm.Select<FileOperation>().WithTransaction(transaction)
                .Where(x => x.State == FileOperationState.Failed).CountAsync(token).ConfigureAwait(false);
            var dueRows = await orm.Select<FileOperation>().WithTransaction(transaction)
                .Where(x => (x.State == FileOperationState.Pending && x.NextAttemptAt <= now) ||
                            (x.State == FileOperationState.Running && x.LeaseUntil <= now))
                .OrderBy(x => x.State == FileOperationState.Pending ? x.NextAttemptAt : x.LeaseUntil)
                .Limit(1).ToListAsync(token).ConfigureAwait(false);
            return new FileOperationalSnapshot
            {
                PendingOperations = pending,
                RunningOperations = running,
                FailedOperations = failed,
                OldestDueAt = dueRows.Count == 0 ? 0 :
                    dueRows[0].State == FileOperationState.Pending ? dueRows[0].NextAttemptAt : dueRows[0].LeaseUntil,
                ActiveIoLeases = await orm.Select<FileIoLease>().WithTransaction(transaction)
                    .Where(x => x.ExpiresAt > now).CountAsync(token).ConfigureAwait(false),
                ReservedBytes = Convert.ToInt64(await orm.Select<FileUploadReservation>().WithTransaction(transaction)
                    .SumAsync(x => x.Bytes, token).ConfigureAwait(false)),
                PendingEvents = await orm.Select<FileOutboxEvent>().WithTransaction(transaction)
                    .Where(x => !x.Delivered).CountAsync(token).ConfigureAwait(false)
            };
        }, ct).ConfigureAwait(false);
    }

    private static async Task<bool> TryReserveUploadAsync(IFreeSql orm, DbTransaction transaction, FileEntry entry,
        FileUploadAdmission admission, CancellationToken ct)
    {
        if (entry.Session == null) throw new ArgumentException("上传准入仅适用于包含会话的文件创建。", nameof(entry));
        if (admission.TenantSessions < 0 || admission.TenantBytes < 0 || admission.UserSessions < 0 || admission.UserBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(admission));
        if (entry.File.Length < 0) throw new ArgumentOutOfRangeException(nameof(entry), "预留长度不能为负数。");
        if (!await TryBumpControlLockAsync(orm, transaction, ControlLockId("upload", entry.File.ScopeKey), ct).ConfigureAwait(false))
            return false;
        var current = orm.Select<FileUploadReservation>().WithTransaction(transaction).Where(x => x.ScopeKey == entry.File.ScopeKey);
        var tenantSessions = await current.CountAsync(ct).ConfigureAwait(false);
        var tenantBytes = Convert.ToInt64(await current.SumAsync(x => x.Bytes, ct).ConfigureAwait(false));
        var userReservations = current.Where(x => x.OwnerId == entry.File.CreatorId);
        var userSessions = await userReservations.CountAsync(ct).ConfigureAwait(false);
        var userBytes = Convert.ToInt64(await userReservations.SumAsync(x => x.Bytes, ct).ConfigureAwait(false));
        var requested = entry.File.Length;
        if (admission.TenantSessions > 0 && tenantSessions >= admission.TenantSessions ||
            admission.TenantBytes > 0 && (requested > admission.TenantBytes || tenantBytes > admission.TenantBytes - requested) ||
            admission.UserSessions > 0 && userSessions >= admission.UserSessions ||
            admission.UserBytes > 0 && (requested > admission.UserBytes || userBytes > admission.UserBytes - requested))
            throw new FileManagementException("capacity", "当前上传会话或预留空间已达到容量限制。");
        await orm.Insert(new FileUploadReservation
        {
            FileId = entry.File.Id,
            ScopeKey = entry.File.ScopeKey,
            OwnerId = entry.File.CreatorId,
            Bytes = requested,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }).WithTransaction(transaction).ExecuteAffrowsAsync(ct).ConfigureAwait(false);
        return true;
    }

    private static async Task<bool> TryBumpControlLockAsync(IFreeSql orm, DbTransaction transaction, string id,
        CancellationToken ct)
    {
        var row = await orm.Select<FileControlLock>().WithTransaction(transaction).Where(x => x.Id == id)
            .FirstAsync(ct).ConfigureAwait(false);
        if (row == null)
        {
            try
            {
                await orm.Insert(new FileControlLock { Id = id, Version = 1 }).WithTransaction(transaction)
                    .ExecuteAffrowsAsync(ct).ConfigureAwait(false);
                return true;
            }
            catch (Exception exception) when (IsUniqueConstraintViolation(exception))
            {
                return false;
            }
        }
        return await orm.Update<FileControlLock>().Set(x => x.Version + 1)
            .Where(x => x.Id == id && x.Version == row.Version)
            .WithTransaction(transaction).ExecuteAffrowsAsync(ct).ConfigureAwait(false) == 1;
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
}
