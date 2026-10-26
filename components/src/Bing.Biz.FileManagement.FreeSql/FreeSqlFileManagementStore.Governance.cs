using System.Data.Common;
using Bing.Biz.FileManagement;
using FreeSql;

namespace Bing.Biz.FileManagement.FreeSql;

public sealed partial class FreeSqlFileManagementStore
{
    public Task<IReadOnlyList<FileOutboxEvent>> QueryEventsAsync(FileEventQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.ScopeKey)) throw new ArgumentException("范围不能为空。", nameof(query));
        ValidatePage(query.Skip, query.Take, 100);
        if (query.Take == 0) return Task.FromResult<IReadOnlyList<FileOutboxEvent>>(Array.Empty<FileOutboxEvent>());
        return InTransactionAsync<IReadOnlyList<FileOutboxEvent>>(async (orm, transaction, token) =>
        {
            var events = orm.Select<FileOutboxEvent>().WithTransaction(transaction).Where(x => x.ScopeKey == query.ScopeKey);
            if (query.FileId.HasValue) events = events.Where(x => x.FileId == query.FileId.Value);
            if (query.Delivered.HasValue) events = events.Where(x => x.Delivered == query.Delivered.Value);
            if (query.FailedOnly) events = events.Where(x => !x.Delivered && x.ErrorCode != null);
            if (!query.IncludeArchived)
                events = events.Where(x => !orm.Select<FileHistoryArchive>().Where(archive => archive.ScopeKey == query.ScopeKey &&
                    archive.Kind == FileHistoryKind.Event && archive.EntityId == x.Id && archive.SourceVersion == x.Version).Any());
            return await events.OrderByDescending(x => x.CreatedAt).OrderBy(x => x.Id).Skip(query.Skip).Limit(query.Take)
                .ToListAsync(token).ConfigureAwait(false);
        }, ct);
    }

    public Task<FileOutboxEvent?> FindEventAsync(Guid id, CancellationToken ct = default)
        => InTransactionAsync<FileOutboxEvent?>(async (orm, transaction, token) =>
            await orm.Select<FileOutboxEvent>().WithTransaction(transaction).Where(x => x.Id == id)
                .FirstAsync(token).ConfigureAwait(false), ct);

    public Task<IReadOnlyList<FileEventDelivery>> ListEventDeliveriesAsync(Guid id, CancellationToken ct = default)
        => InTransactionAsync<IReadOnlyList<FileEventDelivery>>(async (orm, transaction, token) =>
            await orm.Select<FileEventDelivery>().WithTransaction(transaction).Where(x => x.EventId == id)
                .OrderBy(x => x.Consumer).ToListAsync(token).ConfigureAwait(false), ct);

    public Task<bool> TryUpdateEventDeliveryAsync(FileOutboxEvent expected, FileOutboxEvent updated,
        IReadOnlyList<FileEventDelivery> deliveries, string? leaseToken, FileGovernanceAudit? audit,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(updated);
        ArgumentNullException.ThrowIfNull(deliveries);
        var administrative = string.IsNullOrWhiteSpace(leaseToken);
        if (expected.Id == Guid.Empty || expected.Version < 0 || updated.Version != expected.Version + 1 ||
            !SameEventIdentity(expected, updated) || updated.Attempts < expected.Attempts || updated.NextAttemptAt < 0 ||
            updated.LeaseUntil < 0 || administrative != (audit != null) ||
            audit != null && (audit.Id == Guid.Empty || audit.ScopeKey != expected.ScopeKey))
            return Task.FromResult(false);

        var incoming = new Dictionary<string, FileEventDelivery>(StringComparer.Ordinal);
        foreach (var receipt in deliveries)
        {
            if (receipt.EventId != expected.Id || !FileOutboxDispatcher.ValidConsumerName(receipt.Consumer) ||
                receipt.Attempts < 0 || receipt.NextAttemptAt < 0 || receipt.CompletedAt < 0 ||
                receipt.Delivered && (receipt.CompletedAt == 0 || receipt.ErrorCode != null) ||
                !receipt.Delivered && receipt.CompletedAt != 0 || (receipt.ErrorCode?.Length ?? 0) > 200 ||
                !incoming.TryAdd(receipt.Consumer, receipt))
                return Task.FromResult(false);
        }

        return UpdateEventDeliveryCoreAsync();

        async Task<bool> UpdateEventDeliveryCoreAsync()
        {
            try
            {
                return await InTransactionAsync(async (orm, transaction, token) =>
                {
                    var current = await orm.Select<FileOutboxEvent>().WithTransaction(transaction)
                        .Where(x => x.Id == expected.Id).FirstAsync(token).ConfigureAwait(false);
                    if (current == null || current.Version != expected.Version || !SameEventIdentity(current, expected)) return false;
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var activeLease = current.LeaseToken != null && current.LeaseUntil > now;
                    if (administrative ? activeLease : current.LeaseToken != leaseToken || !activeLease || current.Delivered)
                        return false;
                    if (audit != null && await orm.Select<FileGovernanceAudit>().WithTransaction(transaction)
                        .Where(x => x.Id == audit.Id).AnyAsync(token).ConfigureAwait(false)) return false;

                    var existing = await orm.Select<FileEventDelivery>().WithTransaction(transaction)
                        .Where(x => x.EventId == expected.Id).ToListAsync(token).ConfigureAwait(false);
                    var oldByConsumer = existing.ToDictionary(x => x.Consumer, StringComparer.Ordinal);
                    if (oldByConsumer.Keys.Any(name => !incoming.ContainsKey(name)) ||
                        oldByConsumer.Count != 0 && !administrative && incoming.Keys.Any(name => !oldByConsumer.ContainsKey(name)))
                        return false;
                    foreach (var old in existing)
                    {
                        var receipt = incoming[old.Consumer];
                        if (!administrative && (receipt.Attempts < old.Attempts || old.Delivered &&
                            (!receipt.Delivered || receipt.CompletedAt != old.CompletedAt))) return false;
                    }

                    var update = orm.Update<FileOutboxEvent>()
                        .Set(x => x.NextAttemptAt, updated.NextAttemptAt)
                        .Set(x => x.Attempts, updated.Attempts)
                        .Set(x => x.LeaseToken, updated.LeaseToken)
                        .Set(x => x.LeaseUntil, updated.LeaseUntil)
                        .Set(x => x.Delivered, updated.Delivered)
                        .Set(x => x.Version, updated.Version)
                        .Set(x => x.ErrorCode, updated.ErrorCode)
                        .Where(x => x.Id == expected.Id && x.Version == expected.Version);
                    if (administrative) update = update.Where(x => x.LeaseToken == null || x.LeaseUntil <= now);
                    else update = update.Where(x => x.LeaseToken == leaseToken && x.LeaseUntil > now && !x.Delivered);
                    if (await update.WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false) != 1)
                        throw new StoreConcurrencyException();

                    foreach (var receipt in incoming.Values)
                    {
                        if (oldByConsumer.ContainsKey(receipt.Consumer))
                        {
                            if (await orm.Update<FileEventDelivery>().SetSource(receipt)
                                .Where(x => x.EventId == receipt.EventId && x.Consumer == receipt.Consumer)
                                .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false) != 1)
                                throw new StoreConcurrencyException();
                        }
                        else await orm.Insert(receipt).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    }
                    if (audit != null)
                    {
                        await orm.Delete<FileHistoryArchive>().Where(x => x.Kind == FileHistoryKind.Event && x.EntityId == expected.Id)
                            .WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                        await orm.Insert(audit).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    }
                    return true;
                }, ct).ConfigureAwait(false);
            }
            catch (StoreConcurrencyException) { return false; }
            catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { return false; }
        }
    }

    public Task<FileGovernanceAudit?> FindGovernanceAuditAsync(Guid requestId, CancellationToken ct = default)
        => InTransactionAsync<FileGovernanceAudit?>(async (orm, transaction, token) =>
            await orm.Select<FileGovernanceAudit>().WithTransaction(transaction).Where(x => x.Id == requestId)
                .FirstAsync(token).ConfigureAwait(false), ct);

    public Task<IReadOnlyList<FileGovernanceAudit>> ListGovernanceAuditAsync(string scopeKey, Guid? targetId,
        int skip, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        ValidatePage(skip, take, 100);
        if (take == 0) return Task.FromResult<IReadOnlyList<FileGovernanceAudit>>(Array.Empty<FileGovernanceAudit>());
        return InTransactionAsync<IReadOnlyList<FileGovernanceAudit>>(async (orm, transaction, token) =>
        {
            var query = orm.Select<FileGovernanceAudit>().WithTransaction(transaction).Where(x => x.ScopeKey == scopeKey);
            if (targetId.HasValue) query = query.Where(x => x.TargetId == targetId.Value);
            return await query.OrderByDescending(x => x.CreatedAt).OrderBy(x => x.Id).Skip(skip).Limit(take)
                .ToListAsync(token).ConfigureAwait(false);
        }, ct);
    }

    public Task<IReadOnlyList<FileHistoryCandidate>> QueryHistoryCandidatesAsync(string scopeKey,
        FileHistoryKind kind, long cutoff, int take, CancellationToken ct = default)
    {
        ValidateHistoryQuery(scopeKey, kind, cutoff, take);
        return InTransactionAsync<IReadOnlyList<FileHistoryCandidate>>(async (orm, transaction, token) =>
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            switch (kind)
            {
                case FileHistoryKind.Operation:
                {
                    var rows = await orm.Select<FileOperation>().WithTransaction(transaction)
                        .Where(x => x.State == FileOperationState.Succeeded && x.FinishedAt > 0 && x.FinishedAt <= cutoff &&
                            (x.LeaseToken == null || x.LeaseUntil <= now) &&
                            orm.Select<FileRecord>().Where(file => file.Id == x.FileId && file.ScopeKey == scopeKey).Any() &&
                            !orm.Select<FileHistoryArchive>().Where(a => a.ScopeKey == scopeKey && a.Kind == kind &&
                                a.EntityId == x.Id && a.SourceVersion == x.Version).Any())
                        .OrderBy(x => x.FinishedAt).OrderBy(x => x.Id).Limit(take).ToListAsync(token).ConfigureAwait(false);
                    return rows.Select(x => new FileHistoryCandidate { Id = x.Id, Version = x.Version, Time = x.FinishedAt }).ToArray();
                }
                case FileHistoryKind.Event:
                {
                    var rows = await orm.Select<FileOutboxEvent>().WithTransaction(transaction)
                        .Where(x => x.ScopeKey == scopeKey && x.Delivered && x.CreatedAt <= cutoff &&
                            (x.LeaseToken == null || x.LeaseUntil <= now) &&
                            !orm.Select<FileHistoryArchive>().Where(a => a.ScopeKey == scopeKey && a.Kind == kind &&
                                a.EntityId == x.Id && a.SourceVersion == x.Version).Any())
                        .OrderBy(x => x.CreatedAt).OrderBy(x => x.Id).Limit(take).ToListAsync(token).ConfigureAwait(false);
                    return rows.Select(x => new FileHistoryCandidate { Id = x.Id, Version = x.Version, Time = x.CreatedAt }).ToArray();
                }
                case FileHistoryKind.MigrationBatch:
                {
                    var rows = await orm.Select<FileMigrationBatch>().WithTransaction(transaction)
                        .Where(x => x.ScopeKey == scopeKey && x.State == FileMigrationBatchState.Completed && x.CreatedAt <= cutoff &&
                            !orm.Select<FileMigrationBatchItem>().Where(item => item.BatchId == x.Id &&
                                (item.State == FileMigrationItemState.Pending || item.State == FileMigrationItemState.Queued)).Any() &&
                            !orm.Select<FileHistoryArchive>().Where(a => a.ScopeKey == scopeKey && a.Kind == kind &&
                                a.EntityId == x.Id && a.SourceVersion == x.Version).Any())
                        .OrderBy(x => x.CreatedAt).OrderBy(x => x.Id).Limit(take).ToListAsync(token).ConfigureAwait(false);
                    return rows.Select(x => new FileHistoryCandidate { Id = x.Id, Version = x.Version, Time = x.CreatedAt }).ToArray();
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }, ct);
    }

    public Task<bool> TryArchiveHistoryAsync(string scopeKey, FileHistorySelection selection,
        FileGovernanceAudit audit, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(audit);
        if (string.IsNullOrWhiteSpace(scopeKey) || audit.Id == Guid.Empty || audit.ScopeKey != scopeKey ||
            !Enum.IsDefined(typeof(FileHistoryKind), selection.Kind) || selection.Items == null || selection.Cutoff < 0 ||
            selection.Items.Count is < 1 or > 100 ||
            selection.Items.Any(x => x.Id == Guid.Empty || x.Version < 0 || x.Time < 0 || x.Time > selection.Cutoff) ||
            selection.Items.Select(x => x.Id).Distinct().Count() != selection.Items.Count)
            return Task.FromResult(false);

        return ArchiveHistoryCoreAsync();

        async Task<bool> ArchiveHistoryCoreAsync()
        {
            try
            {
                return await InTransactionAsync(async (orm, transaction, token) =>
                {
                    if (await orm.Select<FileGovernanceAudit>().WithTransaction(transaction)
                        .Where(x => x.Id == audit.Id).AnyAsync(token).ConfigureAwait(false)) return false;
                    var ids = selection.Items.Select(x => x.Id).OrderBy(x => x).ToList();
                    var candidates = selection.Items.ToDictionary(x => x.Id);
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    switch (selection.Kind)
                    {
                        case FileHistoryKind.Operation:
                        {
                            var rows = await orm.Select<FileOperation>().WithTransaction(transaction)
                                .Where(x => ids.Contains(x.Id) && orm.Select<FileRecord>()
                                    .Where(file => file.Id == x.FileId && file.ScopeKey == scopeKey).Any())
                                .OrderBy(x => x.Id).ToListAsync(token).ConfigureAwait(false);
                            if (rows.Count != ids.Count || rows.Any(x => !IsOperationCandidate(x, candidates[x.Id], selection.Cutoff, now)))
                                return false;
                            await LockOperationVersionsAsync(orm, transaction, rows, token).ConfigureAwait(false);
                            break;
                        }
                        case FileHistoryKind.Event:
                        {
                            var rows = await orm.Select<FileOutboxEvent>().WithTransaction(transaction)
                                .Where(x => ids.Contains(x.Id) && x.ScopeKey == scopeKey).OrderBy(x => x.Id)
                                .ToListAsync(token).ConfigureAwait(false);
                            if (rows.Count != ids.Count || rows.Any(x => !IsEventCandidate(x, candidates[x.Id], selection.Cutoff, now)))
                                return false;
                            await LockEventVersionsAsync(orm, transaction, rows, token).ConfigureAwait(false);
                            break;
                        }
                        case FileHistoryKind.MigrationBatch:
                        {
                            var rows = await orm.Select<FileMigrationBatch>().WithTransaction(transaction)
                                .Where(x => ids.Contains(x.Id) && x.ScopeKey == scopeKey).OrderBy(x => x.Id)
                                .ToListAsync(token).ConfigureAwait(false);
                            if (rows.Count != ids.Count || rows.Any(x => !IsBatchCandidate(x, candidates[x.Id], selection.Cutoff)))
                                return false;
                            await LockBatchVersionsAsync(orm, transaction, rows, token).ConfigureAwait(false);
                            if (await orm.Select<FileMigrationBatchItem>().WithTransaction(transaction).Where(x => ids.Contains(x.BatchId) &&
                                (x.State == FileMigrationItemState.Pending || x.State == FileMigrationItemState.Queued))
                                .AnyAsync(token).ConfigureAwait(false)) throw new StoreConcurrencyException();
                            break;
                        }
                        default:
                            return false;
                    }

                    var oldMarkers = await orm.Select<FileHistoryArchive>().WithTransaction(transaction)
                        .Where(x => x.Kind == selection.Kind && ids.Contains(x.EntityId)).ToListAsync(token).ConfigureAwait(false);
                    if (oldMarkers.Any(x => x.ScopeKey != scopeKey || candidates.TryGetValue(x.EntityId, out var item) &&
                        item.Version == x.SourceVersion)) throw new StoreConcurrencyException();
                    if (oldMarkers.Count != 0)
                    {
                        var deleted = await orm.Delete<FileHistoryArchive>().Where(x => x.Kind == selection.Kind &&
                            ids.Contains(x.EntityId)).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                        if (deleted != oldMarkers.Count) throw new StoreConcurrencyException();
                    }
                    await orm.Insert(selection.Items.Select(item => new FileHistoryArchive
                    {
                        Id = Guid.NewGuid(), ScopeKey = scopeKey, Kind = selection.Kind, EntityId = item.Id,
                        SourceVersion = item.Version, ArchivedAt = audit.CreatedAt, ActorId = audit.ActorId
                    })).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    await orm.Insert(audit).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    return true;
                }, ct).ConfigureAwait(false);
            }
            catch (StoreConcurrencyException) { return false; }
            catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { return false; }
        }
    }

    public Task<IReadOnlyList<FileHistoryArchive>> ListHistoryArchivesAsync(string scopeKey,
        FileHistoryKind? kind, int skip, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        if (kind.HasValue && !Enum.IsDefined(typeof(FileHistoryKind), kind.Value)) throw new ArgumentOutOfRangeException(nameof(kind));
        ValidatePage(skip, take, 100);
        if (take == 0) return Task.FromResult<IReadOnlyList<FileHistoryArchive>>(Array.Empty<FileHistoryArchive>());
        return InTransactionAsync<IReadOnlyList<FileHistoryArchive>>(async (orm, transaction, token) =>
        {
            var archives = orm.Select<FileHistoryArchive>().WithTransaction(transaction).Where(x => x.ScopeKey == scopeKey);
            if (kind.HasValue) archives = archives.Where(x => x.Kind == kind.Value);
            archives = archives.Where(x =>
                x.Kind == FileHistoryKind.Operation && orm.Select<FileOperation>().Where(operation => operation.Id == x.EntityId &&
                    operation.Version == x.SourceVersion && orm.Select<FileRecord>().Where(file =>
                        file.Id == operation.FileId && file.ScopeKey == x.ScopeKey).Any()).Any() ||
                x.Kind == FileHistoryKind.Event && orm.Select<FileOutboxEvent>().Where(evt => evt.Id == x.EntityId &&
                    evt.ScopeKey == x.ScopeKey && evt.Version == x.SourceVersion).Any() ||
                x.Kind == FileHistoryKind.MigrationBatch && orm.Select<FileMigrationBatch>().Where(batch => batch.Id == x.EntityId &&
                    batch.ScopeKey == x.ScopeKey && batch.Version == x.SourceVersion).Any());
            return await archives.OrderByDescending(x => x.ArchivedAt).OrderBy(x => x.Id).Skip(skip).Limit(take)
                .ToListAsync(token).ConfigureAwait(false);
        }, ct);
    }

    public Task<bool> TryRestoreHistoryAsync(string scopeKey, Guid archiveId, FileGovernanceAudit audit,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        if (string.IsNullOrWhiteSpace(scopeKey) || archiveId == Guid.Empty || audit.Id == Guid.Empty || audit.ScopeKey != scopeKey)
            return Task.FromResult(false);
        return RestoreHistoryCoreAsync();

        async Task<bool> RestoreHistoryCoreAsync()
        {
            try
            {
                return await InTransactionAsync(async (orm, transaction, token) =>
                {
                    if (await orm.Select<FileGovernanceAudit>().WithTransaction(transaction)
                        .Where(x => x.Id == audit.Id).AnyAsync(token).ConfigureAwait(false)) return false;
                    var archive = await orm.Select<FileHistoryArchive>().WithTransaction(transaction)
                        .Where(x => x.Id == archiveId && x.ScopeKey == scopeKey).FirstAsync(token).ConfigureAwait(false);
                    if (archive == null) return false;
                    switch (archive.Kind)
                    {
                        case FileHistoryKind.Operation:
                        {
                            var source = await orm.Select<FileOperation>().WithTransaction(transaction)
                                .Where(x => x.Id == archive.EntityId && x.Version == archive.SourceVersion &&
                                    orm.Select<FileRecord>().Where(file => file.Id == x.FileId && file.ScopeKey == scopeKey).Any())
                                .FirstAsync(token).ConfigureAwait(false);
                            if (source == null) return false;
                            await LockOperationVersionsAsync(orm, transaction, new[] { source }, token).ConfigureAwait(false);
                            break;
                        }
                        case FileHistoryKind.Event:
                        {
                            var source = await orm.Select<FileOutboxEvent>().WithTransaction(transaction)
                                .Where(x => x.Id == archive.EntityId && x.ScopeKey == scopeKey && x.Version == archive.SourceVersion)
                                .FirstAsync(token).ConfigureAwait(false);
                            if (source == null) return false;
                            await LockEventVersionsAsync(orm, transaction, new[] { source }, token).ConfigureAwait(false);
                            break;
                        }
                        case FileHistoryKind.MigrationBatch:
                        {
                            var source = await orm.Select<FileMigrationBatch>().WithTransaction(transaction)
                                .Where(x => x.Id == archive.EntityId && x.ScopeKey == scopeKey && x.Version == archive.SourceVersion)
                                .FirstAsync(token).ConfigureAwait(false);
                            if (source == null) return false;
                            await LockBatchVersionsAsync(orm, transaction, new[] { source }, token).ConfigureAwait(false);
                            break;
                        }
                        default:
                            return false;
                    }
                    if (await orm.Delete<FileHistoryArchive>().Where(x => x.Id == archiveId && x.ScopeKey == scopeKey &&
                        x.SourceVersion == archive.SourceVersion).WithTransaction(transaction)
                        .ExecuteAffrowsAsync(token).ConfigureAwait(false) != 1) throw new StoreConcurrencyException();
                    await orm.Insert(audit).WithTransaction(transaction).ExecuteAffrowsAsync(token).ConfigureAwait(false);
                    return true;
                }, ct).ConfigureAwait(false);
            }
            catch (StoreConcurrencyException) { return false; }
            catch (Exception exception) when (IsUniqueConstraintViolation(exception)) { return false; }
        }
    }

    private static bool SameEventIdentity(FileOutboxEvent left, FileOutboxEvent right)
        => left.Id == right.Id && left.FileId == right.FileId && left.ScopeKey == right.ScopeKey &&
           left.FileVersion == right.FileVersion && left.Kind == right.Kind && left.CreatedAt == right.CreatedAt;

    private static void ValidateHistoryQuery(string scopeKey, FileHistoryKind kind, long cutoff, int take)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        if (!Enum.IsDefined(typeof(FileHistoryKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (cutoff < 0) throw new ArgumentOutOfRangeException(nameof(cutoff));
        if (take is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(take));
    }

    private static bool IsOperationCandidate(FileOperation row, FileHistoryCandidate candidate, long cutoff, long now)
        => row.Id == candidate.Id && row.Version == candidate.Version && row.State == FileOperationState.Succeeded &&
           row.FinishedAt > 0 && row.FinishedAt == candidate.Time && row.FinishedAt <= cutoff &&
           (row.LeaseToken == null || row.LeaseUntil <= now);

    private static bool IsEventCandidate(FileOutboxEvent row, FileHistoryCandidate candidate, long cutoff, long now)
        => row.Id == candidate.Id && row.Version == candidate.Version && row.Delivered && row.CreatedAt == candidate.Time &&
           row.CreatedAt <= cutoff && (row.LeaseToken == null || row.LeaseUntil <= now);

    private static bool IsBatchCandidate(FileMigrationBatch row, FileHistoryCandidate candidate, long cutoff)
        => row.Id == candidate.Id && row.Version == candidate.Version && row.State == FileMigrationBatchState.Completed &&
           row.CreatedAt == candidate.Time && row.CreatedAt <= cutoff;

    private static async Task LockOperationVersionsAsync(IFreeSql orm, DbTransaction transaction,
        IReadOnlyList<FileOperation> rows, CancellationToken ct)
    {
        foreach (var row in rows)
            if (await orm.Update<FileOperation>().Set(x => x.Version + 1)
                .Where(x => x.Id == row.Id && x.Version == row.Version).WithTransaction(transaction)
                .ExecuteAffrowsAsync(ct).ConfigureAwait(false) != 1) throw new StoreConcurrencyException();
        foreach (var row in rows)
            if (await orm.Update<FileOperation>().Set(x => x.Version, row.Version)
                .Where(x => x.Id == row.Id && x.Version == checked(row.Version + 1)).WithTransaction(transaction)
                .ExecuteAffrowsAsync(ct).ConfigureAwait(false) != 1) throw new StoreConcurrencyException();
    }

    private static async Task LockEventVersionsAsync(IFreeSql orm, DbTransaction transaction,
        IReadOnlyList<FileOutboxEvent> rows, CancellationToken ct)
    {
        foreach (var row in rows)
            if (await orm.Update<FileOutboxEvent>().Set(x => x.Version + 1)
                .Where(x => x.Id == row.Id && x.Version == row.Version).WithTransaction(transaction)
                .ExecuteAffrowsAsync(ct).ConfigureAwait(false) != 1) throw new StoreConcurrencyException();
        foreach (var row in rows)
            if (await orm.Update<FileOutboxEvent>().Set(x => x.Version, row.Version)
                .Where(x => x.Id == row.Id && x.Version == checked(row.Version + 1)).WithTransaction(transaction)
                .ExecuteAffrowsAsync(ct).ConfigureAwait(false) != 1) throw new StoreConcurrencyException();
    }

    private static async Task LockBatchVersionsAsync(IFreeSql orm, DbTransaction transaction,
        IReadOnlyList<FileMigrationBatch> rows, CancellationToken ct)
    {
        foreach (var row in rows)
            if (await orm.Update<FileMigrationBatch>().Set(x => x.Version + 1)
                .Where(x => x.Id == row.Id && x.Version == row.Version).WithTransaction(transaction)
                .ExecuteAffrowsAsync(ct).ConfigureAwait(false) != 1) throw new StoreConcurrencyException();
        foreach (var row in rows)
            if (await orm.Update<FileMigrationBatch>().Set(x => x.Version, row.Version)
                .Where(x => x.Id == row.Id && x.Version == checked(row.Version + 1)).WithTransaction(transaction)
                .ExecuteAffrowsAsync(ct).ConfigureAwait(false) != 1) throw new StoreConcurrencyException();
    }
}
