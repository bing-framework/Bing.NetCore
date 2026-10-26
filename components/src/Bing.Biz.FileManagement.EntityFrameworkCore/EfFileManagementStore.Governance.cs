using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Bing.Biz.FileManagement.EntityFrameworkCore;

public sealed partial class EfFileManagementStore
{
    public async Task<IReadOnlyList<FileOutboxEvent>> QueryEventsAsync(FileEventQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.ScopeKey)) throw new ArgumentException("范围不能为空。", nameof(query));
        ValidatePage(query.Skip, query.Take, 100);
        if (query.Take == 0) return Array.Empty<FileOutboxEvent>();

        await using var db = await _factory.CreateDbContextAsync(ct);
        var events = db.Set<FileOutboxEvent>().AsNoTracking().Where(x => x.ScopeKey == query.ScopeKey);
        if (query.FileId.HasValue) events = events.Where(x => x.FileId == query.FileId.Value);
        if (query.Delivered.HasValue) events = events.Where(x => x.Delivered == query.Delivered.Value);
        if (query.FailedOnly) events = events.Where(x => !x.Delivered && x.ErrorCode != null);
        if (!query.IncludeArchived)
            events = events.Where(x => !db.Set<FileHistoryArchive>().Any(archive => archive.ScopeKey == query.ScopeKey &&
                archive.Kind == FileHistoryKind.Event && archive.EntityId == x.Id && archive.SourceVersion == x.Version));
        return await events.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(query.Skip).Take(query.Take).ToListAsync(ct);
    }

    public async Task<FileOutboxEvent?> FindEventAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileOutboxEvent>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<FileEventDelivery>> ListEventDeliveriesAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileEventDelivery>().AsNoTracking().Where(x => x.EventId == id)
            .OrderBy(x => x.Consumer).ToListAsync(ct);
    }

    public async Task<bool> TryUpdateEventDeliveryAsync(FileOutboxEvent expected, FileOutboxEvent updated,
        IReadOnlyList<FileEventDelivery> deliveries, string? leaseToken, FileGovernanceAudit? audit,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(updated);
        ArgumentNullException.ThrowIfNull(deliveries);
        var administrative = string.IsNullOrWhiteSpace(leaseToken);
        if (expected.Id == Guid.Empty || expected.Version < 0 || expected.Attempts < 0 || updated.Version != expected.Version + 1 ||
            !SameEventIdentity(expected, updated) || updated.Attempts < expected.Attempts || updated.NextAttemptAt < 0 || updated.LeaseUntil < 0 ||
            administrative != (audit != null) ||
            audit != null && (audit.Id == Guid.Empty || audit.ScopeKey != expected.ScopeKey))
            return false;

        var incoming = new Dictionary<string, FileEventDelivery>(StringComparer.Ordinal);
        foreach (var receipt in deliveries)
        {
            if (receipt.EventId != expected.Id || !Bing.Biz.FileManagement.FileOutboxDispatcher.ValidConsumerName(receipt.Consumer) ||
                receipt.Attempts < 0 || receipt.NextAttemptAt < 0 || receipt.CompletedAt < 0 ||
                receipt.Delivered && (receipt.CompletedAt == 0 || receipt.ErrorCode != null) ||
                !receipt.Delivered && receipt.CompletedAt != 0 ||
                (receipt.ErrorCode?.Length ?? 0) > 200 || !incoming.TryAdd(receipt.Consumer, receipt))
                return false;
        }

        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var current = await db.Set<FileOutboxEvent>().SingleOrDefaultAsync(x => x.Id == expected.Id, ct);
            if (current == null || current.Version != expected.Version || !SameEventIdentity(current, expected)) return false;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var activeLease = current.LeaseToken != null && current.LeaseUntil > now;
            if (administrative ? activeLease : current.LeaseToken != leaseToken || !activeLease || current.Delivered)
                return false;
            if (audit != null && await db.Set<FileGovernanceAudit>().AnyAsync(x => x.Id == audit.Id, ct)) return false;

            var existing = await db.Set<FileEventDelivery>().Where(x => x.EventId == expected.Id).ToListAsync(ct);
            var oldByConsumer = existing.ToDictionary(x => x.Consumer, StringComparer.Ordinal);
            if (oldByConsumer.Keys.Any(name => !incoming.ContainsKey(name)) ||
                oldByConsumer.Count != 0 && !administrative && incoming.Keys.Any(name => !oldByConsumer.ContainsKey(name)))
                return false;
            foreach (var old in existing)
            {
                var receipt = incoming[old.Consumer];
                if (!administrative && (receipt.Attempts < old.Attempts || old.Delivered &&
                    (!receipt.Delivered || receipt.CompletedAt != old.CompletedAt))
                   )
                    return false;
            }

            current.NextAttemptAt = updated.NextAttemptAt;
            current.Attempts = updated.Attempts;
            current.LeaseToken = updated.LeaseToken;
            current.LeaseUntil = updated.LeaseUntil;
            current.Delivered = updated.Delivered;
            current.ErrorCode = updated.ErrorCode;
            current.Version = updated.Version;
            db.Entry(current).Property(x => x.Version).OriginalValue = expected.Version;

            foreach (var receipt in incoming.Values)
            {
                if (oldByConsumer.TryGetValue(receipt.Consumer, out var tracked))
                {
                    tracked.Attempts = receipt.Attempts;
                    tracked.Delivered = receipt.Delivered;
                    tracked.NextAttemptAt = receipt.NextAttemptAt;
                    tracked.CompletedAt = receipt.CompletedAt;
                    tracked.ErrorCode = receipt.ErrorCode;
                }
                else db.Add(receipt);
            }
            if (audit != null)
            {
                db.RemoveRange(await db.Set<FileHistoryArchive>().Where(x =>
                    x.Kind == FileHistoryKind.Event && x.EntityId == expected.Id).ToListAsync(ct));
                db.Add(audit);
            }

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

    public async Task<FileGovernanceAudit?> FindGovernanceAuditAsync(Guid requestId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Set<FileGovernanceAudit>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
    }

    public async Task<IReadOnlyList<FileGovernanceAudit>> ListGovernanceAuditAsync(string scopeKey, Guid? targetId,
        int skip, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        ValidatePage(skip, take, 100);
        if (take == 0) return Array.Empty<FileGovernanceAudit>();
        await using var db = await _factory.CreateDbContextAsync(ct);
        var audits = db.Set<FileGovernanceAudit>().AsNoTracking().Where(x => x.ScopeKey == scopeKey);
        if (targetId.HasValue) audits = audits.Where(x => x.TargetId == targetId.Value);
        return await audits.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FileHistoryCandidate>> QueryHistoryCandidatesAsync(string scopeKey,
        FileHistoryKind kind, long cutoff, int take, CancellationToken ct = default)
    {
        ValidateHistoryQuery(scopeKey, kind, cutoff, take);
        await using var db = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return kind switch
        {
            FileHistoryKind.Operation => await db.Set<FileOperation>().AsNoTracking()
                .Where(x => x.State == FileOperationState.Succeeded && x.FinishedAt > 0 && x.FinishedAt <= cutoff &&
                    (x.LeaseToken == null || x.LeaseUntil <= now) &&
                    db.Set<FileRecord>().Any(file => file.Id == x.FileId && file.ScopeKey == scopeKey) &&
                    !db.Set<FileHistoryArchive>().Any(a => a.ScopeKey == scopeKey && a.Kind == kind &&
                        a.EntityId == x.Id && a.SourceVersion == x.Version))
                .OrderBy(x => x.FinishedAt).ThenBy(x => x.Id).Take(take)
                .Select(x => new FileHistoryCandidate { Id = x.Id, Version = x.Version, Time = x.FinishedAt }).ToListAsync(ct),
            FileHistoryKind.Event => await db.Set<FileOutboxEvent>().AsNoTracking()
                .Where(x => x.ScopeKey == scopeKey && x.Delivered && x.CreatedAt <= cutoff &&
                    (x.LeaseToken == null || x.LeaseUntil <= now) &&
                    !db.Set<FileHistoryArchive>().Any(a => a.ScopeKey == scopeKey && a.Kind == kind &&
                        a.EntityId == x.Id && a.SourceVersion == x.Version))
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(take)
                .Select(x => new FileHistoryCandidate { Id = x.Id, Version = x.Version, Time = x.CreatedAt }).ToListAsync(ct),
            FileHistoryKind.MigrationBatch => await db.Set<FileMigrationBatch>().AsNoTracking()
                .Where(x => x.ScopeKey == scopeKey && x.State == FileMigrationBatchState.Completed && x.CreatedAt <= cutoff &&
                    !db.Set<FileMigrationBatchItem>().Any(item => item.BatchId == x.Id &&
                        (item.State == FileMigrationItemState.Pending || item.State == FileMigrationItemState.Queued)) &&
                    !db.Set<FileHistoryArchive>().Any(a => a.ScopeKey == scopeKey && a.Kind == kind &&
                        a.EntityId == x.Id && a.SourceVersion == x.Version))
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(take)
                .Select(x => new FileHistoryCandidate { Id = x.Id, Version = x.Version, Time = x.CreatedAt }).ToListAsync(ct),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public async Task<bool> TryArchiveHistoryAsync(string scopeKey, FileHistorySelection selection,
        FileGovernanceAudit audit, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(audit);
        if (string.IsNullOrWhiteSpace(scopeKey) || audit.Id == Guid.Empty || audit.ScopeKey != scopeKey ||
            !Enum.IsDefined(typeof(FileHistoryKind), selection.Kind) || selection.Items == null ||
            selection.Cutoff < 0 || selection.Items.Count is < 1 or > 100 ||
            selection.Items.Any(x => x.Id == Guid.Empty || x.Version < 0 || x.Time < 0 || x.Time > selection.Cutoff) ||
            selection.Items.Select(x => x.Id).Distinct().Count() != selection.Items.Count)
            return false;

        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (await db.Set<FileGovernanceAudit>().AnyAsync(x => x.Id == audit.Id, ct)) return false;
            var ids = selection.Items.Select(x => x.Id).OrderBy(x => x).ToList();
            var candidates = selection.Items.ToDictionary(x => x.Id);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            switch (selection.Kind)
            {
                case FileHistoryKind.Operation:
                {
                    var rows = await db.Set<FileOperation>().Where(x => ids.Contains(x.Id) &&
                        db.Set<FileRecord>().Any(file => file.Id == x.FileId && file.ScopeKey == scopeKey))
                        .OrderBy(x => x.Id).ToListAsync(ct);
                    if (rows.Count != ids.Count || rows.Any(x => !IsOperationCandidate(x, candidates[x.Id], selection.Cutoff, now)))
                        return false;
                    await LockVersionsAsync(db, rows, nameof(FileOperation.Version), x => x.Version,
                        (x, version) => x.Version = version, ct);
                    break;
                }
                case FileHistoryKind.Event:
                {
                    var rows = await db.Set<FileOutboxEvent>().Where(x => ids.Contains(x.Id) && x.ScopeKey == scopeKey)
                        .OrderBy(x => x.Id).ToListAsync(ct);
                    if (rows.Count != ids.Count || rows.Any(x => !IsEventCandidate(x, candidates[x.Id], selection.Cutoff, now)))
                        return false;
                    await LockVersionsAsync(db, rows, nameof(FileOutboxEvent.Version), x => x.Version,
                        (x, version) => x.Version = version, ct);
                    break;
                }
                case FileHistoryKind.MigrationBatch:
                {
                    var rows = await db.Set<FileMigrationBatch>().Where(x => ids.Contains(x.Id) && x.ScopeKey == scopeKey)
                        .OrderBy(x => x.Id).ToListAsync(ct);
                    if (rows.Count != ids.Count || rows.Any(x => !IsBatchCandidate(x, candidates[x.Id], selection.Cutoff)))
                        return false;
                    await LockVersionsAsync(db, rows, nameof(FileMigrationBatch.Version), x => x.Version,
                        (x, version) => x.Version = version, ct);
                    if (await db.Set<FileMigrationBatchItem>().AnyAsync(x => ids.Contains(x.BatchId) &&
                        (x.State == FileMigrationItemState.Pending || x.State == FileMigrationItemState.Queued), ct))
                        return false;
                    break;
                }
                default:
                    return false;
            }

            var oldMarkers = await db.Set<FileHistoryArchive>().Where(x => x.Kind == selection.Kind &&
                ids.Contains(x.EntityId)).ToListAsync(ct);
            if (oldMarkers.Any(x => x.ScopeKey != scopeKey || candidates.TryGetValue(x.EntityId, out var item) &&
                item.Version == x.SourceVersion))
                return false;
            if (oldMarkers.Count != 0)
            {
                db.RemoveRange(oldMarkers);
                await db.SaveChangesAsync(ct);
            }
            db.AddRange(selection.Items.Select(item => new FileHistoryArchive
            {
                Id = Guid.NewGuid(), ScopeKey = scopeKey, Kind = selection.Kind, EntityId = item.Id,
                SourceVersion = item.Version, ArchivedAt = audit.CreatedAt, ActorId = audit.ActorId
            }));
            db.Add(audit);
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

    public async Task<IReadOnlyList<FileHistoryArchive>> ListHistoryArchivesAsync(string scopeKey,
        FileHistoryKind? kind, int skip, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("范围不能为空。", nameof(scopeKey));
        if (kind.HasValue && !Enum.IsDefined(typeof(FileHistoryKind), kind.Value)) throw new ArgumentOutOfRangeException(nameof(kind));
        ValidatePage(skip, take, 100);
        if (take == 0) return Array.Empty<FileHistoryArchive>();
        await using var db = await _factory.CreateDbContextAsync(ct);
        var archives = db.Set<FileHistoryArchive>().AsNoTracking().Where(x => x.ScopeKey == scopeKey);
        if (kind.HasValue) archives = archives.Where(x => x.Kind == kind.Value);
        archives = archives.Where(x =>
            x.Kind == FileHistoryKind.Operation && db.Set<FileOperation>().Any(operation => operation.Id == x.EntityId &&
                operation.Version == x.SourceVersion && db.Set<FileRecord>().Any(file => file.Id == operation.FileId && file.ScopeKey == x.ScopeKey)) ||
            x.Kind == FileHistoryKind.Event && db.Set<FileOutboxEvent>().Any(evt => evt.Id == x.EntityId &&
                evt.ScopeKey == x.ScopeKey && evt.Version == x.SourceVersion) ||
            x.Kind == FileHistoryKind.MigrationBatch && db.Set<FileMigrationBatch>().Any(batch => batch.Id == x.EntityId &&
                batch.ScopeKey == x.ScopeKey && batch.Version == x.SourceVersion));
        return await archives.OrderByDescending(x => x.ArchivedAt).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<bool> TryRestoreHistoryAsync(string scopeKey, Guid archiveId, FileGovernanceAudit audit,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        if (string.IsNullOrWhiteSpace(scopeKey) || archiveId == Guid.Empty || audit.Id == Guid.Empty || audit.ScopeKey != scopeKey)
            return false;
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (await db.Set<FileGovernanceAudit>().AnyAsync(x => x.Id == audit.Id, ct)) return false;
            var archive = await db.Set<FileHistoryArchive>().SingleOrDefaultAsync(x => x.Id == archiveId && x.ScopeKey == scopeKey, ct);
            if (archive == null) return false;
            switch (archive.Kind)
            {
                case FileHistoryKind.Operation:
                {
                    var source = await db.Set<FileOperation>().SingleOrDefaultAsync(x => x.Id == archive.EntityId &&
                        x.Version == archive.SourceVersion && db.Set<FileRecord>().Any(file => file.Id == x.FileId && file.ScopeKey == scopeKey), ct);
                    if (source == null) return false;
                    await LockVersionsAsync(db, new[] { source }, nameof(FileOperation.Version), x => x.Version,
                        (x, version) => x.Version = version, ct);
                    break;
                }
                case FileHistoryKind.Event:
                {
                    var source = await db.Set<FileOutboxEvent>().SingleOrDefaultAsync(x => x.Id == archive.EntityId &&
                        x.ScopeKey == scopeKey && x.Version == archive.SourceVersion, ct);
                    if (source == null) return false;
                    await LockVersionsAsync(db, new[] { source }, nameof(FileOutboxEvent.Version), x => x.Version,
                        (x, version) => x.Version = version, ct);
                    break;
                }
                case FileHistoryKind.MigrationBatch:
                {
                    var source = await db.Set<FileMigrationBatch>().SingleOrDefaultAsync(x => x.Id == archive.EntityId &&
                        x.ScopeKey == scopeKey && x.Version == archive.SourceVersion, ct);
                    if (source == null) return false;
                    await LockVersionsAsync(db, new[] { source }, nameof(FileMigrationBatch.Version), x => x.Version,
                        (x, version) => x.Version = version, ct);
                    break;
                }
                default:
                    return false;
            }

            db.Remove(archive);
            db.Add(audit);
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
        => row.Id == candidate.Id && row.Version == candidate.Version && row.Delivered &&
           row.CreatedAt == candidate.Time && row.CreatedAt <= cutoff && (row.LeaseToken == null || row.LeaseUntil <= now);

    private static bool IsBatchCandidate(FileMigrationBatch row, FileHistoryCandidate candidate, long cutoff)
        => row.Id == candidate.Id && row.Version == candidate.Version && row.State == FileMigrationBatchState.Completed &&
           row.CreatedAt == candidate.Time && row.CreatedAt <= cutoff;

    private static async Task LockVersionsAsync<TEntity>(DbContext db, IReadOnlyCollection<TEntity> rows,
        string propertyName, Func<TEntity, long> readVersion, Action<TEntity, long> writeVersion, CancellationToken ct)
        where TEntity : class
    {
        var entities = rows.ToArray();
        var versions = entities.Select(readVersion).ToArray();
        for (var index = 0; index < entities.Length; index++)
        {
            writeVersion(entities[index], checked(versions[index] + 1));
            var property = db.Entry(entities[index]).Property(propertyName);
            property.OriginalValue = versions[index];
            property.IsModified = true;
        }
        await db.SaveChangesAsync(ct);
        for (var index = 0; index < entities.Length; index++)
        {
            writeVersion(entities[index], versions[index]);
            var property = db.Entry(entities[index]).Property(propertyName);
            property.OriginalValue = checked(versions[index] + 1);
            property.IsModified = true;
        }
        await db.SaveChangesAsync(ct);
    }
}
