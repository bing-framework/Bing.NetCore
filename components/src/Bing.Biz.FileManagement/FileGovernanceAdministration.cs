using System.Text.Json;

namespace Bing.Biz.FileManagement;

/// <summary>可信管理服务；HTTP 调用方必须先验证 files.manage 并传入真实身份范围。</summary>
public sealed class FileGovernanceAdministration
{
    private readonly IFileManagementStore _store;
    private readonly FileManagementOptions _options;
    private readonly HashSet<string> _consumers;
    public FileGovernanceAdministration(IFileManagementStore store, FileManagementOptions options, IEnumerable<IFileLifecycleEventHandler> handlers)
    { _store = store; _options = options; _consumers = handlers.Select(x => x.Name).ToHashSet(StringComparer.Ordinal); }

    public IReadOnlyCollection<string> Consumers => _consumers.OrderBy(x => x, StringComparer.Ordinal).ToArray();

    public async Task<IReadOnlyList<FileEventView>> ListEventsAsync(FileEventQuery query, CancellationToken ct = default)
    {
        Page(query.ScopeKey, query.Skip, query.Take);
        return (await _store.QueryEventsAsync(query, ct)).Select(View).ToArray();
    }
    public async Task<(FileEventView Event, IReadOnlyList<FileEventDelivery> Deliveries)> GetEventAsync(string scopeKey, Guid id, CancellationToken ct = default)
    {
        var value = await RequireEventAsync(scopeKey, id, ct);
        return (View(value), await _store.ListEventDeliveriesAsync(id, ct));
    }
    public async Task<FileGovernanceAudit> ReplayAsync(string scopeKey, string actor, Guid id, ReplayFileEvent request, CancellationToken ct = default)
    {
        Validate(scopeKey, actor, request.RequestId, request.Reason);
        if (request.ExpectedVersion < 1 || !FileOutboxDispatcher.ValidConsumerName(request.Consumer)) throw Invalid();
        var audit = Audit(scopeKey, actor, request.RequestId, "event-replay", id, request.Reason, request);
        var previous = await PreviousAsync(audit, ct); if (previous != null) return previous;
        if (!_consumers.Contains(request.Consumer)) throw new FileManagementException("invalid", "重放目标消费者尚未注册。");
        var original = await RequireEventAsync(scopeKey, id, ct);
        if (original.Version != request.ExpectedVersion || original.LeaseToken != null && original.LeaseUntil > FileManagementService.Now) throw Conflict();
        var rows = (await _store.ListEventDeliveriesAsync(id, ct)).ToList();
        var receipt = rows.SingleOrDefault(x => x.Consumer == request.Consumer);
        if (receipt == null)
        {
            // 仅显式管理操作允许为冻结的历史事件添加新消费者。
            receipt = new FileEventDelivery { EventId = id, Consumer = request.Consumer }; rows.Add(receipt);
        }
        receipt.Delivered = false; receipt.Attempts = 0; receipt.CompletedAt = 0; receipt.NextAttemptAt = FileManagementService.Now; receipt.ErrorCode = null;
        var updated = FileOutboxDispatcher.Clone(original);
        updated.Delivered = false; updated.NextAttemptAt = receipt.NextAttemptAt; updated.ErrorCode = null;
        updated.LeaseToken = null; updated.LeaseUntil = 0; updated.Version++;
        audit.ResultJson = JsonSerializer.Serialize(new { eventId = id, consumer = request.Consumer, version = updated.Version });
        if (await _store.TryUpdateEventDeliveryAsync(original, updated, rows, null, audit, ct)) return audit;
        return await PreviousAsync(audit, ct) ?? throw Conflict();
    }

    public async Task<FileHistorySelection> PreviewAsync(string scopeKey, FileHistoryKind kind, int? retentionDays = null, int take = 20, CancellationToken ct = default)
    {
        Page(scopeKey, 0, take);
        var days = retentionDays ?? _options.HistoryRetentionDays;
        if (!Enum.IsDefined(typeof(FileHistoryKind), kind) || days < _options.HistoryRetentionDays || days > 3650) throw Invalid();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeMilliseconds();
        return new() { Kind = kind, Cutoff = cutoff, Items = await _store.QueryHistoryCandidatesAsync(scopeKey, kind, cutoff, take, ct) };
    }
    public async Task<FileGovernanceAudit> ArchiveAsync(string scopeKey, string actor, FileHistorySelection selection, FileGovernanceRequest request, CancellationToken ct = default)
    {
        Validate(scopeKey, actor, request.RequestId, request.Reason);
        if (!Enum.IsDefined(typeof(FileHistoryKind), selection.Kind) || selection.Cutoff <= 0 ||
            selection.Cutoff > DateTimeOffset.UtcNow.AddDays(-_options.HistoryRetentionDays).ToUnixTimeMilliseconds() ||
            selection.Items.Count is < 1 or > 100 || selection.Items.Any(x => x.Id == Guid.Empty || x.Version < 1 || x.Time <= 0 || x.Time > selection.Cutoff) ||
            selection.Items.Select(x => x.Id).Distinct().Count() != selection.Items.Count) throw Invalid();
        var audit = Audit(scopeKey, actor, request.RequestId, "history-archive", Guid.Empty, request.Reason, selection);
        var previous = await PreviousAsync(audit, ct); if (previous != null) return previous;
        audit.ResultJson = JsonSerializer.Serialize(new { count = selection.Items.Count, kind = selection.Kind });
        if (await _store.TryArchiveHistoryAsync(scopeKey, selection, audit, ct)) return audit;
        return await PreviousAsync(audit, ct) ?? throw Conflict();
    }
    public Task<IReadOnlyList<FileHistoryArchive>> ListArchivesAsync(string scopeKey, FileHistoryKind? kind, int skip, int take, CancellationToken ct = default)
    {
        Page(scopeKey, skip, take);
        if (kind.HasValue && !Enum.IsDefined(typeof(FileHistoryKind), kind.Value)) throw Invalid();
        return _store.ListHistoryArchivesAsync(scopeKey, kind, skip, take, ct);
    }
    public async Task<FileGovernanceAudit> RestoreAsync(string scopeKey, string actor, Guid archiveId, FileGovernanceRequest request, CancellationToken ct = default)
    {
        Validate(scopeKey, actor, request.RequestId, request.Reason);
        if (archiveId == Guid.Empty) throw Invalid();
        var audit = Audit(scopeKey, actor, request.RequestId, "history-restore", archiveId, request.Reason, new { archiveId });
        var previous = await PreviousAsync(audit, ct); if (previous != null) return previous;
        audit.ResultJson = JsonSerializer.Serialize(new { archiveId });
        if (await _store.TryRestoreHistoryAsync(scopeKey, archiveId, audit, ct)) return audit;
        return await PreviousAsync(audit, ct) ?? throw Conflict();
    }
    public Task<IReadOnlyList<FileGovernanceAudit>> ListAuditAsync(string scopeKey, Guid? targetId, int skip, int take, CancellationToken ct = default)
    { Page(scopeKey, skip, take); return _store.ListGovernanceAuditAsync(scopeKey, targetId, skip, take, ct); }

    private async Task<FileGovernanceAudit?> PreviousAsync(FileGovernanceAudit request, CancellationToken ct)
    {
        var previous = await _store.FindGovernanceAuditAsync(request.Id, ct);
        if (previous == null) return null;
        if (previous.ScopeKey != request.ScopeKey || previous.ActorId != request.ActorId || previous.Action != request.Action ||
            previous.TargetId != request.TargetId || previous.Reason != request.Reason || previous.RequestJson != request.RequestJson) throw Conflict();
        return previous;
    }
    private async Task<FileOutboxEvent> RequireEventAsync(string scopeKey, Guid id, CancellationToken ct)
    {
        var value = await _store.FindEventAsync(id, ct);
        if (value == null || value.ScopeKey != scopeKey) throw new FileManagementException("not_found", "事件不存在。");
        return value;
    }
    private static FileGovernanceAudit Audit(string scope, string actor, Guid id, string action, Guid target, string reason, object request) => new()
    { Id = id, ScopeKey = scope, ActorId = actor, Action = action, TargetId = target, Reason = reason, CreatedAt = FileManagementService.Now, RequestJson = JsonSerializer.Serialize(request) };
    private static void Validate(string scope, string actor, Guid id, string reason)
    {
        if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(actor) || actor.Length > 200 || id == Guid.Empty ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || reason.Any(char.IsControl)) throw Invalid();
    }
    private static void Page(string scope, int skip, int take)
    { if (string.IsNullOrWhiteSpace(scope) || skip < 0 || take is < 1 or > 100) throw Invalid(); }
    private static FileManagementException Invalid() => new("invalid", "治理请求参数无效，归档期限不得低于配置保留期。");
    private static FileManagementException Conflict() => new("conflict", "记录、租约或请求标识已变化，请刷新后重试。");
    private static FileEventView View(FileOutboxEvent value) => new()
    { Id = value.Id, FileId = value.FileId, Kind = value.Kind, CreatedAt = value.CreatedAt, Version = value.Version, Delivered = value.Delivered,
        NextAttemptAt = value.NextAttemptAt, Processing = value.LeaseToken != null && value.LeaseUntil > FileManagementService.Now, ErrorCode = value.ErrorCode };
}
