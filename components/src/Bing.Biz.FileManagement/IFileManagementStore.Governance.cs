namespace Bing.Biz.FileManagement;

public partial interface IFileManagementStore
{
    Task<IReadOnlyList<FileOutboxEvent>> QueryEventsAsync(FileEventQuery query, CancellationToken ct = default);
    Task<FileOutboxEvent?> FindEventAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FileEventDelivery>> ListEventDeliveriesAsync(Guid id, CancellationToken ct = default);
    /// <summary>
    /// 原子 CAS 更新事件及完整回执快照。updated.Version 必须为 expected.Version+1。
    /// leaseToken 非空时必须匹配当前有效租约；为空时拒绝活动租约且必须附带管理审计。
    /// 审计重复返回 false；审计与更新同事务。带审计的重放同时清除该事件归档标记。
    /// 更新不得改变事件不可变字段。回执消费者名唯一；消费期间不得删掉已有回执。
    /// </summary>
    Task<bool> TryUpdateEventDeliveryAsync(FileOutboxEvent expected, FileOutboxEvent updated,
        IReadOnlyList<FileEventDelivery> deliveries, string? leaseToken, FileGovernanceAudit? audit, CancellationToken ct = default);
    Task<FileGovernanceAudit?> FindGovernanceAuditAsync(Guid requestId, CancellationToken ct = default);
    Task<IReadOnlyList<FileGovernanceAudit>> ListGovernanceAuditAsync(string scopeKey, Guid? targetId, int skip, int take, CancellationToken ct = default);
    /// <summary>
    /// SQL 内筛选、排序和限量，排除匹配版本的归档。Operation: Succeeded、FinishedAt正且不晚于cutoff、无有效租约；
    /// Event: Delivered、CreatedAt不晚于cutoff、无有效租约；Batch: Completed、CreatedAt不晚于cutoff、无Pending/Queued条目。
    /// 返回 Time 分别为 FinishedAt/CreatedAt，按 Time、Id 升序。Take 1～100。
    /// </summary>
    Task<IReadOnlyList<FileHistoryCandidate>> QueryHistoryCandidatesAsync(string scopeKey, FileHistoryKind kind, long cutoff, int take, CancellationToken ct = default);
    /// <summary>同事务重新检查全部候选范围/终态/版本/时间/租约及未归档，锁定源版本后写标记与审计。任一冲突全部回滚；源版本不变化。</summary>
    Task<bool> TryArchiveHistoryAsync(string scopeKey, FileHistorySelection selection, FileGovernanceAudit audit, CancellationToken ct = default);
    /// <summary>仅返回源版本仍匹配的归档，范围过滤后分页。</summary>
    Task<IReadOnlyList<FileHistoryArchive>> ListHistoryArchivesAsync(string scopeKey, FileHistoryKind? kind, int skip, int take, CancellationToken ct = default);
    /// <summary>按随机归档 Id 删除标记并原子记录审计；不同归档周期具有不同 Id，禁止 ABA 恢复。</summary>
    Task<bool> TryRestoreHistoryAsync(string scopeKey, Guid archiveId, FileGovernanceAudit audit, CancellationToken ct = default);
}
