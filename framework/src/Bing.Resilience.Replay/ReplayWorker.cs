using System.Text.Json;
using Bing.Resilience.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bing.Resilience.Replay;

/// <summary>每次认领只执行一次 Forward 或 Confirm；账本持久化在调用之前，所有状态回写受租约隔离。</summary>
public sealed class ReplayWorker : BackgroundService
{
    private readonly IResilienceOperationStore _store;
    private readonly IOperationDataProtector _protector;
    private readonly ReplayCatalog _handlers;
    private readonly ReplayOptions _options;
    private readonly ResilienceOperationCatalog _operations;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReplayWorker> _logger;
    internal ReplayWorker(IResilienceOperationStore store, IOperationDataProtector protector, ReplayCatalog handlers,
        ReplayOptions options, ResilienceOperationCatalog operations, IServiceScopeFactory scopes, TimeProvider clock, ILogger<ReplayWorker> logger)
    {
        _store = store; _protector = protector; _handlers = handlers; _options = options; _operations = operations;
        _scopes = scopes; _clock = clock; _logger = logger;
        if (!store.IsDurable) throw new ResilienceConfigurationException("Worker 需要持久 Store。");
    }
    private DateTimeOffset _nextCleanup;
    /// <summary>执行一个显式保留批次；未配置时无操作。不会清理在途、未知或人工待处理记录。</summary>
    public ValueTask<OperationRetentionResult> CleanupOnceAsync(CancellationToken token = default)
    {
        if (_options.Retention is null) return ValueTask.FromResult(new OperationRetentionResult(0, 0, 0, 0));
        if (_store is not IOperationRetentionStore retention) throw new ResilienceConfigurationException("Store 不支持终态保留治理。");
        return retention.CleanupAsync("replay", _options.Retention, RedactPayload, token);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.StartWorker) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Retention is not null && _clock.GetUtcNow() >= _nextCleanup)
                {
                    await CleanupOnceAsync(stoppingToken).ConfigureAwait(false);
                    _nextCleanup = _clock.GetUtcNow() + TimeSpan.FromMinutes(1);
                }
                if (await RunOnceAsync(stoppingToken).ConfigureAwait(false)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { _logger.LogError(error, "持久重放 Worker 本轮失败；已提交的认领将在租约到期后恢复。"); }
            await ResilienceTime.DelayAsync(_clock, _options.PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
    public async ValueTask<bool> RunOnceAsync(CancellationToken token = default)
    {
        OperationRecord? claimed;
        await using (var transaction = await _store.BeginAsync(token).ConfigureAwait(false))
        {
            claimed = await transaction.ClaimDueAsync("replay", new[] { "Pending", "RetryScheduled", "Executing", "Unknown", "Confirming" }, _options.LeaseDuration, token).ConfigureAwait(false);
            if (claimed is null) return false;
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }
        var record = claimed;
        ReplayDocument document;
        ReplayHandlerRegistration registration;
        try
        {
            document = ReplayCoordinator.Read(record, _protector);
            registration = _handlers.Get(document.Command);
            var operation = _operations.Get(document.Command.OperationName);
            if (operation.ContractVersion != document.Command.ContractVersion || operation.Kind != ResilienceOperationKind.IdempotentWrite ||
                !await _options.Authorize!(document.Command, token).ConfigureAwait(false))
            { await FinishWithoutAttempt(record, "ManualIntervention", "业务权限或契约已变化", token).ConfigureAwait(false); return true; }
        }
        catch (ResilienceConfigurationException)
        { await FinishWithoutAttempt(record, "ManualIntervention", "历史处理器、契约或密钥缺失", token).ConfigureAwait(false); return true; }
        var confirming = record.State is "Executing" or "Unknown" or "Confirming";
        if (record.Deadline <= _clock.GetUtcNow() || record.Attempts >= _options.MaxAttempts)
        {
            await FinishWithoutAttempt(record, confirming ? "ManualIntervention" : "Expired", "操作期限或总尝试预算耗尽", token).ConfigureAwait(false);
            return true;
        }
        var now = _clock.GetUtcNow();
        var running = record with { State = confirming ? "Confirming" : "Executing", Version = record.Version + 1, Attempts = record.Attempts + 1, UpdatedAt = now };
        await using (var transaction = await _store.BeginAsync(token).ConfigureAwait(false))
        {
            if (!await transaction.CompareExchangeAsync(running, record.Version, record.LeaseToken, token).ConfigureAwait(false)) throw new OperationConcurrencyException(record.Id);
            await transaction.AppendAttemptAsync(new(record.Id, running.Attempts, confirming ? "confirm" : "forward", running.LeaseToken!, now), token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }
        ResilienceDiagnostics.RecordLifecycle("replay.state", running.State);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(token);
        var remaining = running.Deadline - _clock.GetUtcNow();
        var timeout = remaining < _options.AttemptTimeout ? remaining : _options.AttemptTimeout;
        using var timer = _clock.CreateTimer(_ => { try { execution.Cancel(); } catch (ObjectDisposedException) { } }, null, timeout > TimeSpan.Zero ? timeout : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        using var heartbeat = new CancellationTokenSource();
        var renewal = Renew(running, execution, heartbeat.Token);
        ReplayHandlerResult result;
        using var durableAttempt = new ResilienceDurableAttemptScope();
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var handler = registration.Factory(scope.ServiceProvider);
            execution.Token.ThrowIfCancellationRequested();
            var context = new ReplayExecutionContext(document.Command, running.Attempts, confirming);
            result = confirming
                ? await handler.ConfirmAsync(context, execution.Token).ConfigureAwait(false)
                : await handler.ExecuteAsync(context, execution.Token).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // 超时/取消/连接断开都不能证明远端没有执行；恢复阶段必须先确认。
            result = new(ReplayOutcome.Unknown, Reason: error is OperationCanceledException ? "cancelled_or_timeout" : "handler_exception");
        }
        finally
        {
            heartbeat.Cancel();
            await renewal.ConfigureAwait(false);
        }
        var nextState = result.Outcome switch
        {
            ReplayOutcome.Succeeded => "Succeeded",
            ReplayOutcome.FailedNoEffect => "Failed",
            ReplayOutcome.RetryableNoEffect => "RetryScheduled",
            _ when confirming && registration.AllowSameKeyRecovery => "RetryScheduled",
            _ when confirming => "ManualIntervention",
            _ => "Unknown"
        };
        var delay = result.RetryAfter ?? _options.RetryDelay;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        if (delay > TimeSpan.FromDays(30)) delay = TimeSpan.FromDays(30);
        var updatedDocument = document with { Result = result.Result, LastReason = result.Reason };
        var updated = running with { State = nextState, Version = running.Version + 1, UpdatedAt = _clock.GetUtcNow(),
            NextAttemptAt = _clock.GetUtcNow() + delay, LeaseToken = null, LeaseUntil = null,
            Data = _protector.Protect(JsonSerializer.Serialize(updatedDocument)) };
        // 调用方取消不丢弃已完成的事实；独立短预算只用于受 fencing 保护的状态提交。
        using var finishBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using (var transaction = await _store.BeginAsync(finishBudget.Token).ConfigureAwait(false))
        {
            await transaction.RecordAttemptRouteAsync(running.Id, running.Attempts, running.LeaseToken!, durableAttempt.Generation, durableAttempt.Endpoint, finishBudget.Token).ConfigureAwait(false);
            await transaction.CompleteAttemptAsync(running.Id, running.Attempts, running.LeaseToken!, result.Outcome.ToString(), _clock.GetUtcNow(), finishBudget.Token).ConfigureAwait(false);
            if (!await transaction.CompareExchangeAsync(updated, running.Version, running.LeaseToken, finishBudget.Token).ConfigureAwait(false)) throw new OperationConcurrencyException(running.Id);
            await transaction.AppendAuditAsync(new(updated.Id, updated.Version, "replay." + nextState.ToLowerInvariant(), "worker", result.Reason.Length == 0 ? nextState : result.Reason, _clock.GetUtcNow()), finishBudget.Token).ConfigureAwait(false);
            await transaction.CommitAsync(finishBudget.Token).ConfigureAwait(false);
        }
        ResilienceDiagnostics.RecordLifecycle("replay.state", updated.State);
        return true;
    }
    private string RedactPayload(OperationRecord record)
    {
        var doc = ReplayCoordinator.Read(record, _protector);
        return _protector.Protect(JsonSerializer.Serialize(doc with
        {
            Command = doc.Command with { Payload = "", IdempotencyKey = "" }, Result = null, LastReason = null
        }));
    }
    private async Task Renew(OperationRecord record, CancellationTokenSource execution, CancellationToken stop)
    {
        try
        {
            while (true)
            {
                await ResilienceTime.DelayAsync(_clock, TimeSpan.FromTicks(_options.LeaseDuration.Ticks / 3), stop).ConfigureAwait(false);
                await using var transaction = await _store.BeginAsync(stop).ConfigureAwait(false);
                if (!await transaction.RenewLeaseAsync(record.Id, record.Version, record.LeaseToken!, _options.LeaseDuration, stop).ConfigureAwait(false))
                { execution.Cancel(); return; }
                await transaction.CommitAsync(stop).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error) { execution.Cancel(); _logger.LogWarning(error, "重放续租失败，停止当前尝试；旧租约不能提交结果。"); }
    }
    private async ValueTask FinishWithoutAttempt(OperationRecord record, string state, string reason, CancellationToken token)
    {
        await using var transaction = await _store.BeginAsync(token).ConfigureAwait(false);
        var updated = record with { State = state, Version = record.Version + 1, LeaseToken = null, LeaseUntil = null, UpdatedAt = _clock.GetUtcNow() };
        if (!await transaction.CompareExchangeAsync(updated, record.Version, record.LeaseToken, token).ConfigureAwait(false)) throw new OperationConcurrencyException(record.Id);
        await transaction.AppendAuditAsync(new(record.Id, updated.Version, "replay." + state.ToLowerInvariant(), "worker", reason, _clock.GetUtcNow()), token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        ResilienceDiagnostics.RecordLifecycle("replay.state", updated.State);
    }
}
