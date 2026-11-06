using System.Data.Common;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using FreeSql;

namespace Bing.Biz.FileManagement.FreeSql;

public sealed partial class FreeSqlFileManagementStore : IRemoteDriveStateStore, IFileStorageTargetStore
{
    public Task<RemoteDriveConnection?> FindConnectionAsync(Guid id, CancellationToken ct = default)
        => InTransactionAsync<RemoteDriveConnection?>(async (orm, transaction, token) =>
            await orm.Select<RemoteDriveConnection>().WithTransaction(transaction).Where(x => x.Id == id).FirstAsync(token).ConfigureAwait(false), ct);

    public Task<IReadOnlyList<RemoteDriveConnection>> ListConnectionsAsync(string scopeKey, string? ownerId,
        bool includeShared, int skip, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take < 0) throw new ArgumentOutOfRangeException(nameof(take));
        if (take == 0) return Task.FromResult<IReadOnlyList<RemoteDriveConnection>>(Array.Empty<RemoteDriveConnection>());
        return InTransactionAsync<IReadOnlyList<RemoteDriveConnection>>(async (orm, transaction, token) =>
        {
            var query = orm.Select<RemoteDriveConnection>().WithTransaction(transaction).Where(x => x.ScopeKey == scopeKey);
            if (ownerId != null)
                query = query.Where(x => x.Ownership == RemoteDriveOwnership.Personal && x.OwnerId == ownerId ||
                                         includeShared && x.Ownership == RemoteDriveOwnership.Shared);
            else if (!includeShared)
                query = query.Where(x => x.Ownership == RemoteDriveOwnership.Shared);
            return await query.OrderBy(x => x.CreatedAt).OrderBy(x => x.Id).Skip(skip).Limit(take).ToListAsync(token).ConfigureAwait(false);
        }, ct);
    }

    public async Task<bool> TryCreateConnectionAsync(RemoteDriveConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.Id == Guid.Empty || connection.Version != 1) return false;
        try { return await _orm.Insert(connection).ExecuteAffrowsAsync(ct).ConfigureAwait(false) == 1; }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { return false; }
    }

    public async Task<bool> TryUpdateConnectionAsync(RemoteDriveConnection connection, long version, string? leaseToken = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (version < 1 || connection.Id == Guid.Empty || connection.Version != version) return false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var nextVersion = checked(version + 1);
        var originalVersion = connection.Version;
        connection.Version = nextVersion;
        bool updated;
        try { updated = await InTransactionAsync(async (orm, transaction, token) =>
        {
            var current = await orm.Select<RemoteDriveConnection>().WithTransaction(transaction)
                .Where(x => x.Id == connection.Id).FirstAsync(token).ConfigureAwait(false);
            if (current == null || current.Version != version || !SameConnectionIdentity(current, connection) ||
                leaseToken != null && (current.LeaseToken != leaseToken || current.LeaseUntil <= now) ||
                connection.LeaseToken != null && connection.LeaseToken != current.LeaseToken) return false;
            var query = orm.Update<RemoteDriveConnection>().SetSource(connection)
                .Where(x => x.Id == connection.Id && x.Version == version);
            if (leaseToken != null) query = query.Where(x => x.LeaseToken == leaseToken && x.LeaseUntil > now);
            return await query.WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false) == 1;
        }, ct).ConfigureAwait(false); }
        catch { connection.Version = originalVersion; throw; }
        if (!updated) connection.Version = originalVersion;
        return updated;
    }

    public Task<bool> TryClaimConnectionAsync(Guid id, long version, string token, long until, CancellationToken ct = default)
    {
        ValidateClaim(version, token, until);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return InTransactionAsync(async (orm, transaction, cancellationToken) =>
            await orm.Update<RemoteDriveConnection>().Set(x => x.LeaseToken, token).Set(x => x.LeaseUntil, until).Set(x => x.Version + 1)
                .Where(x => x.Id == id && x.Version == version && x.State != RemoteDriveConnectionState.Disabled &&
                    (x.LeaseToken == null || x.LeaseUntil <= now))
                .WithTransaction(transaction).ExecuteAffrowsAsync(cancellationToken).ConfigureAwait(false) == 1, ct);
    }

    public Task<RemoteDriveObject?> FindObjectAsync(string id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return InTransactionAsync<RemoteDriveObject?>(async (orm, transaction, token) =>
            await orm.Select<RemoteDriveObject>().WithTransaction(transaction).Where(x => x.Id == id).FirstAsync(token).ConfigureAwait(false), ct);
    }

    public Task<bool> TryDeleteObjectAsync(string id, long version, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id) || version < 1) return Task.FromResult(false);
        return InTransactionAsync(async (orm, transaction, token) =>
            await orm.Update<RemoteDriveObject>().Set(x => x.Deleted, true).Set(x => x.Version + 1)
                .Where(x => x.Id == id && x.Version == version && !x.Deleted)
                .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false) == 1, ct);
    }

    public Task<RemoteDriveTransfer?> FindTransferAsync(Guid id, CancellationToken ct = default)
        => InTransactionAsync<RemoteDriveTransfer?>(async (orm, transaction, token) =>
            await orm.Select<RemoteDriveTransfer>().WithTransaction(transaction).Where(x => x.Id == id).FirstAsync(token).ConfigureAwait(false), ct);

    public async Task<bool> TryCreateTransferAsync(RemoteDriveTransfer transfer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (transfer.Id == Guid.Empty || transfer.Version != 1) return false;
        try
        {
            return await InTransactionAsync(async (orm, transaction, token) =>
            {
                var connection = await orm.Select<RemoteDriveConnection>().WithTransaction(transaction)
                    .Where(x => x.Id == transfer.ConnectionId).FirstAsync(token).ConfigureAwait(false);
                if (connection == null || connection.State != RemoteDriveConnectionState.Active ||
                    connection.StoreName != transfer.StoreName || connection.ScopeKey != transfer.ScopeKey) return false;
                return await orm.Insert(transfer).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false) == 1;
            }, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { return false; }
    }

    public async Task<bool> TryUpdateTransferAsync(RemoteDriveTransfer transfer, long version, string? leaseToken = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (version < 1 || transfer.Id == Guid.Empty || transfer.Version != version) return false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var nextVersion = checked(version + 1);
        var originalVersion = transfer.Version;
        transfer.Version = nextVersion;
        bool updated;
        try { updated = await InTransactionAsync(async (orm, transaction, token) =>
        {
            var current = await orm.Select<RemoteDriveTransfer>().WithTransaction(transaction)
                .Where(x => x.Id == transfer.Id).FirstAsync(token).ConfigureAwait(false);
            if (current == null || current.Version != version || !SameTransferIdentity(current, transfer) ||
                leaseToken != null && (current.LeaseToken != leaseToken || current.LeaseUntil <= now) ||
                transfer.LeaseToken != null && transfer.LeaseToken != current.LeaseToken) return false;
            var query = orm.Update<RemoteDriveTransfer>().SetSource(transfer)
                .Where(x => x.Id == transfer.Id && x.Version == version);
            // 不从旧业务快照写回心跳字段；显式释放仍受有效令牌和版本约束。
            var releaseLease = leaseToken != null && transfer.LeaseToken == null && transfer.LeaseUntil == 0;
            if (!releaseLease) query = query.IgnoreColumns(x => new { x.LeaseToken, x.LeaseUntil });
            if (leaseToken != null) query = query.Where(x => x.LeaseToken == leaseToken && x.LeaseUntil > now);
            return await query.WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false) == 1;
        }, ct).ConfigureAwait(false); }
        catch { transfer.Version = originalVersion; throw; }
        if (!updated) transfer.Version = originalVersion;
        return updated;
    }

    public Task<bool> TryClaimTransferAsync(Guid id, long version, string token, long until, CancellationToken ct = default)
    {
        ValidateClaim(version, token, until);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return InTransactionAsync(async (orm, transaction, cancellationToken) =>
            await orm.Update<RemoteDriveTransfer>().Set(x => x.LeaseToken, token).Set(x => x.LeaseUntil, until).Set(x => x.Version + 1)
                .Where(x => x.Id == id && x.Version == version && x.State != RemoteDriveTransferState.Completed &&
                    (x.State != RemoteDriveTransferState.Aborted || !x.CleanupCompleted) && (x.LeaseToken == null || x.LeaseUntil <= now))
                .WithTransaction(transaction).ExecuteAffrowsAsync(cancellationToken).ConfigureAwait(false) == 1, ct);
    }

    public Task<bool> RenewTransferAsync(Guid id, string token, long until, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("租约令牌不能为空。", nameof(token));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (until <= now) return Task.FromResult(false);
        return InTransactionAsync(async (orm, transaction, cancellationToken) =>
            await orm.Update<RemoteDriveTransfer>().Set(x => x.LeaseUntil, until)
                .Where(x => x.Id == id && x.LeaseToken == token && x.LeaseUntil > now &&
                    x.State != RemoteDriveTransferState.Completed && x.State != RemoteDriveTransferState.Aborted)
                .WithTransaction(transaction).ExecuteAffrowsAsync(cancellationToken).ConfigureAwait(false) == 1, ct);
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
            transfer.State != RemoteDriveTransferState.Verified) return false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var nextTransferVersion = checked(transferVersion + 1);
        var originalObjectVersion = value.Version;
        var originalTransferVersion = transfer.Version;
        try
        {
            var published = await InTransactionAsync(async (orm, transaction, token) =>
            {
                var currentTransfer = await orm.Select<RemoteDriveTransfer>().WithTransaction(transaction)
                    .Where(x => x.Id == transfer.Id).FirstAsync(token).ConfigureAwait(false);
                var connection = await orm.Select<RemoteDriveConnection>().WithTransaction(transaction)
                    .Where(x => x.Id == transfer.ConnectionId).FirstAsync(token).ConfigureAwait(false);
                if (currentTransfer == null || currentTransfer.Version != transferVersion || currentTransfer.LeaseToken != leaseToken ||
                    currentTransfer.LeaseUntil <= now || currentTransfer.State is not (RemoteDriveTransferState.Uploading or RemoteDriveTransferState.Verified) || connection == null ||
                    connection.State != RemoteDriveConnectionState.Active || currentTransfer.ConnectionId != value.ConnectionId ||
                    !SameTransferIdentity(currentTransfer, transfer)) return false;
                var currentObject = await orm.Select<RemoteDriveObject>().WithTransaction(transaction).Where(x => x.Id == value.Id)
                    .FirstAsync(token).ConfigureAwait(false);
                if (expectedObjectVersion == 0 ? currentObject != null : currentObject == null || currentObject.Version != expectedObjectVersion)
                    return false;

                transfer.Version = nextTransferVersion;
                var transferRows = await orm.Update<RemoteDriveTransfer>().SetSource(transfer)
                    .IgnoreColumns(x => new { x.LeaseToken, x.LeaseUntil })
                    .Where(x => x.Id == transfer.Id && x.Version == transferVersion && x.LeaseToken == leaseToken &&
                        x.LeaseUntil > now && (x.State == RemoteDriveTransferState.Uploading || x.State == RemoteDriveTransferState.Verified))
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (transferRows != 1) throw new StoreConcurrencyException();
                value.Version = expectedObjectVersion == 0 ? 1 : checked(expectedObjectVersion + 1);
                if (currentObject == null)
                    await orm.Insert(value).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                else
                {
                    var objectRows = await orm.Update<RemoteDriveObject>().SetSource(value)
                        .Where(x => x.Id == value.Id && x.Version == expectedObjectVersion)
                        .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    if (objectRows != 1) throw new StoreConcurrencyException();
                }
                return true;
            }, ct).ConfigureAwait(false);
            if (!published)
            {
                value.Version = originalObjectVersion;
                transfer.Version = originalTransferVersion;
                return false;
            }
            return true;
        }
        catch (StoreConcurrencyException)
        {
            value.Version = originalObjectVersion;
            transfer.Version = originalTransferVersion;
            return false;
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception))
        {
            value.Version = originalObjectVersion;
            transfer.Version = originalTransferVersion;
            return false;
        }
        catch
        {
            value.Version = originalObjectVersion;
            transfer.Version = originalTransferVersion;
            throw;
        }
    }

    public Task<bool> TryDeferCleanupAsync(Guid id, long version, long now, long nextAttemptAt, string errorCode, CancellationToken ct = default)
    {
        if (version < 1 || nextAttemptAt <= now || string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > 64)
            throw new ArgumentException("清理重试时间或错误分类无效。");
        return InTransactionAsync(async (orm, transaction, token) =>
            await orm.Update<RemoteDriveTransfer>().Set(x => x.CleanupNextAttemptAt, nextAttemptAt).Set(x => x.CleanupErrorCode, errorCode).Set(x => x.Version + 1)
                .Where(x => x.Id == id && x.Version == version && !x.CleanupCompleted && x.State != RemoteDriveTransferState.Completed &&
                    (x.LeaseToken == null || x.LeaseUntil <= now) &&
                    (x.State == RemoteDriveTransferState.Verified || x.State == RemoteDriveTransferState.Aborted || x.ExpiresAt <= now))
                .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false) == 1, ct);
    }

    public Task<IReadOnlyList<RemoteDriveTransfer>> ListCleanupAsync(long now, int take, CancellationToken ct = default)
    {
        ValidateTake(take);
        if (take == 0) return Task.FromResult<IReadOnlyList<RemoteDriveTransfer>>(Array.Empty<RemoteDriveTransfer>());
        return InTransactionAsync<IReadOnlyList<RemoteDriveTransfer>>(async (orm, transaction, token) =>
            await orm.Select<RemoteDriveTransfer>().WithTransaction(transaction)
                .Where(x => !x.CleanupCompleted && x.CleanupNextAttemptAt <= now &&
                    (x.State == RemoteDriveTransferState.Verified || x.State == RemoteDriveTransferState.Aborted ||
                     x.State != RemoteDriveTransferState.Completed && x.ExpiresAt <= now) &&
                    (x.LeaseToken == null || x.LeaseUntil <= now))
                .OrderBy(x => x.CleanupNextAttemptAt).OrderBy(x => x.ExpiresAt).OrderBy(x => x.Id).Limit(take).ToListAsync(token).ConfigureAwait(false), ct);
    }

    public Task<FileStoragePreference?> FindPreferenceAsync(string scopeKey, string ownerId, string container, CancellationToken ct = default)
        => InTransactionAsync<FileStoragePreference?>(async (orm, transaction, token) =>
            await orm.Select<FileStoragePreference>().WithTransaction(transaction)
                .Where(x => x.ScopeKey == scopeKey && x.OwnerId == ownerId && x.Container == container).FirstAsync(token).ConfigureAwait(false), ct);

    public async Task<bool> SavePreferenceAsync(FileStoragePreference preference, long expectedVersion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preference);
        if (expectedVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        var nextVersion = expectedVersion == 0 ? 1 : checked(expectedVersion + 1);
        try
        {
            var saved = await InTransactionAsync(async (orm, transaction, token) =>
            {
                var current = await orm.Select<FileStoragePreference>().WithTransaction(transaction)
                    .Where(x => x.ScopeKey == preference.ScopeKey && x.OwnerId == preference.OwnerId && x.Container == preference.Container)
                    .FirstAsync(token).ConfigureAwait(false);
                if (expectedVersion == 0)
                {
                    if (current != null) return false;
                    preference.Version = 1;
                    return await orm.Insert(preference).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false) == 1;
                }
                if (current == null || current.Version != expectedVersion || current.Id != preference.Id) return false;
                preference.Version = nextVersion;
                return await orm.Update<FileStoragePreference>().SetSource(preference)
                    .Where(x => x.Id == preference.Id && x.Version == expectedVersion)
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false) == 1;
            }, ct).ConfigureAwait(false);
            if (saved) preference.Version = nextVersion;
            return saved;
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { return false; }
    }

    public Task<FileBindingCopyRequest?> FindCopyAsync(Guid id, CancellationToken ct = default)
        => InTransactionAsync<FileBindingCopyRequest?>(async (orm, transaction, token) =>
            await orm.Select<FileBindingCopyRequest>().WithTransaction(transaction).Where(x => x.Id == id).FirstAsync(token).ConfigureAwait(false), ct);

    public Task<FileBindingCopyRequest?> FindCopyRequestAsync(string requestKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requestKey);
        return InTransactionAsync<FileBindingCopyRequest?>(async (orm, transaction, token) =>
            await orm.Select<FileBindingCopyRequest>().WithTransaction(transaction).Where(x => x.RequestKey == requestKey).FirstAsync(token).ConfigureAwait(false), ct);
    }

    public async Task<bool> CreateCopyAsync(FileBindingCopyRequest request, FileEntry target, FileOperation operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(operation);
        if (target.File.Id != request.CopyFileId || operation.FileId != request.CopyFileId || operation.Kind != FileOperationKind.CopyForBinding ||
            target.File.State != FileState.Pending || target.File.Ownership != RemoteDriveOwnership.Shared || target.Session != null || target.Bindings.Count != 0)
            throw new ArgumentException("复制请求、私有目标和后台任务不一致。");
        try
        {
            return await InTransactionAsync(async (orm, transaction, token) =>
            {
                if (await orm.Select<FileBindingCopyRequest>().WithTransaction(transaction)
                        .Where(x => x.Id == request.Id || x.RequestKey == request.RequestKey || x.CopyFileId == request.CopyFileId).AnyAsync(token).ConfigureAwait(false) ||
                    await orm.Select<FileRecord>().WithTransaction(transaction)
                        .Where(x => x.Id == target.File.Id || x.RequestKey == target.File.RequestKey).AnyAsync(token).ConfigureAwait(false) ||
                    await orm.Select<FileOperation>().WithTransaction(transaction).Where(x => x.Id == operation.Id).AnyAsync(token).ConfigureAwait(false))
                    return false;
                var source = await orm.Select<FileRecord>().WithTransaction(transaction).Where(x => x.Id == request.SourceFileId).FirstAsync(token).ConfigureAwait(false);
                if (!CopySourceMatches(source, request)) return false;
                request.Version = 1;
                operation.Version = Math.Max(operation.Version, 1);
                PrepareOperation(operation, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                await orm.Insert(request).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                await orm.Insert(target.File).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                await orm.Insert(operation).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                return true;
            }, ct, System.Data.IsolationLevel.Serializable).ConfigureAwait(false);
        }
        catch (StoreConcurrencyException) { return false; }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { return false; }
    }

    public async Task<bool> UpdateCopyAsync(FileBindingCopyRequest request, long expectedVersion, FileEntry target, long targetVersion,
        IReadOnlyList<FileOperation>? operations = null, FileOperationLease? operationLease = null, FileTransferLease? transferLease = null,
        bool validateSource = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(target);
        if (expectedVersion < 1 || targetVersion < 1 || request.Version != expectedVersion || target.File.Id != request.CopyFileId ||
            target.File.Version != targetVersion || operations?.Any(x => x.FileId != target.File.Id) == true) return false;
        var originalRequestVersion = request.Version;
        var originalFileVersion = target.File.Version;
        var nextRequestVersion = checked(expectedVersion + 1);
        var nextFileVersion = checked(targetVersion + 1);
        try
        {
            var updated = await InTransactionAsync(async (orm, transaction, token) =>
            {
                var currentRequest = await orm.Select<FileBindingCopyRequest>().WithTransaction(transaction)
                    .Where(x => x.Id == request.Id).FirstAsync(token).ConfigureAwait(false);
                var currentFile = await orm.Select<FileRecord>().WithTransaction(transaction)
                    .Where(x => x.Id == request.CopyFileId).FirstAsync(token).ConfigureAwait(false);
                if (currentRequest == null || currentRequest.Version != expectedVersion || !SameCopyIdentity(currentRequest, request) ||
                    currentFile == null || currentFile.Version != targetVersion || !CanTransitionCopy(currentRequest.State, request.State)) return false;
                if (validateSource)
                {
                    var source = await orm.Select<FileRecord>().WithTransaction(transaction)
                        .Where(x => x.Id == currentRequest.SourceFileId).FirstAsync(token).ConfigureAwait(false);
                    if (!CopySourceMatches(source, currentRequest)) return false;
                }

                var changed = false;
                if (operationLease != null)
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var fenced = await orm.Update<FileOperation>().Set(x => x.Version + 1)
                        .Where(x => x.Id == operationLease.OperationId && x.FileId == currentFile.Id && x.LeaseToken == operationLease.Token &&
                            x.State == FileOperationState.Running && x.LeaseUntil > now)
                        .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    if (fenced != 1) return false;
                    changed = true;
                }
                if (transferLease != null && !await PublishTransferAsync(orm, transaction, transferLease, target.File, token).ConfigureAwait(false))
                {
                    if (changed) throw new StoreConcurrencyException();
                    return false;
                }
                if (transferLease != null) changed = true;

                request.Version = nextRequestVersion;
                var requestRows = await orm.Update<FileBindingCopyRequest>().SetSource(request)
                    .Where(x => x.Id == request.Id && x.Version == expectedVersion)
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (requestRows != 1)
                {
                    if (changed) throw new StoreConcurrencyException();
                    return false;
                }
                target.File.Version = nextFileVersion;
                var fileRows = await orm.Update<FileRecord>().SetSource(target.File)
                    .Where(x => x.Id == target.File.Id && x.Version == targetVersion)
                    .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (fileRows != 1) throw new StoreConcurrencyException();
                if (target.Session != null)
                {
                    target.Session.Version = nextFileVersion;
                    var sessionRows = await orm.Update<FileUploadSession>().SetSource(target.Session)
                        .Where(x => x.Id == target.Session.Id && x.FileId == target.File.Id)
                        .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    if (sessionRows != 1) throw new StoreConcurrencyException();
                }
                await orm.Delete<FileBinding>().Where(x => x.FileId == target.File.Id).WithTransaction(transaction)
                    .ExecuteAffrowsAsync(token).ConfigureAwait(false);
                foreach (var binding in target.Bindings)
                    await orm.Insert(binding).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                var nowForEvents = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (CreateTransitionEvent(target.File, currentFile.State, nowForEvents) is { } transition)
                    await orm.Insert(transition).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                if (operations != null)
                    foreach (var operation in operations)
                    {
                        PrepareOperation(operation, nowForEvents);
                        await orm.Insert(operation).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    }
                return true;
            }, ct, validateSource ? System.Data.IsolationLevel.Serializable : null).ConfigureAwait(false);
            if (!updated)
            {
                request.Version = originalRequestVersion;
                target.File.Version = originalFileVersion;
                return false;
            }
            request.Version = nextRequestVersion;
            target.File.Version = nextFileVersion;
            if (target.Session != null) target.Session.Version = nextFileVersion;
            return true;
        }
        catch (StoreConcurrencyException)
        {
            request.Version = originalRequestVersion;
            target.File.Version = originalFileVersion;
            return false;
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception))
        {
            request.Version = originalRequestVersion;
            target.File.Version = originalFileVersion;
            return false;
        }
        catch
        {
            request.Version = originalRequestVersion;
            target.File.Version = originalFileVersion;
            throw;
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

    private static async Task FailCopyOperationAsync(IFreeSql orm, DbTransaction transaction, FileOperation operation, string? error,
        long now, CancellationToken ct)
    {
        if (!Guid.TryParseExact(operation.Payload, "N", out var requestId)) return;
        var request = await orm.Select<FileBindingCopyRequest>().WithTransaction(transaction)
            .Where(x => x.Id == requestId).FirstAsync(ct).ConfigureAwait(false);
        if (request == null || request.State != FileBindingCopyState.Pending) return;
        request.State = FileBindingCopyState.Failed;
        request.ErrorCode = error ?? "copy_failed";
        var requestVersion = request.Version;
        request.Version = checked(requestVersion + 1);
        var changedRequest = await orm.Update<FileBindingCopyRequest>().SetSource(request)
            .Where(x => x.Id == request.Id && x.Version == requestVersion).WithTransaction(transaction)
            .ExecuteAffrowsAsync(ct).ConfigureAwait(false);
        if (changedRequest != 1) throw new StoreConcurrencyException();
        var file = await orm.Select<FileRecord>().WithTransaction(transaction).Where(x => x.Id == request.CopyFileId).FirstAsync(ct).ConfigureAwait(false);
        if (file?.State != FileState.Pending) return;
        // 请求失败与副本清理同事务提交，避免生成必然被 Delete 拒绝的清理任务。
        var changedFile = await orm.Update<FileRecord>().Set(x => x.State, FileState.DeletePending).Set(x => x.Version + 1)
            .Where(x => x.Id == file.Id && x.Version == file.Version).WithTransaction(transaction).ExecuteAffrowsAsync(ct).ConfigureAwait(false);
        if (changedFile != 1) throw new StoreConcurrencyException();
        await orm.Insert(new FileOperation { Id = Guid.NewGuid(), FileId = file.Id, Kind = FileOperationKind.Delete,
            State = FileOperationState.Pending, NextAttemptAt = now, Version = 1, CreatedAt = now }).WithTransaction(transaction)
            .ExecuteAffrowsAsync(ct).ConfigureAwait(false);
    }
}
