using System.Text.Json;
using Bing.Resilience.Operations;
namespace Bing.Resilience.Compensation;

internal sealed class SagaCoordinator(IResilienceOperationStore store, IOperationDataProtector protector, SagaCatalog handlers,
    SagaOptions options, ResilienceOperationCatalog operations, TimeProvider clock) : ISagaCoordinator
{
    public async ValueTask<DurableOperationReceipt> StartAsync(SagaCommand command, CancellationToken token = default)
    {
        await using (var tx = await store.BeginAsync(token).ConfigureAwait(false))
        {
            await StartInTransactionAsync(tx, command, token).ConfigureAwait(false);
            await tx.CommitAsync(token).ConfigureAwait(false);
        }
        ResilienceDiagnostics.RecordLifecycle("saga.state", "accepted");
        return await DurableOperationReceipt.VerifyCommittedAsync(store, command.SagaId, token).ConfigureAwait(false);
    }
    public async ValueTask<OperationRecord> StartInTransactionAsync(IOperationTransaction tx, SagaCommand command, CancellationToken token = default)
    {
        if (!store.IsDurable) throw new ResilienceConfigurationException("Saga 需要持久 Store。");
        command = command with { Steps = Array.AsReadOnly(command.Steps.Select(s => s with
            { CompensateAfter = s.CompensateAfter is null ? null : Array.AsReadOnly(s.CompensateAfter.ToArray()) }).ToArray()) };
        var operation = operations.Get(command.OperationName);
        if (operation.Kind != ResilienceOperationKind.IdempotentWrite || operation.ContractVersion != command.ContractVersion ||
            string.IsNullOrWhiteSpace(command.SagaId) || command.SagaId.Length > 64 || string.IsNullOrWhiteSpace(command.IdempotencyKey) ||
            command.IdempotencyKey.Length > 256 || string.IsNullOrWhiteSpace(command.AccessScope) || command.AccessScope.Length > 512 ||
            command.Deadline <= clock.GetUtcNow() || command.Deadline - clock.GetUtcNow() > options.MaximumLifetime ||
            command.Steps.Count is < 1 or > 128 || command.Steps.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != command.Steps.Count ||
            command.Steps.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 32))
            throw new ResilienceConfigurationException("Saga 操作、步骤或期限配置无效。");
        foreach (var step in command.Steps) handlers.Get(step);
        ValidateDag(command);
        if (!await options.Authorize!(command, token).ConfigureAwait(false)) throw new UnauthorizedAccessException();
        var now = clock.GetUtcNow();
        var scope = OperationFingerprint.Scope(operation.Name, operation.ContractVersion, OperationFingerprint.Scope(operation.IdempotencyScope!, operation.ContractVersion, command.AccessScope));
        var fingerprint = OperationFingerprint.Hash(JsonSerializer.Serialize(new { command.Steps, command.UseCompensationDag }));
        var record = new OperationRecord(command.SagaId, "saga", scope, OperationFingerprint.Hash(command.IdempotencyKey), fingerprint, "Pending", 0,
            protector.Protect(JsonSerializer.Serialize(new SagaDocument(1, command))), 0, now, now, now, command.Deadline);
        var existing = await tx.FindByKeyAsync("saga", scope, record.KeyHash, token).ConfigureAwait(false);
        if (existing is not null) return Existing(existing, record);
        if (!await tx.TryInsertAsync(record, token).ConfigureAwait(false))
        {
            existing = await tx.FindByKeyAsync("saga", scope, record.KeyHash, token).ConfigureAwait(false) ?? throw new OperationIdempotencyConflictException(record.Id);
            return Existing(existing, record);
        }
        foreach (var step in command.Steps)
        {
            var stepId = StepId(command.SagaId, step.Id);
            var doc = new SagaStepDocument(1, step, OperationFingerprint.Scope(command.SagaId, step.Id, "forward"),
                OperationFingerprint.Scope(command.SagaId, step.Id, "compensate"));
            var child = new OperationRecord(stepId, "saga-step", OperationFingerprint.Hash(command.SagaId), OperationFingerprint.Hash(step.Id),
                OperationFingerprint.Hash(JsonSerializer.Serialize(step)), "Pending", 0, protector.Protect(JsonSerializer.Serialize(doc)), 0, now, now, now, command.Deadline);
            if (!await tx.TryInsertAsync(child, token).ConfigureAwait(false)) throw new OperationIdempotencyConflictException(stepId);
        }
        await tx.AppendAuditAsync(new(record.Id, 0, "saga.started", "caller", "持久接收 Saga", now), token).ConfigureAwait(false);
        return record;
    }
    private static OperationRecord Existing(OperationRecord existing, OperationRecord candidate) =>
        existing.Id == candidate.Id && existing.Fingerprint == candidate.Fingerprint ? existing : throw new OperationIdempotencyConflictException(existing.Id);
    internal static string StepId(string saga, string step) => OperationFingerprint.Scope(saga, step, "step");
    internal static SagaDocument Read(OperationRecord r, IOperationDataProtector p)
    {
        try
        {
            return JsonSerializer.Deserialize<SagaDocument>(p.Unprotect(OperationRetainedData.Unwrap(r.Data))) is { Format: 1 } doc
                ? doc : throw new ResilienceConfigurationException("Saga 文档版本无效。");
        }
        catch (ResilienceConfigurationException) { throw; }
        catch (JsonException error) { throw new ResilienceConfigurationException("Saga 文档损坏。", error); }
    }
    internal static SagaStepDocument ReadStep(OperationRecord r, IOperationDataProtector p)
    {
        try
        {
            return JsonSerializer.Deserialize<SagaStepDocument>(p.Unprotect(OperationRetainedData.Unwrap(r.Data))) is { Format: 1 } doc
                ? doc : throw new ResilienceConfigurationException("Saga 步骤版本无效。");
        }
        catch (ResilienceConfigurationException) { throw; }
        catch (JsonException error) { throw new ResilienceConfigurationException("Saga 步骤损坏。", error); }
    }
    private static void ValidateDag(SagaCommand command)
    {
        var all = command.Steps.ToDictionary(s => s.Id, StringComparer.Ordinal);
        if (!command.UseCompensationDag && command.Steps.Any(x => x.CompensateAfter?.Count > 0))
            throw new ResilienceConfigurationException("补偿依赖需要显式启用 DAG。");
        var visited = new HashSet<string>(StringComparer.Ordinal); var visiting = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!all.TryGetValue(id, out var step) || !visiting.Add(id)) throw new ResilienceConfigurationException("补偿依赖缺失或成环。");
            foreach (var dependency in step.CompensateAfter ?? Array.Empty<string>()) Visit(dependency);
            visiting.Remove(id); visited.Add(id);
        }
        foreach (var step in command.Steps) Visit(step.Id);
    }
    public async ValueTask<SagaSnapshot?> GetAsync(string sagaId, string accessScope, CancellationToken token = default)
    {
        await using var tx = await store.BeginAsync(token).ConfigureAwait(false);
        var parent = await tx.FindAsync(sagaId, token).ConfigureAwait(false);
        if (parent is null || parent.Kind != "saga") return null;
        var doc = Read(parent, protector);
        if (doc.Command.AccessScope != accessScope) throw new UnauthorizedAccessException();
        var steps = new List<OperationRecord>();
        foreach (var s in doc.Command.Steps) steps.Add(await tx.FindAsync(StepId(sagaId, s.Id), token).ConfigureAwait(false) ?? throw new ResilienceConfigurationException("缺少 Saga 步骤。"));
        return new(parent, steps.AsReadOnly());
    }
    public async ValueTask ApplyManualAsync(string sagaId, string stepId, string accessScope, long expectedSagaVersion,
        SagaManualAction action, string actor, string reason, CancellationToken token = default)
    {
        await using var tx = await store.BeginAsync(token).ConfigureAwait(false);
        var parent = await tx.FindAsync(sagaId, token).ConfigureAwait(false) ?? throw new KeyNotFoundException(sagaId);
        var doc = Read(parent, protector);
        if (doc.Command.AccessScope != accessScope) throw new UnauthorizedAccessException();
        if (OperationRetainedData.IsRedacted(parent.Data)) throw new ResilienceConfigurationException("Saga 载荷已按保留策略清理，不能重新执行或补偿。");
        if (parent.Version != expectedSagaVersion || parent.LeaseUntil > clock.GetUtcNow()) throw new OperationConcurrencyException(sagaId);
        var child = await tx.FindAsync(StepId(sagaId, stepId), token).ConfigureAwait(false) ?? throw new KeyNotFoundException(stepId);
        var step = ReadStep(child, protector);
        var state = action switch
        {
            SagaManualAction.Retry when child.State is "Failed" or "RetryScheduled" => "RetryScheduled",
            SagaManualAction.Retry when child.State is "CompensationFailed" or "CompensationRetryScheduled" => "CompensationRetryScheduled",
            SagaManualAction.Skip when step.Definition.Optional && child.State is "Failed" or "RetryScheduled" or "Pending" => "Skipped",
            SagaManualAction.ConfirmSucceeded when child.State is "Unknown" or "Confirming" or "Executing" or "ManualIntervention" => "Succeeded",
            SagaManualAction.ConfirmSucceeded when child.State is "CompensationUnknown" or "Compensating" or "ConfirmingCompensation" => "Compensated",
            SagaManualAction.ConfirmNoEffect when child.State is "Unknown" or "Confirming" or "Executing" or "ManualIntervention" => "RetryScheduled",
            SagaManualAction.ConfirmNoEffect when child.State is "CompensationUnknown" or "Compensating" or "ConfirmingCompensation" => "CompensationRetryScheduled",
            SagaManualAction.Compensate when child.State == "Succeeded" && step.Definition.CanCompensate => "CompensationPending",
            _ => throw new ResilienceConfigurationException("该人工动作不适用于当前步骤；未知副作用必须先确认。")
        };
        var compensating = doc.Compensating || action == SagaManualAction.Compensate || state.StartsWith("Compensation", StringComparison.Ordinal);
        var updated = parent with { Version = parent.Version + 1, State = compensating ? "Compensating" : "Running", NextAttemptAt = clock.GetUtcNow(),
            LeaseToken = null, LeaseUntil = null, Data = protector.Protect(JsonSerializer.Serialize(doc with { Compensating = compensating })) };
        // 人工重试只增加一次已授权尝试，不清空累计次数或历史账本。
        if (state is "RetryScheduled" or "CompensationRetryScheduled" or "CompensationPending")
            step = step with { ManualAttemptLimit = Math.Max(options.MaxAttemptsPerStep, Math.Max(step.ManualAttemptLimit ?? 0, checked(child.Attempts + 1))) };
        var next = child with { Version = child.Version + 1, State = state, NextAttemptAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(),
            Data = protector.Protect(JsonSerializer.Serialize(step)) };
        if (!await tx.CompareExchangeAsync(updated, expectedSagaVersion, token: token).ConfigureAwait(false) ||
            !await tx.CompareExchangeAsync(next, child.Version, token: token).ConfigureAwait(false)) throw new OperationConcurrencyException(sagaId);
        await tx.AppendAuditAsync(new(sagaId, updated.Version, "saga.manual." + action.ToString().ToLowerInvariant(), actor, reason, clock.GetUtcNow()), token).ConfigureAwait(false);
        await tx.CommitAsync(token).ConfigureAwait(false);
        ResilienceDiagnostics.RecordLifecycle("saga.state", updated.State);
    }
}
