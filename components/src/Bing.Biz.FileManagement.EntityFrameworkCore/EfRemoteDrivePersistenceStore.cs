using System.Data;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Microsoft.EntityFrameworkCore;

namespace Bing.Biz.FileManagement.EntityFrameworkCore;

public sealed partial class EfFileManagementStore : IRemoteDriveStateStore, IFileStorageTargetStore
{
    public async Task<RemoteDriveConnection?> FindConnectionAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<RemoteDriveConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<RemoteDriveConnection>> ListConnectionsAsync(string scopeKey, string? ownerId,
        bool includeShared, int skip, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take < 0) throw new ArgumentOutOfRangeException(nameof(take));
        if (take == 0) return Array.Empty<RemoteDriveConnection>();
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.Set<RemoteDriveConnection>().AsNoTracking().Where(x => x.ScopeKey == scopeKey);
        if (ownerId != null)
            query = query.Where(x => x.Ownership == RemoteDriveOwnership.Personal && x.OwnerId == ownerId ||
                                     includeShared && x.Ownership == RemoteDriveOwnership.Shared);
        else if (!includeShared)
            query = query.Where(x => x.Ownership == RemoteDriveOwnership.Shared);
        return await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<bool> TryCreateConnectionAsync(RemoteDriveConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.Id == Guid.Empty || connection.Version != 1) return false;
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Add(connection);
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception)) { return false; }
    }

    public async Task<bool> TryUpdateConnectionAsync(RemoteDriveConnection connection, long version, string? leaseToken = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (version < 1 || connection.Id == Guid.Empty || connection.Version != version) return false;
        var nextVersion = checked(version + 1);
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.Set<RemoteDriveConnection>().SingleOrDefaultAsync(x => x.Id == connection.Id, ct);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (current == null || current.Version != version || !SameConnectionIdentity(current, connection) ||
            leaseToken != null && (current.LeaseToken != leaseToken || current.LeaseUntil <= now) ||
            connection.LeaseToken != null && connection.LeaseToken != current.LeaseToken)
            return false;
        db.Entry(current).CurrentValues.SetValues(connection);
        current.Version = nextVersion;
        db.Entry(current).Property(x => x.Version).OriginalValue = version;
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            connection.Version = nextVersion;
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

    public async Task<bool> TryClaimConnectionAsync(Guid id, long version, string token, long until, CancellationToken ct = default)
    {
        ValidateClaim(version, token, until);
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.Set<RemoteDriveConnection>().SingleOrDefaultAsync(x => x.Id == id, ct);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (current == null || current.Version != version || current.State == RemoteDriveConnectionState.Disabled ||
            current.LeaseToken != null && current.LeaseUntil > now) return false;
        current.LeaseToken = token;
        current.LeaseUntil = until;
        current.Version = checked(version + 1);
        db.Entry(current).Property(x => x.Version).OriginalValue = version;
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); return false; }
    }

    public async Task<RemoteDriveObject?> FindObjectAsync(string id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<RemoteDriveObject>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<bool> TryDeleteObjectAsync(string id, long version, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id) || version < 1) return false;
        await using var db = await _factory.CreateDbContextAsync(ct);
        var value = await db.Set<RemoteDriveObject>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value == null || value.Deleted || value.Version != version) return false;
        value.Deleted = true;
        value.Version = checked(version + 1);
        db.Entry(value).Property(x => x.Version).OriginalValue = version;
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task<RemoteDriveTransfer?> FindTransferAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<RemoteDriveTransfer>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<bool> TryCreateTransferAsync(RemoteDriveTransfer transfer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (transfer.Id == Guid.Empty || transfer.Version != 1) return false;
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var connection = await db.Set<RemoteDriveConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == transfer.ConnectionId, ct);
        if (connection == null || connection.State != RemoteDriveConnectionState.Active || connection.StoreName != transfer.StoreName ||
            connection.ScopeKey != transfer.ScopeKey) return false;
        db.Add(transfer);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true; }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        { await transaction.RollbackAsync(ct); return false; }
    }

    public async Task<bool> TryUpdateTransferAsync(RemoteDriveTransfer transfer, long version, string? leaseToken = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (version < 1 || transfer.Id == Guid.Empty || transfer.Version != version) return false;
        var nextVersion = checked(version + 1);
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.Set<RemoteDriveTransfer>().SingleOrDefaultAsync(x => x.Id == transfer.Id, ct);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (current == null || current.Version != version || !SameTransferIdentity(current, transfer) ||
            leaseToken != null && (current.LeaseToken != leaseToken || current.LeaseUntil <= now) ||
            transfer.LeaseToken != null && transfer.LeaseToken != current.LeaseToken)
            return false;
        // 心跳独立续租；业务快照不能覆盖租约，仅持有者可以显式释放。
        var releaseLease = leaseToken != null && transfer.LeaseToken == null && transfer.LeaseUntil == 0;
        var entry = db.Entry(current);
        entry.CurrentValues.SetValues(transfer);
        if (!releaseLease)
        {
            entry.Property(x => x.LeaseToken).CurrentValue = entry.Property(x => x.LeaseToken).OriginalValue;
            entry.Property(x => x.LeaseUntil).CurrentValue = entry.Property(x => x.LeaseUntil).OriginalValue;
            entry.Property(x => x.LeaseToken).IsModified = false;
            entry.Property(x => x.LeaseUntil).IsModified = false;
        }
        current.Version = nextVersion;
        db.Entry(current).Property(x => x.Version).OriginalValue = version;
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            transfer.Version = nextVersion;
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }
    }

    public async Task<bool> TryClaimTransferAsync(Guid id, long version, string token, long until, CancellationToken ct = default)
    {
        ValidateClaim(version, token, until);
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.Set<RemoteDriveTransfer>().SingleOrDefaultAsync(x => x.Id == id, ct);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (current == null || current.Version != version || current.State == RemoteDriveTransferState.Completed ||
            current.State == RemoteDriveTransferState.Aborted && current.CleanupCompleted ||
            current.LeaseToken != null && current.LeaseUntil > now) return false;
        current.LeaseToken = token;
        current.LeaseUntil = until;
        current.Version = checked(version + 1);
        db.Entry(current).Property(x => x.Version).OriginalValue = version;
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); return false; }
    }

    public async Task<bool> RenewTransferAsync(Guid id, string token, long until, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        // Version 同时保护业务检查点。同一持有者的检查点或发布可能先提交，
        // 因而版本冲突后重新读取并核对所有权；不能将正常业务推进直接视为失租。
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (until <= now) return false;
            await using var db = await _factory.CreateDbContextAsync(ct);
            var transfer = await db.Set<RemoteDriveTransfer>().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (transfer == null || transfer.LeaseToken != token || transfer.LeaseUntil <= now ||
                transfer.State is RemoteDriveTransferState.Completed or RemoteDriveTransferState.Aborted) return false;
            transfer.LeaseUntil = Math.Max(transfer.LeaseUntil, until);
            db.Entry(transfer).Property(x => x.Version).IsModified = false;
            try { await db.SaveChangesAsync(ct); return true; }
            catch (DbUpdateConcurrencyException) { /* 仅重试版本竞争；每次重读租约和终态，次数有界。 */ }
        }
        return false;
    }

    public async Task<bool> TryPublishAsync(RemoteDriveObject value, long expectedObjectVersion, RemoteDriveTransfer transfer,
        long transferVersion, string leaseToken, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(transfer);
        if (expectedObjectVersion < 0 || transferVersion < 1 || transfer.Version != transferVersion || string.IsNullOrWhiteSpace(leaseToken) ||
            value.Id != RemoteDriveObject.Key(transfer.StoreName, transfer.ScopeKey, new FileAddress(transfer.Container, transfer.ObjectKey)) ||
            value.TransferId != transfer.Id || value.ConnectionId != transfer.ConnectionId || value.StoreName != transfer.StoreName ||
            value.ScopeKey != transfer.ScopeKey || value.Container != transfer.Container || value.ObjectKey != transfer.ObjectKey ||
            value.Length != transfer.Length || value.MetadataJson != transfer.MetadataJson || transfer.ExpectedObjectVersion != expectedObjectVersion ||
            transfer.State != RemoteDriveTransferState.Verified)
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var nextTransferVersion = checked(transferVersion + 1);
        var originalValueVersion = value.Version;
        var originalTransferVersion = transfer.Version;
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var persistedTransfer = await db.Set<RemoteDriveTransfer>().SingleOrDefaultAsync(x => x.Id == transfer.Id, ct);
            var connection = await db.Set<RemoteDriveConnection>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == transfer.ConnectionId, ct);
            if (persistedTransfer == null || persistedTransfer.Version != transferVersion || persistedTransfer.LeaseToken != leaseToken ||
                persistedTransfer.LeaseUntil <= now || persistedTransfer.State is not (RemoteDriveTransferState.Uploading or RemoteDriveTransferState.Verified) || connection == null ||
                connection.State != RemoteDriveConnectionState.Active || persistedTransfer.ConnectionId != value.ConnectionId ||
                !SameTransferIdentity(persistedTransfer, transfer))
                return false;

            var persistedObject = await db.Set<RemoteDriveObject>().SingleOrDefaultAsync(x => x.Id == value.Id, ct);
            if (expectedObjectVersion == 0 ? persistedObject != null : persistedObject == null || persistedObject.Version != expectedObjectVersion)
                return false;

            var transferEntry = db.Entry(persistedTransfer);
            transferEntry.CurrentValues.SetValues(transfer);
            // 发布与检查点遵循相同规则，租约由 claim / renew / release 单独维护。
            transferEntry.Property(x => x.LeaseToken).CurrentValue = transferEntry.Property(x => x.LeaseToken).OriginalValue;
            transferEntry.Property(x => x.LeaseUntil).CurrentValue = transferEntry.Property(x => x.LeaseUntil).OriginalValue;
            transferEntry.Property(x => x.LeaseToken).IsModified = false;
            transferEntry.Property(x => x.LeaseUntil).IsModified = false;
            persistedTransfer.Version = nextTransferVersion;
            db.Entry(persistedTransfer).Property(x => x.Version).OriginalValue = transferVersion;
            if (persistedObject == null)
            {
                value.Version = 1;
                db.Add(value);
            }
            else
            {
                db.Entry(persistedObject).CurrentValues.SetValues(value);
                persistedObject.Version = checked(expectedObjectVersion + 1);
                db.Entry(persistedObject).Property(x => x.Version).OriginalValue = expectedObjectVersion;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            transfer.Version = nextTransferVersion;
            value.Version = expectedObjectVersion == 0 ? 1 : checked(expectedObjectVersion + 1);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            value.Version = originalValueVersion;
            transfer.Version = originalTransferVersion;
            return false;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(ct);
            value.Version = originalValueVersion;
            transfer.Version = originalTransferVersion;
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            value.Version = originalValueVersion;
            transfer.Version = originalTransferVersion;
            throw;
        }
    }

    public async Task<bool> TryDeferCleanupAsync(Guid id, long version, long now, long nextAttemptAt, string errorCode, CancellationToken ct = default)
    {
        if (version < 1 || nextAttemptAt <= now || string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > 64)
            throw new ArgumentException("清理重试时间或错误分类无效。");
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.Set<RemoteDriveTransfer>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (current == null || current.Version != version || current.CleanupCompleted || current.State == RemoteDriveTransferState.Completed ||
            current.LeaseToken != null && current.LeaseUntil > now ||
            current.State is not (RemoteDriveTransferState.Verified or RemoteDriveTransferState.Aborted) && current.ExpiresAt > now) return false;
        current.CleanupNextAttemptAt = nextAttemptAt;
        current.CleanupErrorCode = errorCode;
        current.Version = checked(version + 1);
        db.Entry(current).Property(x => x.Version).OriginalValue = version;
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); return false; }
    }

    public async Task<IReadOnlyList<RemoteDriveTransfer>> ListCleanupAsync(long now, int take, CancellationToken ct = default)
    {
        if (take < 0) throw new ArgumentOutOfRangeException(nameof(take));
        if (take == 0) return Array.Empty<RemoteDriveTransfer>();
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<RemoteDriveTransfer>().AsNoTracking().Where(x => !x.CleanupCompleted && x.CleanupNextAttemptAt <= now &&
                (x.State == RemoteDriveTransferState.Verified || x.State == RemoteDriveTransferState.Aborted ||
                 x.State != RemoteDriveTransferState.Completed && x.ExpiresAt <= now) &&
                (x.LeaseToken == null || x.LeaseUntil <= now))
            .OrderBy(x => x.CleanupNextAttemptAt).ThenBy(x => x.ExpiresAt).ThenBy(x => x.Id).Take(take).ToListAsync(ct);
    }

    public async Task<FileStoragePreference?> FindPreferenceAsync(string scopeKey, string ownerId, string container,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileStoragePreference>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ScopeKey == scopeKey && x.OwnerId == ownerId && x.Container == container, ct);
    }

    public async Task<bool> SavePreferenceAsync(FileStoragePreference preference, long expectedVersion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preference);
        if (expectedVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.Set<FileStoragePreference>().SingleOrDefaultAsync(x => x.ScopeKey == preference.ScopeKey &&
            x.OwnerId == preference.OwnerId && x.Container == preference.Container, ct);
        if (expectedVersion == 0)
        {
            if (current != null) return false;
            preference.Version = 1;
            db.Add(preference);
        }
        else
        {
            if (current == null || current.Version != expectedVersion || current.Id != preference.Id) return false;
            db.Entry(current).CurrentValues.SetValues(preference);
            current.Version = checked(expectedVersion + 1);
            db.Entry(current).Property(x => x.Version).OriginalValue = expectedVersion;
        }
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            preference.Version = expectedVersion == 0 ? 1 : checked(expectedVersion + 1);
            return true;
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); return false; }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        { await transaction.RollbackAsync(ct); return false; }
    }

    public async Task<FileBindingCopyRequest?> FindCopyAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileBindingCopyRequest>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<FileBindingCopyRequest?> FindCopyRequestAsync(string requestKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requestKey);
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileBindingCopyRequest>().AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == requestKey, ct);
    }

    public async Task<bool> CreateCopyAsync(FileBindingCopyRequest request, FileEntry target, FileOperation operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(operation);
        if (target.File.Id != request.CopyFileId || operation.FileId != request.CopyFileId || operation.Kind != FileOperationKind.CopyForBinding ||
            target.File.State != FileState.Pending || target.File.Ownership != RemoteDriveOwnership.Shared || target.Session != null || target.Bindings.Count != 0)
            throw new ArgumentException("复制请求、私有目标和后台任务不一致。");
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (await db.Set<FileBindingCopyRequest>().AnyAsync(x => x.Id == request.Id || x.RequestKey == request.RequestKey ||
                    x.CopyFileId == request.CopyFileId, ct) ||
                await db.Set<FileRecord>().AnyAsync(x => x.Id == target.File.Id || x.RequestKey == target.File.RequestKey, ct) ||
                await db.Set<FileOperation>().AnyAsync(x => x.Id == operation.Id, ct)) return false;
            var source = await db.Set<FileRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.SourceFileId, ct);
            if (!CopySourceMatches(source, request)) return false;
            operation.Version = Math.Max(1, operation.Version);
            PrepareOperation(operation, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            request.Version = 1;
            db.Add(request); db.Add(target.File); db.AddRange(target.Bindings); db.Add(operation);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); return false; }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        { await transaction.RollbackAsync(ct); return false; }
    }

    public async Task<bool> UpdateCopyAsync(FileBindingCopyRequest request, long expectedVersion, FileEntry target, long targetVersion,
        IReadOnlyList<FileOperation>? operations = null, FileOperationLease? operationLease = null, FileTransferLease? transferLease = null,
        bool validateSource = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(target);
        if (expectedVersion < 1 || targetVersion < 1 || request.Version != expectedVersion || target.File.Id != request.CopyFileId ||
            target.File.Version != targetVersion ||
            operations?.Any(x => x.FileId != target.File.Id) == true) return false;
        var originalRequestVersion = request.Version;
        var originalTargetVersion = target.File.Version;
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var currentRequest = await db.Set<FileBindingCopyRequest>().SingleOrDefaultAsync(x => x.Id == request.Id, ct);
            var currentFile = await db.Set<FileRecord>().SingleOrDefaultAsync(x => x.Id == request.CopyFileId, ct);
            if (currentRequest == null || currentRequest.Version != expectedVersion || !SameCopyIdentity(currentRequest, request) ||
                currentFile == null || currentFile.Version != targetVersion) return false;
            if (!CanTransitionCopy(currentRequest.State, request.State)) return false;
            if (validateSource)
            {
                var source = await db.Set<FileRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == currentRequest.SourceFileId, ct);
                if (!CopySourceMatches(source, currentRequest)) return false;
            }
            if (operationLease != null)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var operation = await db.Set<FileOperation>().SingleOrDefaultAsync(x => x.Id == operationLease.OperationId, ct);
                if (operation == null || operation.FileId != currentFile.Id || operation.LeaseToken != operationLease.Token ||
                    operation.State != FileOperationState.Running || operation.LeaseUntil <= now) return false;
                operation.Version++;
                await db.SaveChangesAsync(ct);
            }
            if (transferLease != null && !await PublishTransferAsync(db, transferLease, target.File, ct)) return false;

            var priorState = currentFile.State;
            db.Entry(currentRequest).CurrentValues.SetValues(request);
            currentRequest.Version = checked(expectedVersion + 1);
            db.Entry(currentRequest).Property(x => x.Version).OriginalValue = expectedVersion;
            db.Entry(currentFile).CurrentValues.SetValues(target.File);
            currentFile.Version = checked(targetVersion + 1);
            db.Entry(currentFile).Property(x => x.Version).OriginalValue = targetVersion;
            if (target.Session != null)
            {
                var session = await db.Set<FileUploadSession>().SingleOrDefaultAsync(x => x.FileId == target.File.Id, ct);
                if (session == null) return false;
                db.Entry(session).CurrentValues.SetValues(target.Session);
                session.Version = currentFile.Version;
            }
            var existingBindings = await db.Set<FileBinding>().Where(x => x.FileId == target.File.Id).ToListAsync(ct);
            var wanted = target.Bindings.ToDictionary(x => x.Id);
            foreach (var old in existingBindings)
            {
                if (!wanted.Remove(old.Id, out var updated)) db.Remove(old);
                else db.Entry(old).CurrentValues.SetValues(updated);
            }
            db.AddRange(wanted.Values);
            var nowForEvents = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (CreateTransitionEvent(target.File, priorState, nowForEvents) is { } transition) db.Add(transition);
            if (operations != null)
            {
                foreach (var operation in operations) PrepareOperation(operation, nowForEvents);
                db.AddRange(operations);
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            request.Version = checked(expectedVersion + 1);
            target.File.Version = checked(targetVersion + 1);
            if (target.Session != null) target.Session.Version = target.File.Version;
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct); request.Version = originalRequestVersion; target.File.Version = originalTargetVersion; return false;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(ct); request.Version = originalRequestVersion; target.File.Version = originalTargetVersion; return false;
        }
    }

    private static bool SameConnectionIdentity(RemoteDriveConnection old, RemoteDriveConnection value) =>
        old.Id == value.Id && old.TemplateName == value.TemplateName && old.Provider == value.Provider && old.StoreName == value.StoreName &&
        old.ScopeKind == value.ScopeKind && old.TenantId == value.TenantId && old.ScopeKey == value.ScopeKey && old.Ownership == value.Ownership &&
        old.OwnerId == value.OwnerId && old.DisplayName == value.DisplayName && old.RootFolderId == value.RootFolderId && old.CreatedAt == value.CreatedAt &&
        (old.RemoteAccountId == value.RemoteAccountId || old.RemoteAccountId.Length == 0 && value.RemoteAccountId.Length > 0) &&
        (old.DriveId == value.DriveId || old.DriveId.Length == 0 && value.DriveId.Length > 0);

    private static bool SameTransferIdentity(RemoteDriveTransfer old, RemoteDriveTransfer value) =>
        old.Id == value.Id && old.ConnectionId == value.ConnectionId && old.StoreName == value.StoreName && old.ScopeKey == value.ScopeKey &&
        old.Container == value.Container && old.ObjectKey == value.ObjectKey && old.StageStoreName == value.StageStoreName &&
        old.StageContainer == value.StageContainer && old.StageObjectKey == value.StageObjectKey && old.ExpectedObjectVersion == value.ExpectedObjectVersion &&
        old.Length == value.Length && old.PartSize == value.PartSize && old.ContentType == value.ContentType && old.CreatedAt == value.CreatedAt;

    private static bool SameCopyIdentity(FileBindingCopyRequest old, FileBindingCopyRequest value) =>
        old.Id == value.Id && old.RequestKey == value.RequestKey && old.ScopeKey == value.ScopeKey && old.OwnerId == value.OwnerId &&
        old.SourceFileId == value.SourceFileId && old.SourceVersion == value.SourceVersion && old.CopyFileId == value.CopyFileId &&
        old.BusinessType == value.BusinessType && old.BusinessId == value.BusinessId && old.CreatedAt == value.CreatedAt;

    private static bool CopySourceMatches(FileRecord? source, FileBindingCopyRequest request) => source != null &&
        source.Version == request.SourceVersion && source.State == FileState.Ready && source.Ownership == RemoteDriveOwnership.Personal &&
        source.CreatorId == request.OwnerId && source.ScopeKey == request.ScopeKey;

    private static bool CanTransitionCopy(FileBindingCopyState current, FileBindingCopyState next) => current == next || current switch
    {
        FileBindingCopyState.Pending => next is FileBindingCopyState.Copied or FileBindingCopyState.Cancelled or FileBindingCopyState.Failed,
        FileBindingCopyState.Copied => next is FileBindingCopyState.Bound or FileBindingCopyState.Cancelled,
        _ => false
    };

    private static void ValidateClaim(long version, string token, long until)
    {
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        if (until <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) throw new ArgumentOutOfRangeException(nameof(until));
    }

    private static async Task FailCopyOperationAsync(FileManagementDbContext db, FileOperation operation, string? error,
        long now, CancellationToken ct)
    {
        if (!Guid.TryParseExact(operation.Payload, "N", out var requestId)) return;
        var request = await db.Set<FileBindingCopyRequest>().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request == null || request.State != FileBindingCopyState.Pending) return;
        request.State = FileBindingCopyState.Failed;
        request.ErrorCode = error ?? "copy_failed";
        request.Version++;
        var file = await db.Set<FileRecord>().SingleOrDefaultAsync(x => x.Id == request.CopyFileId, ct);
        if (file?.State == FileState.Pending)
        {
            // 请求保留失败原因；副本进入 Delete 接受的状态，由正常 Worker 幂等回收。
            file.State = FileState.DeletePending;
            file.Version++;
            db.Add(new FileOperation { Id = Guid.NewGuid(), FileId = file.Id, Kind = FileOperationKind.Delete,
                State = FileOperationState.Pending, NextAttemptAt = now, Version = 1, CreatedAt = now });
        }
    }
}
