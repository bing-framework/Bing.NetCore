using System.Data.Common;
using System.Data;
using System.Reflection;
using Bing.Biz.FileManagement;
using FreeSql;

namespace Bing.Biz.FileManagement.FreeSql;

/// <summary>使用 FreeSql 实现文件管理仓储事务、CAS 和租约操作。</summary>
public sealed partial class FreeSqlFileManagementStore : IFileManagementStore
{
    private readonly IFreeSql _orm;

    /// <summary>使用调用方拥有的 FreeSql 客户端创建仓储。</summary>
    /// <param name="orm">FreeSql 客户端；本仓储不负责释放它。</param>
    public FreeSqlFileManagementStore(IFreeSql orm)
    {
        _orm = orm ?? throw new ArgumentNullException(nameof(orm));
        FreeSqlFileManagementModel.Configure(_orm);
    }

    /// <inheritdoc />
    public async Task<FileManagementStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new()
        {
            ActiveSessions = await _orm.Select<FileUploadSession>().Where(x => x.State == FileState.Pending || x.State == FileState.Uploading || x.State == FileState.Completing || x.State == FileState.Quarantined).CountAsync(ct),
            PendingCleanup = await _orm.Select<FileOperation>().Where(x => (x.State == FileOperationState.Pending || x.State == FileOperationState.Running) && (x.Kind == FileOperationKind.Abort || x.Kind == FileOperationKind.Delete || x.Kind == FileOperationKind.DeleteReplica)).CountAsync(ct)
                + await _orm.Select<FileTransferAttempt>().Where(x => x.State == FileTransferState.Retired || x.State == FileTransferState.Active && x.LeaseUntil <= now).CountAsync(ct),
            FailedOperations = await _orm.Select<FileOperation>().Where(x => x.State == FileOperationState.Failed).CountAsync(ct)
        };
    }

    /// <inheritdoc />
    public Task<FileEntry?> FindAsync(Guid fileId, CancellationToken ct = default)
        => InTransactionAsync((orm, transaction, token) => FindEntryAsync(orm, transaction, fileId, token), ct);

    /// <inheritdoc />
    public Task<FileEntry?> FindSessionAsync(Guid sessionId, CancellationToken ct = default)
        => InTransactionAsync(async (orm, transaction, token) =>
        {
            var session = await orm.Select<FileUploadSession>()
                .WithTransaction(transaction)
                .Where(x => x.Id == sessionId)
                .FirstAsync(token)
                .ConfigureAwait(false);
            return session == null
                ? null
                : await FindEntryAsync(orm, transaction, session.FileId, token).ConfigureAwait(false);
        }, ct);

    /// <inheritdoc />
    public Task<FileEntry?> FindRequestAsync(string requestKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requestKey);
        return InTransactionAsync(async (orm, transaction, token) =>
        {
            var file = await orm.Select<FileRecord>()
                .WithTransaction(transaction)
                .Where(x => x.RequestKey == requestKey)
                .FirstAsync(token)
                .ConfigureAwait(false);
            return file == null ? null : await FindEntryAsync(orm, transaction, file.Id, token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public async Task<bool> TryCreateAsync(FileEntry entry, IReadOnlyList<FileOperation>? operations = null,
        CancellationToken ct = default, FileTransferLease? transfer = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry, operations);
        try
        {
            return await InTransactionAsync(async (orm, transaction, token) =>
            {
                if (await orm.Select<FileRecord>().WithTransaction(transaction)
                    .Where(x => x.Id == entry.File.Id || x.RequestKey == entry.File.RequestKey).AnyAsync(token).ConfigureAwait(false))
                    return false;
                if (entry.Admission != null && !await TryReserveUploadAsync(orm, transaction, entry, entry.Admission, token).ConfigureAwait(false))
                    throw new StoreConcurrencyException();
                if (transfer != null && !await PublishTransferAsync(orm, transaction, transfer, entry.File, token)) throw new StoreConcurrencyException();
                await orm.Insert(entry.File).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (entry.Session != null)
                    await orm.Insert(entry.Session).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                foreach (var binding in entry.Bindings)
                    await orm.Insert(binding).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (operations != null)
                    foreach (var operation in operations)
                    {
                        PrepareOperation(operation, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        await orm.Insert(operation).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    }
                if (entry.File.State == FileState.Ready)
                    await orm.Insert(CreateReadyEvent(entry.File, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                        .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                return true;
            }, ct).ConfigureAwait(false);
        }
        catch (StoreConcurrencyException) { return false; }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception))
        {
            if (await RootExistsAsync(entry.File.Id, entry.File.RequestKey, ct).ConfigureAwait(false))
                return false;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryUpdateAsync(FileEntry entry, long expectedVersion,
        IReadOnlyList<FileUploadPart>? suppliedParts = null, IReadOnlyList<FileOperation>? operations = null,
        CancellationToken ct = default, FileOperationLease? lease = null, FileTransferLease? transfer = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry, operations);
        if (expectedVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        if (suppliedParts != null && entry.Session == null && suppliedParts.Count > 0)
            throw new ArgumentException("没有上传会话时不能写入分片。", nameof(suppliedParts));
        if (entry.Session != null && suppliedParts != null && suppliedParts.Any(part => part.SessionId != entry.Session.Id))
            throw new ArgumentException("分片会话不一致。", nameof(suppliedParts));
        if (suppliedParts != null && suppliedParts.Select(part => part.Number).Distinct().Count() != suppliedParts.Count)
            throw new ArgumentException("同一批次不能包含重复分片序号。", nameof(suppliedParts));

        var nextVersion = checked(expectedVersion + 1);
        var originalFileVersion = entry.File.Version;
        var originalSessionVersion = entry.Session?.Version;
        bool updated;
        try
        {
            entry.File.Version = nextVersion;
            updated = await InTransactionAsync(async (orm, transaction, token) =>
            {
                var currentFile = await orm.Select<FileRecord>().WithTransaction(transaction)
                    .Where(x => x.Id == entry.File.Id).FirstAsync(token).ConfigureAwait(false);
                var priorState = currentFile?.State;
                if (lease != null)
                {
                    var leaseNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var fenced = await orm.Update<FileOperation>().Set(x => x.Version + 1)
                        .Where(x => x.Id == lease.OperationId && x.FileId == entry.File.Id && x.LeaseToken == lease.Token &&
                            x.State == FileOperationState.Running && x.LeaseUntil > leaseNow)
                        .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    if (fenced != 1) throw new StoreConcurrencyException();
                }
                if (transfer != null && !await PublishTransferAsync(orm, transaction, transfer, entry.File, token)) throw new StoreConcurrencyException();
                var affected = await orm.Update<FileRecord>()
                    .SetSource(entry.File)
                    .Where(x => x.Id == entry.File.Id && x.Version == expectedVersion)
                    .WithTransaction(transaction)
                    .ExecuteAffrowsAsync(token)
                    .ConfigureAwait(false);
                if (affected != 1)
                    throw new StoreConcurrencyException();

                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (operations != null)
                    foreach (var operation in operations) PrepareOperation(operation, now);
                if (priorState.HasValue && CreateTransitionEvent(entry.File, priorState.Value, now) is { } transition)
                    await orm.Insert(transition).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (entry.File.State == FileState.Ready)
                    await orm.Delete<FileUploadReservation>().Where(x => x.FileId == entry.File.Id)
                        .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);

                if (entry.Session != null)
                {
                    entry.Session.Version = nextVersion;
                    var sessionAffected = await orm.Update<FileUploadSession>()
                        .SetSource(entry.Session)
                        .Where(x => x.Id == entry.Session.Id && x.FileId == entry.File.Id)
                        .WithTransaction(transaction)
                        .ExecuteAffrowsAsync(token)
                        .ConfigureAwait(false);
                    if (sessionAffected != 1)
                        throw new InvalidOperationException("文件上传会话缺失，无法同步聚合版本。");
                }

                await orm.Delete<FileBinding>()
                    .Where(x => x.FileId == entry.File.Id)
                    .WithTransaction(transaction)
                    .ExecuteAffrowsAsync(token)
                    .ConfigureAwait(false);
                foreach (var binding in entry.Bindings)
                    await orm.Insert(binding).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);

                if (suppliedParts is { Count: > 0 })
                    await UpsertPartsAsync(orm, transaction, suppliedParts, token).ConfigureAwait(false);
                if (operations != null)
                    foreach (var operation in operations)
                        await orm.Insert(operation).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);

                return true;
            }, ct).ConfigureAwait(false);
        }
        catch (StoreConcurrencyException) { updated = false; }
        finally
        {
            entry.File.Version = originalFileVersion;
            if (entry.Session != null && originalSessionVersion.HasValue)
                entry.Session.Version = originalSessionVersion.Value;
        }
        if (updated)
        {
            entry.File.Version = nextVersion;
            if (entry.Session != null)
                entry.Session.Version = nextVersion;
        }
        return updated;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FileUploadPart>> ListPartsAsync(Guid sessionId, CancellationToken ct = default)
        => InTransactionAsync<IReadOnlyList<FileUploadPart>>(async (orm, transaction, token) =>
            await orm.Select<FileUploadPart>()
                .WithTransaction(transaction)
                .Where(x => x.SessionId == sessionId)
                .OrderBy(x => x.Number)
                .ToListAsync(token)
                .ConfigureAwait(false), ct);

    /// <inheritdoc />
    public Task<FileUploadPart?> FindPartAsync(Guid sessionId, int number, CancellationToken ct = default)
    {
        if (number <= 0)
            throw new ArgumentOutOfRangeException(nameof(number));
        return InTransactionAsync<FileUploadPart?>(async (orm, transaction, token) =>
            await orm.Select<FileUploadPart>()
                .WithTransaction(transaction)
                .Where(x => x.SessionId == sessionId && x.Number == number)
                .FirstAsync(token)
                .ConfigureAwait(false), ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FileRecord>> ListFilesAsync(string scopeKey, string? creatorId, int skip, int take,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scopeKey);
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip));
        if (take < 0)
            throw new ArgumentOutOfRangeException(nameof(take));
        return InTransactionAsync<IReadOnlyList<FileRecord>>(async (orm, transaction, token) =>
        {
            var query = orm.Select<FileRecord>().WithTransaction(transaction).Where(x => x.ScopeKey == scopeKey);
            if (creatorId != null)
                query = query.Where(x => x.CreatorId == creatorId);
            return await query.OrderByDescending(x => x.CreatedAt).OrderBy(x => x.Id)
                .Skip(skip).Limit(take).ToListAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FileRecord>> ListReadyFilesAsync(string scopeKey, string? creatorId, int skip, int take,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scopeKey);
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip));
        if (take < 0)
            throw new ArgumentOutOfRangeException(nameof(take));
        ct.ThrowIfCancellationRequested();
        if (take == 0)
            return Task.FromResult<IReadOnlyList<FileRecord>>(Array.Empty<FileRecord>());

        return InTransactionAsync<IReadOnlyList<FileRecord>>(async (orm, transaction, token) =>
        {
            var query = orm.Select<FileRecord>()
                .WithTransaction(transaction)
                .Where(x => x.ScopeKey == scopeKey && x.State == FileState.Ready);
            if (creatorId != null)
                query = query.Where(x => x.CreatorId == creatorId);
            return await query.OrderByDescending(x => x.CreatedAt).OrderBy(x => x.Id)
                .Skip(skip).Limit(take).ToListAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> ListExpiredSessionsAsync(long now, int take, CancellationToken ct = default)
    {
        ValidateTake(take);
        return InTransactionAsync<IReadOnlyList<Guid>>(async (orm, transaction, token) =>
        {
            var sessions = await orm.Select<FileUploadSession>()
                .WithTransaction(transaction)
                .Where(x => (x.State == FileState.Pending || x.State == FileState.Uploading) &&
                            (x.ExpiresAt <= now || x.AbsoluteExpiresAt <= now))
                .OrderBy(x => x.ExpiresAt)
                .Limit(take)
                .ToListAsync(token)
                .ConfigureAwait(false);
            return sessions.Select(session => session.Id).ToArray();
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FileOperation>> ListDueOperationsAsync(long now, int take, CancellationToken ct = default)
    {
        ValidateTake(take);
        return InTransactionAsync<IReadOnlyList<FileOperation>>(async (orm, transaction, token) =>
            await orm.Select<FileOperation>()
                .WithTransaction(transaction)
                .Where(x => (x.State == FileOperationState.Pending && x.NextAttemptAt <= now) ||
                            (x.State == FileOperationState.Running && x.LeaseUntil <= now))
                .OrderBy(x => x.NextAttemptAt).OrderBy(x => x.Id)
                .Limit(take)
                .ToListAsync(token)
                .ConfigureAwait(false), ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FileOperation>> ListDueOperationsAsync(long now, int take, FileOperationQueue queue,
        CancellationToken ct = default)
    {
        ValidateTake(take);
        if (queue is not (FileOperationQueue.Short or FileOperationQueue.Long))
            throw new ArgumentOutOfRangeException(nameof(queue));
        ct.ThrowIfCancellationRequested();
        if (take == 0) return Task.FromResult<IReadOnlyList<FileOperation>>(Array.Empty<FileOperation>());

        return InTransactionAsync<IReadOnlyList<FileOperation>>(async (orm, transaction, token) =>
        {
            var query = orm.Select<FileOperation>()
                .WithTransaction(transaction)
                .Where(x => (x.State == FileOperationState.Pending && x.NextAttemptAt <= now) ||
                            (x.State == FileOperationState.Running && x.LeaseUntil <= now));
            query = queue == FileOperationQueue.Long
                ? query.Where(x => x.Kind == FileOperationKind.Complete || x.Kind == FileOperationKind.Migrate || x.Kind == FileOperationKind.Inspect || x.Kind == FileOperationKind.CopyForBinding)
                : query.Where(x => x.Kind != FileOperationKind.Complete && x.Kind != FileOperationKind.Migrate && x.Kind != FileOperationKind.Inspect && x.Kind != FileOperationKind.CopyForBinding);
            return await query.OrderBy(x => x.NextAttemptAt).OrderBy(x => x.Id)
                .Limit(take)
                .ToListAsync(token)
                .ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<bool> TryClaimAsync(Guid operationId, long expectedVersion, string token, long now, long leaseUntil,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("租约令牌不能为空。", nameof(token));
        if (leaseUntil <= now)
            throw new ArgumentOutOfRangeException(nameof(leaseUntil), "租约结束时间必须晚于当前时间。");
        return InTransactionAsync(async (orm, transaction, cancellationToken) =>
        {
            var affected = await orm.Update<FileOperation>()
                .Set(x => x.LeaseToken, token)
                .Set(x => x.LeaseUntil, leaseUntil)
                .Set(x => x.State, FileOperationState.Running)
                .Set(x => x.Attempts + 1)
                .Set(x => x.Version + 1)
                .Where(x => x.Id == operationId && x.Version == expectedVersion &&
                            ((x.State == FileOperationState.Pending && x.NextAttemptAt <= now) ||
                             (x.State == FileOperationState.Running && x.LeaseUntil <= now)))
                .WithTransaction(transaction)
                .ExecuteAffrowsAsync(cancellationToken)
                .ConfigureAwait(false);
            return affected == 1;
        }, ct);
    }

    /// <inheritdoc />
    public Task<bool> RenewLeaseAsync(Guid operationId, string token, long leaseUntil, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("租约令牌不能为空。", nameof(token));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (leaseUntil <= now)
            return Task.FromResult(false);
        return InTransactionAsync(async (orm, transaction, cancellationToken) =>
        {
            var affected = await orm.Update<FileOperation>()
                .Set(x => x.LeaseUntil, leaseUntil)
                .Set(x => x.Version + 1)
                .Where(x => x.Id == operationId && x.LeaseToken == token && x.State == FileOperationState.Running &&
                            x.LeaseUntil > now)
                .WithTransaction(transaction)
                .ExecuteAffrowsAsync(cancellationToken)
                .ConfigureAwait(false);
            return affected == 1;
        }, ct);
    }

    /// <inheritdoc />
    public async Task<bool> FinishOperationAsync(Guid operationId, string token, FileOperationState state,
        long nextAttemptAt, string? error, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("租约令牌不能为空。", nameof(token));
        if (state == FileOperationState.Running)
            throw new ArgumentException("完成操作不能继续保持 Running 状态。", nameof(state));
        try { return await InTransactionAsync(async (orm, transaction, cancellationToken) =>
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var affected = await orm.Update<FileOperation>()
                .Set(x => x.State, state)
                .Set(x => x.NextAttemptAt, nextAttemptAt)
                .Set(x => x.LastError, error)
                .Set(x => x.LeaseToken, (string?)null)
                .Set(x => x.LeaseUntil, 0L)
                .Set(x => x.FinishedAt, state == FileOperationState.Pending ? 0L : now)
                .Set(x => x.Version + 1)
                .Where(x => x.Id == operationId && x.LeaseToken == token && x.State == FileOperationState.Running && x.LeaseUntil > now)
                .WithTransaction(transaction)
                .ExecuteAffrowsAsync(cancellationToken)
                .ConfigureAwait(false);
            if (affected != 1) return false;
            if (state == FileOperationState.Failed)
            {
                var operation = await orm.Select<FileOperation>().WithTransaction(transaction).Where(x => x.Id == operationId).FirstAsync(cancellationToken);
                if (operation.Kind is FileOperationKind.Initialize or FileOperationKind.Complete or FileOperationKind.Inspect)
                {
                    var entry = await FindEntryAsync(orm, transaction, operation.FileId, cancellationToken);
                    var expectedState = operation.Kind switch
                    {
                        FileOperationKind.Initialize => FileState.Pending,
                        FileOperationKind.Complete => FileState.Completing,
                        _ => FileState.Quarantined
                    };
                    if (entry?.File.State == expectedState)
                    {
                        var version = entry.File.Version;
                        var changed = await orm.Update<FileRecord>().Set(x => x.State, FileState.Failed).Set(x => x.Version + 1)
                            .Where(x => x.Id == operation.FileId && x.Version == version).WithTransaction(transaction).ExecuteAffrowsAsync(cancellationToken);
                        if (changed != 1) throw new StoreConcurrencyException();
                        if (entry.Session != null)
                            await orm.Update<FileUploadSession>().Set(x => x.State, FileState.Failed).Set(x => x.Version, version + 1)
                                .Where(x => x.FileId == operation.FileId).WithTransaction(transaction).ExecuteAffrowsAsync(cancellationToken);
                        await orm.Insert(new FileOperation { Id = Guid.NewGuid(), FileId = operation.FileId, Kind = FileOperationKind.Abort,
                            State = FileOperationState.Pending, NextAttemptAt = now, Version = 1, CreatedAt = now }).WithTransaction(transaction).ExecuteAffrowsAsync(cancellationToken);
                    }
                }
                else if (operation.Kind == FileOperationKind.CopyForBinding)
                    await FailCopyOperationAsync(orm, transaction, operation, operation.LastError, now, cancellationToken).ConfigureAwait(false);
            }
            return true;
        }, ct).ConfigureAwait(false); }
        catch (StoreConcurrencyException) { return false; }
    }

    private sealed class StoreConcurrencyException : Exception { }

    /// <inheritdoc />
    public Task<LegacyFileImport?> FindImportAsync(Guid id, CancellationToken ct = default)
        => InTransactionAsync<LegacyFileImport?>(async (orm, transaction, token) =>
            await orm.Select<LegacyFileImport>()
                .WithTransaction(transaction)
                .Where(x => x.Id == id)
                .FirstAsync(token)
                .ConfigureAwait(false), ct);

    /// <inheritdoc />
    public Task SaveImportAsync(LegacyFileImport import, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(import);
        return InTransactionAsync(async (orm, transaction, token) =>
        {
            var existing = await orm.Select<LegacyFileImport>()
                .WithTransaction(transaction)
                .Where(x => x.Id == import.Id)
                .FirstAsync(token)
                .ConfigureAwait(false);
            if (existing == null)
            {
                await orm.Insert(import).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
            }
            else if (existing.Status != "Imported")
            {
                await orm.Update<LegacyFileImport>()
                    .SetSource(import)
                    .WithTransaction(transaction)
                    .ExecuteAffrowsAsync(token)
                    .ConfigureAwait(false);
            }
            return true;
        }, ct);
    }

    private static async Task<FileEntry?> FindEntryAsync(IFreeSql orm, DbTransaction transaction, Guid fileId,
        CancellationToken ct)
    {
        var file = await orm.Select<FileRecord>()
            .WithTransaction(transaction)
            .Where(x => x.Id == fileId)
            .FirstAsync(ct)
            .ConfigureAwait(false);
        if (file == null)
            return null;

        var session = await orm.Select<FileUploadSession>()
            .WithTransaction(transaction)
            .Where(x => x.FileId == fileId)
            .FirstAsync(ct)
            .ConfigureAwait(false);
        var bindings = await orm.Select<FileBinding>()
            .WithTransaction(transaction)
            .Where(x => x.FileId == fileId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return new FileEntry { File = file, Session = session, Bindings = bindings };
    }

    private static async Task UpsertPartsAsync(IFreeSql orm, DbTransaction transaction,
        IReadOnlyList<FileUploadPart> parts, CancellationToken ct)
    {
        var sessionId = parts[0].SessionId;
        var numbers = parts.Select(part => part.Number).ToArray();
        var existing = await orm.Select<FileUploadPart>()
            .WithTransaction(transaction)
            .Where(part => part.SessionId == sessionId && Enumerable.Contains(numbers, part.Number))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var existingNumbers = existing.Select(part => part.Number).ToHashSet();
        foreach (var part in parts)
        {
            if (existingNumbers.Contains(part.Number))
            {
                await orm.Update<FileUploadPart>()
                    .SetSource(part)
                    .WithTransaction(transaction)
                    .ExecuteAffrowsAsync(ct)
                    .ConfigureAwait(false);
            }
            else
            {
                await orm.Insert(part).WithTransaction(transaction).ExecuteAffrowsAsync(ct).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> RootExistsAsync(Guid id, string requestKey, CancellationToken ct)
        => await InTransactionAsync(async (orm, transaction, token) =>
            await orm.Select<FileRecord>()
                .WithTransaction(transaction)
                .Where(x => x.Id == id || x.RequestKey == requestKey)
                .AnyAsync(token)
                .ConfigureAwait(false), ct).ConfigureAwait(false);

    private async Task<TResult> InTransactionAsync<TResult>(
        Func<IFreeSql, DbTransaction, CancellationToken, Task<TResult>> action, CancellationToken ct,
        IsolationLevel? isolationLevel = null)
    {
        ct.ThrowIfCancellationRequested();
        using var unitOfWork = _orm.CreateUnitOfWork();
        unitOfWork.IsolationLevel = isolationLevel;
        var transaction = unitOfWork.GetOrBeginTransaction();
        try
        {
            var result = await action(_orm, transaction, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            unitOfWork.Commit();
            return result;
        }
        catch (Exception exception)
        {
            try
            {
                unitOfWork.Rollback();
            }
            catch (Exception rollbackException)
            {
                exception.Data["Bing.FileManagement.RollbackException"] = rollbackException;
            }
            throw;
        }
    }

    private static void ValidateEntry(FileEntry entry, IReadOnlyList<FileOperation>? operations)
    {
        if (entry.File == null)
            throw new ArgumentException("文件记录不能为空。", nameof(entry));
        if (entry.Bindings == null)
            throw new ArgumentException("文件关联集合不能为空。", nameof(entry));
        if (entry.Session != null && entry.Session.FileId != entry.File.Id)
            throw new ArgumentException("上传会话不属于该文件。", nameof(entry));
        if (entry.Bindings.Any(binding => binding.FileId != entry.File.Id))
            throw new ArgumentException("关联记录不属于该文件。", nameof(entry));
        if (operations?.Any(operation => operation.FileId != entry.File.Id) == true)
            throw new ArgumentException("操作记录不属于该文件。", nameof(operations));
    }

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            var type = current.GetType();
            var name = type.FullName ?? type.Name;
            if (name.Contains("SqliteException", StringComparison.Ordinal) ||
                name.Contains("SQLiteException", StringComparison.Ordinal))
            {
                var code = ReadIntProperty(current, "SqliteExtendedErrorCode") ??
                           ReadIntProperty(current, "SqliteErrorCode") ??
                           ReadIntProperty(current, "ExtendedErrorCode") ??
                           ReadIntProperty(current, "ErrorCode");
                if (code is 19 or 1555 or 2067)
                    return true;
            }
            if (name.Contains("MySqlException", StringComparison.Ordinal) && ReadIntProperty(current, "Number") == 1062)
                return true;
            if (name.Contains("PostgresException", StringComparison.Ordinal) &&
                string.Equals(ReadStringProperty(current, "SqlState"), "23505", StringComparison.Ordinal))
                return true;
            if (ReadIntProperty(current, "Number") is 2601 or 2627)
                return true;
            if (name.Contains("OracleException", StringComparison.Ordinal) && ReadIntProperty(current, "Number") == 1)
                return true;
        }
        return false;
    }

    private static int? ReadIntProperty(Exception exception, string propertyName)
    {
        try
        {
            return exception.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(exception) is { } value ? Convert.ToInt32(value) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadStringProperty(Exception exception, string propertyName)
    {
        try
        {
            return exception.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(exception) as string;
        }
        catch
        {
            return null;
        }
    }

    private static void ValidateTake(int take)
    {
        if (take < 0)
            throw new ArgumentOutOfRangeException(nameof(take));
    }
}
