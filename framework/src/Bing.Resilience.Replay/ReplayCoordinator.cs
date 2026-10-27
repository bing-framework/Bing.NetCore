using System.Text;
using System.Text.Json;
using Bing.Resilience.Operations;

namespace Bing.Resilience.Replay;

internal sealed class ReplayCoordinator(IResilienceOperationStore store, IOperationDataProtector protector,
    ResilienceOperationCatalog operations, ReplayCatalog handlers, ReplayOptions options, TimeProvider clock) : IReplayCoordinator
{
    public async ValueTask<DurableOperationReceipt> EnqueueAsync(ReplayCommand command, CancellationToken token = default)
    {
        await using (var transaction = await store.BeginAsync(token).ConfigureAwait(false))
        {
            await EnqueueInTransactionAsync(transaction, command, token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
        }
        ResilienceDiagnostics.RecordLifecycle("replay.state", "accepted");
        return await DurableOperationReceipt.VerifyCommittedAsync(store, command.OperationId, token).ConfigureAwait(false);
    }
    public async ValueTask<OperationRecord> EnqueueInTransactionAsync(IOperationTransaction transaction, ReplayCommand command, CancellationToken token = default)
    {
        if (!store.IsDurable) throw new ResilienceConfigurationException("持久重放拒绝非持久 Store。");
        var operation = operations.Get(command.OperationName);
        handlers.Get(command);
        if (operation.Kind != ResilienceOperationKind.IdempotentWrite || operation.ContractVersion != command.ContractVersion ||
            string.IsNullOrWhiteSpace(command.OperationId) || command.OperationId.Length > 64 ||
            string.IsNullOrWhiteSpace(command.AccessScope) || command.AccessScope.Length > 512 ||
            string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 256 ||
            command.Deadline <= clock.GetUtcNow() || command.Deadline - clock.GetUtcNow() > options.MaximumLifetime ||
            Encoding.UTF8.GetByteCount(command.Payload) > 512 * 1024)
            throw new ResilienceConfigurationException("持久写入的操作契约、标识、载荷或期限无效。");
        if (!await options.Authorize!(command, token).ConfigureAwait(false)) throw new UnauthorizedAccessException("操作不再获得业务授权。");
        var scope = OperationFingerprint.Scope(operation.Name, operation.ContractVersion,
            OperationFingerprint.Scope(operation.IdempotencyScope!, operation.ContractVersion, command.AccessScope));
        var key = OperationFingerprint.Hash(command.IdempotencyKey);
        var fingerprint = OperationFingerprint.Hash(command.Payload);
        var now = clock.GetUtcNow();
        var record = new OperationRecord(command.OperationId, "replay", scope, key, fingerprint, "Pending", 0,
            protector.Protect(JsonSerializer.Serialize(new ReplayDocument(1, command))), 0, now, now, now, command.Deadline);
        var existing = await transaction.FindByKeyAsync("replay", scope, key, token).ConfigureAwait(false);
        if (existing is not null) return ValidateExisting(existing, record);
        if (!await transaction.TryInsertAsync(record, token).ConfigureAwait(false))
        {
            existing = await transaction.FindByKeyAsync("replay", scope, key, token).ConfigureAwait(false)
                ?? throw new OperationIdempotencyConflictException(record.Id);
            return ValidateExisting(existing, record);
        }
        await transaction.AppendAuditAsync(new(record.Id, 0, "replay.enqueued", "caller", "持久接收命令", now), token).ConfigureAwait(false);
        return record;
    }
    private OperationRecord ValidateExisting(OperationRecord existing, OperationRecord candidate)
    {
        var previous = Read(existing, protector).Command;
        var next = Read(candidate, protector).Command;
        if (previous.CommandType != next.CommandType || previous.SchemaVersion != next.SchemaVersion ||
            existing.Fingerprint != candidate.Fingerprint || existing.Id != candidate.Id) throw new OperationIdempotencyConflictException(existing.Id);
        return existing;
    }
    public async ValueTask<OperationRecord?> GetAsync(string operationId, string accessScope, CancellationToken token = default)
    {
        await using var transaction = await store.BeginAsync(token).ConfigureAwait(false);
        var record = await transaction.FindAsync(operationId, token).ConfigureAwait(false);
        if (record is null || record.Kind != "replay") return null;
        CheckScope(record, accessScope);
        return record;
    }
    private void CheckScope(OperationRecord record, string accessScope)
    {
        var document = Read(record, protector);
        if (!string.Equals(document.Command.AccessScope, accessScope, StringComparison.Ordinal)) throw new UnauthorizedAccessException();
    }
    public async ValueTask CancelAsync(string operationId, string accessScope, long expectedVersion, string actor, string reason, CancellationToken token = default)
    {
        await using var transaction = await store.BeginAsync(token).ConfigureAwait(false);
        var record = await transaction.FindAsync(operationId, token).ConfigureAwait(false) ?? throw new KeyNotFoundException(operationId);
        CheckScope(record, accessScope);
        if (record.Kind != "replay" || record.Version != expectedVersion || record.LeaseUntil > clock.GetUtcNow()) throw new OperationConcurrencyException(operationId);
        if (record.State is not ("Pending" or "RetryScheduled")) throw new ResilienceConfigurationException("未知或已完成副作用不能直接取消；应先确认。");
        var updated = record with { State = "Cancelled", Version = record.Version + 1, UpdatedAt = clock.GetUtcNow() };
        if (!await transaction.CompareExchangeAsync(updated, expectedVersion, token: token).ConfigureAwait(false)) throw new OperationConcurrencyException(operationId);
        await transaction.AppendAuditAsync(new(operationId, updated.Version, "replay.cancelled", actor, reason, clock.GetUtcNow()), token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        ResilienceDiagnostics.RecordLifecycle("replay.state", "Cancelled");
    }
    internal static ReplayDocument Read(OperationRecord record, IOperationDataProtector protector)
    {
        try
        {
            var document = JsonSerializer.Deserialize<ReplayDocument>(protector.Unprotect(OperationRetainedData.Unwrap(record.Data)));
            if (document is null || document.Format != 1) throw new ResilienceConfigurationException("未知持久命令格式。");
            return document;
        }
        catch (ResilienceConfigurationException) { throw; }
        catch (JsonException error) { throw new ResilienceConfigurationException("持久命令文档损坏。", error); }
    }
}
