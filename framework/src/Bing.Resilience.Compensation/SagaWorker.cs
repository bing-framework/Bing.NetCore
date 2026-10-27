using System.Text.Json;
using Bing.Resilience.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bing.Resilience.Compensation;

/// <summary>Saga 是步骤日志的唯一调度者；按版本与租约提交，未知副作用先 Confirm。</summary>
public sealed class SagaWorker : BackgroundService
{
    private readonly IResilienceOperationStore _store;
    private readonly IOperationDataProtector _protector;
    private readonly SagaCatalog _handlers;
    private readonly SagaOptions _options;
    private readonly ResilienceOperationCatalog _operations;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<SagaWorker> _logger;
    internal SagaWorker(IResilienceOperationStore store, IOperationDataProtector protector, SagaCatalog handlers, SagaOptions options,
        ResilienceOperationCatalog operations, IServiceScopeFactory scopes, TimeProvider clock, ILogger<SagaWorker> logger)
    {
        _store = store; _protector = protector; _handlers = handlers; _options = options; _operations = operations; _scopes = scopes; _clock = clock; _logger = logger;
        if (!store.IsDurable) throw new ResilienceConfigurationException("Saga Worker 需要持久 Store。");
    }
    private DateTimeOffset _nextCleanup;
    /// <summary>执行一个显式保留批次；未配置时无操作。不会清理在途、未知或人工待处理记录。</summary>
    public ValueTask<OperationRetentionResult> CleanupOnceAsync(CancellationToken token = default)
    {
        if (_options.Retention is null) return ValueTask.FromResult(new OperationRetentionResult(0, 0, 0, 0));
        if (_store is not IOperationRetentionStore retention) throw new ResilienceConfigurationException("Store 不支持终态保留治理。");
        return retention.CleanupAsync("saga", _options.Retention, RedactPayload, token);
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
            catch (Exception error) { _logger.LogError(error, "Saga 本轮执行失败，未完成步骤由持久状态恢复。"); }
            await ResilienceTime.DelayAsync(_clock, _options.PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
    private sealed record Work(OperationRecord Record, SagaStepDocument Document, SagaHandlerRegistration Handler, SagaStepAction Action, int LedgerNumber);
    private sealed record Done(Work Work, SagaStepResult Result, long? Generation, string? Endpoint);
    public async ValueTask<bool> RunOnceAsync(CancellationToken token = default)
    {
        OperationRecord parent;
        SagaDocument document;
        var children = new List<OperationRecord>();
        await using (var tx = await _store.BeginAsync(token).ConfigureAwait(false))
        {
            var claimed = await tx.ClaimDueAsync("saga", new[] { "Pending", "Running", "AwaitingConfirmation", "Compensating" }, _options.LeaseDuration, token).ConfigureAwait(false);
            if (claimed is null) return false;
            parent = claimed;
            await tx.CommitAsync(token).ConfigureAwait(false);
        }
        try
        {
            document = SagaCoordinator.Read(parent, _protector);
            await using var read = await _store.BeginAsync(token).ConfigureAwait(false);
            foreach (var definition in document.Command.Steps)
                children.Add(await read.FindAsync(SagaCoordinator.StepId(parent.Id, definition.Id), token).ConfigureAwait(false)
                    ?? throw new ResilienceConfigurationException("Saga 步骤丢失。"));
            await read.CommitAsync(token).ConfigureAwait(false);
        }
        catch (ResilienceConfigurationException error)
        {
            await FinishCorrupt(parent, error.Message, token).ConfigureAwait(false);
            return true;
        }
        try
        {
            if (_operations.Get(document.Command.OperationName).ContractVersion != document.Command.ContractVersion ||
                !await _options.Authorize!(document.Command, token).ConfigureAwait(false))
            { await End(parent, document, "ManualIntervention", "Saga 权限或契约已变化", token).ConfigureAwait(false); return true; }
        }
        catch (ResilienceConfigurationException)
        { await End(parent, document, "ManualIntervention", "缺少历史业务契约", token).ConfigureAwait(false); return true; }

        Dictionary<string, SagaStepDocument> docs;
        try { docs = children.ToDictionary(x => x.Id, x => SagaCoordinator.ReadStep(x, _protector)); }
        catch (ResilienceConfigurationException error)
        { await FinishCorrupt(parent, error.Message, token).ConfigureAwait(false); return true; }
        if (!document.Compensating && children.Any(c => c.State == "Failed")) document = document with { Compensating = true };
        var candidates = Select(children, docs, document).ToArray();
        if (candidates.Length == 0)
        {
            var terminal = document.Compensating
                ? children.Any(c => c.State is "Succeeded" or "CompensationPending" or "CompensationRetryScheduled" or "CompensationUnknown" or "Compensating" or "ConfirmingCompensation") ? "ManualIntervention" : "Compensated"
                : children.All(c => c.State is "Succeeded" or "Skipped") ? "Completed" : "ManualIntervention";
            await End(parent, document, terminal, "步骤集合达到终态", token).ConfigureAwait(false); return true;
        }
        if (parent.Deadline <= _clock.GetUtcNow())
        {
            await End(parent, document, "ManualIntervention", "Saga 已到期，必须明确处理已有副作用", token).ConfigureAwait(false);
            return true;
        }
        if (candidates.Any(x => x.Attempts >= Math.Max(_options.MaxAttemptsPerStep, docs[x.Id].ManualAttemptLimit ?? 0) || document.Compensating && !docs[x.Id].Definition.CanCompensate))
        {
            await End(parent, document, "ManualIntervention", "步骤尝试耗尽或业务不支持撤销", token).ConfigureAwait(false); return true;
        }
        var earliest = candidates.Min(x => x.NextAttemptAt);
        if (earliest > _clock.GetUtcNow())
        { await End(parent, document, document.Compensating ? "Compensating" : "Running", "等待下一次尝试", token, earliest).ConfigureAwait(false); return true; }
        candidates = candidates.Where(x => x.NextAttemptAt <= _clock.GetUtcNow()).ToArray();
        Work[] work;
        try
        {
            work = candidates.Select((child, index) =>
            {
                var action = child.State switch
                {
                    "Executing" or "Unknown" or "Confirming" => SagaStepAction.ConfirmForward,
                    "Compensating" or "CompensationUnknown" or "ConfirmingCompensation" => SagaStepAction.ConfirmCompensation,
                    _ => document.Compensating ? SagaStepAction.Compensate : SagaStepAction.Forward
                };
                return new Work(child, docs[child.Id], _handlers.Get(docs[child.Id].Definition), action, parent.Attempts + index + 1);
            }).ToArray();
        }
        catch (ResilienceConfigurationException)
        { await End(parent, document, "ManualIntervention", "缺少历史步骤处理器", token).ConfigureAwait(false); return true; }
        // 本地事务步骤串行执行，避免并发共用同一 DbConnection。
        if (work.Any(w => w.Handler.UsesLocalTransaction)) work = work.Take(1).ToArray();
        var running = parent with { Version = parent.Version + 1, Attempts = parent.Attempts + work.Length, UpdatedAt = _clock.GetUtcNow(),
            State = document.Compensating ? "Compensating" : "Running", Data = Protect(document) };
        await using (var tx = await _store.BeginAsync(token).ConfigureAwait(false))
        {
            if (!await tx.CompareExchangeAsync(running, parent.Version, parent.LeaseToken, token).ConfigureAwait(false)) throw new OperationConcurrencyException(parent.Id);
            for (var i = 0; i < work.Length; i++)
            {
                var current = work[i];
                var state = current.Action switch
                {
                    SagaStepAction.Forward => "Executing", SagaStepAction.ConfirmForward => "Confirming",
                    SagaStepAction.Compensate => "Compensating", _ => "ConfirmingCompensation"
                };
                var updated = current.Record with { Version = current.Record.Version + 1, State = state, Attempts = current.Record.Attempts + 1, UpdatedAt = _clock.GetUtcNow() };
                if (!await tx.CompareExchangeAsync(updated, current.Record.Version, token: token).ConfigureAwait(false)) throw new OperationConcurrencyException(updated.Id);
                await tx.AppendAttemptAsync(new(parent.Id, current.LedgerNumber, current.Action + ":" + current.Document.Definition.Id,
                    running.LeaseToken!, _clock.GetUtcNow()), token).ConfigureAwait(false);
                work[i] = current with { Record = updated };
            }
            await tx.CommitAsync(token).ConfigureAwait(false);
        }
        ResilienceDiagnostics.RecordLifecycle("saga.state", running.State);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var heartbeat = new CancellationTokenSource();
        var renewal = Renew(running, execution, heartbeat.Token);
        IOperationTransaction? localTransaction = null;
        Done[] completed;
        try
        {
            if (work[0].Handler.UsesLocalTransaction)
            {
                localTransaction = await _store.BeginAsync(execution.Token).ConfigureAwait(false);
                completed = new[] { await Invoke(work[0], document.Command, localTransaction, execution.Token).ConfigureAwait(false) };
                if (completed[0].Result.Outcome != SagaOutcome.Succeeded)
                {
                    await localTransaction.DisposeAsync().ConfigureAwait(false); localTransaction = null;
                }
            }
            else completed = await Task.WhenAll(work.Select(w => Invoke(w, document.Command, null, execution.Token))).ConfigureAwait(false);
        }
        catch
        {
            if (localTransaction is not null) await localTransaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally { heartbeat.Cancel(); await renewal.ConfigureAwait(false); }

        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var final = localTransaction ?? await _store.BeginAsync(budget.Token).ConfigureAwait(false);
        var compensating = document.Compensating;
        var awaiting = false; var manual = false;
        var nextAt = _clock.GetUtcNow();
        foreach (var item in completed)
        {
            var action = item.Work.Action;
            var isCompensation = action is SagaStepAction.Compensate or SagaStepAction.ConfirmCompensation;
            var isConfirmation = action is SagaStepAction.ConfirmForward or SagaStepAction.ConfirmCompensation;
            var outcome = item.Result.Outcome;
            string state;
            switch (outcome)
            {
                case SagaOutcome.Succeeded: state = isCompensation ? "Compensated" : "Succeeded"; break;
                case SagaOutcome.PartialEffect:
                    if (isCompensation) { state = "CompensationUnknown"; manual = true; }
                    else { state = "Succeeded"; compensating = true; }
                    break;
                case SagaOutcome.FailedNoEffect:
                    if (isCompensation) { state = "CompensationFailed"; manual = true; }
                    else if (item.Work.Document.Definition.Optional) state = "Skipped";
                    else { state = "Failed"; compensating = true; }
                    break;
                case SagaOutcome.RetryableNoEffect: state = isCompensation ? "CompensationRetryScheduled" : "RetryScheduled"; break;
                default:
                    if (isConfirmation && item.Work.Document.Definition.AllowSameKeyRecovery)
                        state = isCompensation ? "CompensationRetryScheduled" : "RetryScheduled";
                    else
                    {
                        state = isCompensation ? "CompensationUnknown" : "Unknown";
                        if (isConfirmation) manual = true; else awaiting = true;
                    }
                    break;
            }
            var delay = item.Result.RetryAfter ?? _options.RetryDelay;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            if (delay > TimeSpan.FromDays(30)) delay = TimeSpan.FromDays(30);
            var childNext = state is "RetryScheduled" or "CompensationRetryScheduled" or "Unknown" or "CompensationUnknown" ? _clock.GetUtcNow() + delay : _clock.GetUtcNow();
            if (childNext > nextAt) nextAt = childNext;
            var child = item.Work.Record with { Version = item.Work.Record.Version + 1, State = state, UpdatedAt = _clock.GetUtcNow(), NextAttemptAt = childNext,
                Data = Protect(item.Work.Document with { Output = item.Result.Output ?? item.Work.Document.Output, LastReason = item.Result.Reason }) };
            if (!await final.CompareExchangeAsync(child, item.Work.Record.Version, token: budget.Token).ConfigureAwait(false)) throw new OperationConcurrencyException(child.Id);
            await final.RecordAttemptRouteAsync(running.Id, item.Work.LedgerNumber, running.LeaseToken!, item.Generation, item.Endpoint, budget.Token).ConfigureAwait(false);
            await final.CompleteAttemptAsync(running.Id, item.Work.LedgerNumber, running.LeaseToken!, outcome.ToString(), _clock.GetUtcNow(), budget.Token).ConfigureAwait(false);
        }
        var resultDocument = document with { Compensating = compensating };
        var updatedParent = running with { Version = running.Version + 1, State = manual ? "ManualIntervention" : awaiting ? "AwaitingConfirmation" : compensating ? "Compensating" : "Running",
            LeaseToken = null, LeaseUntil = null, NextAttemptAt = nextAt, UpdatedAt = _clock.GetUtcNow(), Data = Protect(resultDocument) };
        if (!await final.CompareExchangeAsync(updatedParent, running.Version, running.LeaseToken, budget.Token).ConfigureAwait(false)) throw new OperationConcurrencyException(running.Id);
        await final.AppendAuditAsync(new(running.Id, updatedParent.Version, "saga.progress", "worker", updatedParent.State, _clock.GetUtcNow()), budget.Token).ConfigureAwait(false);
        await final.CommitAsync(budget.Token).ConfigureAwait(false);
        ResilienceDiagnostics.RecordLifecycle("saga.state", updatedParent.State);
        return true;
    }
    private IEnumerable<OperationRecord> Select(IReadOnlyList<OperationRecord> children, IReadOnlyDictionary<string, SagaStepDocument> docs, SagaDocument saga)
    {
        var unknown = children.Where(c => c.State is "Executing" or "Unknown" or "Confirming").Take(1).ToArray();
        if (unknown.Length > 0) return unknown;
        if (!saga.Compensating)
            return children.Where(c => c.State is not ("Succeeded" or "Skipped")).Take(1);
        var pending = children.Where(c => c.State is "Succeeded" or "CompensationPending" or "CompensationRetryScheduled" or "CompensationUnknown" or "Compensating" or "ConfirmingCompensation").ToArray();
        if (!saga.Command.UseCompensationDag) return Enumerable.Reverse(pending).Take(1);
        var outstanding = pending.Select(x => docs[x.Id].Definition.Id).ToHashSet(StringComparer.Ordinal);
        return pending.Where(c => (docs[c.Id].Definition.CompensateAfter ?? Array.Empty<string>()).All(id => !outstanding.Contains(id)))
            .Take(_options.MaxParallelCompensations);
    }
    private async Task<Done> Invoke(Work work, SagaCommand command, IOperationTransaction? transaction, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        var remaining = command.Deadline - _clock.GetUtcNow();
        var duration = remaining < _options.AttemptTimeout ? remaining : _options.AttemptTimeout;
        using var timer = _clock.CreateTimer(_ => { try { timeout.Cancel(); } catch (ObjectDisposedException) { } }, null, duration > TimeSpan.Zero ? duration : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        using var attempt = new ResilienceDurableAttemptScope();
        SagaStepResult result;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var handler = work.Handler.Factory(scope.ServiceProvider);
            var context = new SagaStepContext(command, work.Document.Definition, work.Document.ForwardKey, work.Document.CompensationKey,
                work.Document.Output, work.Record.Attempts, work.Action, transaction);
            timeout.Token.ThrowIfCancellationRequested();
            result = work.Action switch
            {
                SagaStepAction.Forward => await handler.ForwardAsync(context, timeout.Token).ConfigureAwait(false),
                SagaStepAction.Compensate => await handler.CompensateAsync(context, timeout.Token).ConfigureAwait(false),
                _ => await handler.ConfirmAsync(context, timeout.Token).ConfigureAwait(false)
            };
            // 本地业务在被取消后不能继续提交；外部成功则仍记录明确成功事实。
            if (transaction is not null) timeout.Token.ThrowIfCancellationRequested();
        }
        catch (Exception error) { result = new(SagaOutcome.Unknown, Reason: error is OperationCanceledException ? "cancelled_or_timeout" : "handler_exception"); }
        return new(work, result, attempt.Generation, attempt.Endpoint);
    }
    private string Protect<T>(T value) => _protector.Protect(JsonSerializer.Serialize(value));
    private async ValueTask End(OperationRecord parent, SagaDocument document, string state, string reason, CancellationToken token, DateTimeOffset? next = null)
    {
        await using var tx = await _store.BeginAsync(token).ConfigureAwait(false);
        var updated = parent with { Version = parent.Version + 1, State = state, LeaseToken = null, LeaseUntil = null,
            Data = Protect(document), UpdatedAt = _clock.GetUtcNow(), NextAttemptAt = next ?? _clock.GetUtcNow() };
        if (!await tx.CompareExchangeAsync(updated, parent.Version, parent.LeaseToken, token).ConfigureAwait(false)) throw new OperationConcurrencyException(parent.Id);
        await tx.AppendAuditAsync(new(parent.Id, updated.Version, "saga." + state.ToLowerInvariant(), "worker", reason, _clock.GetUtcNow()), token).ConfigureAwait(false);
        await tx.CommitAsync(token).ConfigureAwait(false);
        ResilienceDiagnostics.RecordLifecycle("saga.state", updated.State);
    }
    private async ValueTask FinishCorrupt(OperationRecord parent, string reason, CancellationToken token)
    {
        await using var tx = await _store.BeginAsync(token).ConfigureAwait(false);
        var updated = parent with { Version = parent.Version + 1, State = "ManualIntervention", LeaseToken = null,
            LeaseUntil = null, UpdatedAt = _clock.GetUtcNow(), NextAttemptAt = _clock.GetUtcNow() };
        if (!await tx.CompareExchangeAsync(updated, parent.Version, parent.LeaseToken, token).ConfigureAwait(false))
            throw new OperationConcurrencyException(parent.Id);
        await tx.AppendAuditAsync(new(parent.Id, updated.Version, "saga.manualintervention", "worker", reason, _clock.GetUtcNow()), token).ConfigureAwait(false);
        await tx.CommitAsync(token).ConfigureAwait(false);
        ResilienceDiagnostics.RecordLifecycle("saga.state", updated.State);
    }
    private string RedactPayload(OperationRecord record)
    {
        if (record.Kind == "saga")
        {
            var doc = SagaCoordinator.Read(record, _protector);
            return Protect(doc with { Command = doc.Command with { IdempotencyKey = "",
                Steps = doc.Command.Steps.Select(s => s with { Input = "" }).ToArray() } });
        }
        var step = SagaCoordinator.ReadStep(record, _protector);
        return Protect(step with { Definition = step.Definition with { Input = "" }, Output = null, LastReason = null });
    }
    private async Task Renew(OperationRecord parent, CancellationTokenSource execution, CancellationToken token)
    {
        try
        {
            while (true)
            {
                await ResilienceTime.DelayAsync(_clock, TimeSpan.FromTicks(_options.LeaseDuration.Ticks / 3), token).ConfigureAwait(false);
                await using var tx = await _store.BeginAsync(token).ConfigureAwait(false);
                if (!await tx.RenewLeaseAsync(parent.Id, parent.Version, parent.LeaseToken!, _options.LeaseDuration, token).ConfigureAwait(false))
                { execution.Cancel(); return; }
                await tx.CommitAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { execution.Cancel(); _logger.LogWarning(error, "Saga 续租失败，旧 Worker 停止提交。"); }
    }
}
