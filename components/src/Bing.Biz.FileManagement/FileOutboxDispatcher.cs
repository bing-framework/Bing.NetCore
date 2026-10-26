namespace Bing.Biz.FileManagement;

public sealed class FileLifecycleEvent
{
    public Guid Id { get; init; }
    public Guid FileId { get; init; }
    public string ScopeKey { get; init; } = "";
    public long FileVersion { get; init; }
    public string Kind { get; init; } = "";
    public long CreatedAt { get; init; }
}
/// <summary>至少一次消费；处理器必须按事件 ID 幂等，不能假定回调成功后投递确认一定到达。</summary>
public interface IFileLifecycleEventHandler
{
    string Name { get; }
    Task HandleAsync(FileLifecycleEvent message, CancellationToken ct);
}

public sealed class FileOutboxDispatcher
{
    private readonly IFileManagementStore _store;
    private readonly FileManagementOptions _options;
    private readonly IFileLifecycleEventHandler[] _handlers;
    public FileOutboxDispatcher(IFileManagementStore store, FileManagementOptions options, IEnumerable<IFileLifecycleEventHandler> handlers)
    {
        _store = store; _options = options; _handlers = handlers.ToArray();
        if (_handlers.Any(h => !ValidConsumerName(h.Name)) || _handlers.Select(h => h.Name).Distinct(StringComparer.Ordinal).Count() != _handlers.Length)
            throw new ArgumentException("事件消费者名称不能为空或重复。");
    }
    public async Task<int> DispatchAsync(CancellationToken ct = default)
    {
        // 无消费者不丢弃事件，后续配置消费者可以从持久队列继续。
        if (_handlers.Length == 0) return 0;
        var delivered = 0;
        foreach (var candidate in await _store.ListDueEventsAsync(FileManagementService.Now, 10, ct))
        {
            var token = Guid.NewGuid().ToString("N"); var now = FileManagementService.Now;
            if (!await _store.TryClaimEventAsync(candidate.Id, candidate.Version, token, now, now + (long)_options.EventLease.TotalMilliseconds, ct)) continue;
            using var work = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var stop = new CancellationTokenSource();
            var heartbeat = RenewAsync(candidate.Id, token, work, stop.Token);
            try
            {
                var message = new FileLifecycleEvent { Id = candidate.Id, FileId = candidate.FileId, ScopeKey = candidate.ScopeKey,
                    FileVersion = candidate.FileVersion, Kind = candidate.Kind, CreatedAt = candidate.CreatedAt };
                // 首次投递持久化消费者集合；部署新增消费者不改变已经冻结的历史事件。
                if (!await UpdateAsync(candidate.Id, token, (value, receipts) =>
                {
                    if (receipts.Count == 0) receipts.AddRange(_handlers.Select(h => new FileEventDelivery { EventId = value.Id, Consumer = h.Name }));
                }, work.Token)) continue;
                foreach (var receipt in await _store.ListEventDeliveriesAsync(candidate.Id, work.Token))
                {
                    if (receipt.Delivered || receipt.NextAttemptAt > FileManagementService.Now) continue;
                    work.Token.ThrowIfCancellationRequested();
                    if (receipt.Attempts >= _options.MaxEventDeliveryAttempts)
                    {
                        if (!await UpdateAsync(candidate.Id, token, (_, rows) =>
                        {
                            var row = rows.Single(x => x.Consumer == receipt.Consumer);
                            row.NextAttemptAt = long.MaxValue; row.ErrorCode = "attempts_exhausted";
                        }, work.Token)) break;
                        continue;
                    }
                    // 先持久化尝试，进程崩溃同样计入重试上限；成功确认丢失仍可能重复调用。
                    if (!await UpdateAsync(candidate.Id, token, (_, rows) =>
                    {
                        var row = rows.Single(x => x.Consumer == receipt.Consumer);
                        row.Attempts++; row.ErrorCode = "outcome_unknown";
                        row.NextAttemptAt = RetryAt(row.Attempts);
                    }, work.Token)) break;
                    string? error = null;
                    var handler = _handlers.SingleOrDefault(h => h.Name == receipt.Consumer);
                    using var attempt = CancellationTokenSource.CreateLinkedTokenSource(work.Token);
                    attempt.CancelAfter(_options.EventHandlerTimeout);
                    try
                    {
                        if (handler == null) error = "handler_unavailable";
                        else await handler.HandleAsync(message, attempt.Token).WaitAsync(attempt.Token);
                    }
                    catch (OperationCanceledException) when (work.IsCancellationRequested) { throw; }
                    catch (Exception ex) { error = ex is OperationCanceledException ? "handler_timeout" : "handler_failed"; }
                    if (!await UpdateAsync(candidate.Id, token, (_, rows) =>
                    {
                        var row = rows.Single(x => x.Consumer == receipt.Consumer);
                        row.Delivered = error == null; row.ErrorCode = error;
                        row.CompletedAt = error == null ? FileManagementService.Now : 0;
                        row.NextAttemptAt = error == null ? 0 : row.Attempts >= _options.MaxEventDeliveryAttempts ? long.MaxValue : RetryAt(row.Attempts);
                    }, work.Token)) break;
                }
                if (await UpdateAsync(candidate.Id, token, (value, rows) =>
                {
                    value.Delivered = rows.Count > 0 && rows.All(x => x.Delivered);
                    value.NextAttemptAt = value.Delivered ? 0 : rows.Where(x => !x.Delivered).Min(x => x.NextAttemptAt);
                    value.ErrorCode = value.Delivered ? null : value.NextAttemptAt == long.MaxValue ? "attempts_exhausted" : "consumer_pending";
                    value.LeaseToken = null; value.LeaseUntil = 0;
                }, work.Token))
                {
                    if ((await _store.FindEventAsync(candidate.Id, ct))?.Delivered == true) delivered++;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (OperationCanceledException) when (work.IsCancellationRequested) { /* 租约失效，保留已提交回执供下一工作进程恢复。 */ }
            finally { stop.Cancel(); await heartbeat; }
        }
        return delivered;
    }

    /// <summary>消费者使用稳定的小写 ASCII 标识，避免数据库排序规则造成名称歧义。</summary>
    public static bool ValidConsumerName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 100 &&
        name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_' or '.');
    private static long RetryAt(int attempts) => FileManagementService.Now + Math.Min(3600000, 1000L << Math.Min(attempts, 12));

    private async Task<bool> UpdateAsync(Guid id, string token, Action<FileOutboxEvent, List<FileEventDelivery>> change, CancellationToken ct)
    {
        // 心跳也推进事件版本；仅在有效租约内重新读取并有限重试 CAS。
        for (var retry = 0; retry < 5; retry++)
        {
            ct.ThrowIfCancellationRequested();
            var original = await _store.FindEventAsync(id, ct);
            if (original == null || original.Delivered || original.LeaseToken != token || original.LeaseUntil <= FileManagementService.Now) return false;
            var rows = (await _store.ListEventDeliveriesAsync(id, ct)).ToList();
            var updated = Clone(original); change(updated, rows); updated.Version++;
            if (await _store.TryUpdateEventDeliveryAsync(original, updated, rows, token, null, ct)) return true;
        }
        return false;
    }
    internal static FileOutboxEvent Clone(FileOutboxEvent value) => new()
    {
        Id = value.Id, FileId = value.FileId, ScopeKey = value.ScopeKey, FileVersion = value.FileVersion, Kind = value.Kind,
        CreatedAt = value.CreatedAt, NextAttemptAt = value.NextAttemptAt, Attempts = value.Attempts, LeaseToken = value.LeaseToken,
        LeaseUntil = value.LeaseUntil, Delivered = value.Delivered, Version = value.Version, ErrorCode = value.ErrorCode
    };
    private async Task RenewAsync(Guid id, string token, CancellationTokenSource work, CancellationToken stop)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_options.EventLease.TotalMilliseconds / 3), stop);
                if (!await _store.RenewEventLeaseAsync(id, token, FileManagementService.Now + (long)_options.EventLease.TotalMilliseconds, stop))
                { work.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch { work.Cancel(); }
    }
}
