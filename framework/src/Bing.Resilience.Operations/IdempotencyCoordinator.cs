using System.Text.Json;

namespace Bing.Resilience.Operations;

public enum IdempotencyResultKind { Completed, Processing, Unknown, Expired }
public sealed record IdempotencyResult<T>(string OperationId, IdempotencyResultKind Kind, T? Result, bool IsCommitted);

/// <summary>本地数据库业务与幂等结果在同一事务提交。外部副作用应使用下游幂等协议与持久确认。</summary>
public sealed class IdempotencyCoordinator(IResilienceOperationStore store, TimeProvider clock)
{
    public async ValueTask<IdempotencyResult<T>> ExecuteAsync<T>(ResilienceOperationDescriptor operation, string accessScope, string idempotencyKey,
        string fingerprint, DateTimeOffset deadline, Func<IOperationTransaction, CancellationToken, ValueTask<T>> action, CancellationToken token = default)
    {
        if (!store.IsDurable) throw new ResilienceConfigurationException("接收端幂等需要持久存储。");
        await using var transaction = await store.BeginAsync(token).ConfigureAwait(false);
        var result = await ExecuteInTransactionAsync(transaction, operation, accessScope, idempotencyKey, fingerprint, deadline, action, token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return result with { IsCommitted = true };
    }
    /// <summary>加入现有业务事务；返回值不会宣称已经持久提交，提交仍由事务所有者完成。</summary>
    public async ValueTask<IdempotencyResult<T>> ExecuteInTransactionAsync<T>(IOperationTransaction transaction,
        ResilienceOperationDescriptor operation, string accessScope, string idempotencyKey, string fingerprint, DateTimeOffset deadline,
        Func<IOperationTransaction, CancellationToken, ValueTask<T>> action, CancellationToken token = default)
    {
        if (operation.Kind != ResilienceOperationKind.IdempotentWrite || string.IsNullOrWhiteSpace(accessScope) ||
            string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 256 || deadline <= clock.GetUtcNow())
            throw new ResilienceConfigurationException("写入幂等契约、访问范围、键或截止时间无效。");
        token.ThrowIfCancellationRequested();
        var scope = OperationFingerprint.Scope(operation.Name, operation.ContractVersion, OperationFingerprint.Scope(operation.IdempotencyScope!, operation.ContractVersion, accessScope));
        var key = OperationFingerprint.Hash(idempotencyKey);
        var existing = await transaction.FindByKeyAsync("idempotency", scope, key, token).ConfigureAwait(false);
        if (existing is not null) return Existing<T>(existing, fingerprint);
        var now = clock.GetUtcNow();
        var record = new OperationRecord(Guid.NewGuid().ToString("N"), "idempotency", scope, key, fingerprint, "Executing", 0, "null", 0, now, now, now, deadline);
        if (!await transaction.TryInsertAsync(record, token).ConfigureAwait(false))
        {
            existing = await transaction.FindByKeyAsync("idempotency", scope, key, token).ConfigureAwait(false)
                ?? throw new OperationConcurrencyException(record.Id);
            return Existing<T>(existing, fingerprint);
        }
        var value = await action(transaction, token).ConfigureAwait(false);
        var completed = record with { State = "Succeeded", Data = JsonSerializer.Serialize(value), Version = 1, UpdatedAt = clock.GetUtcNow() };
        if (!await transaction.CompareExchangeAsync(completed, 0, token: token).ConfigureAwait(false)) throw new OperationConcurrencyException(record.Id);
        await transaction.AppendAuditAsync(new(record.Id, 1, "idempotency.completed", "receiver", "业务与结果同事务完成", clock.GetUtcNow()), token).ConfigureAwait(false);
        return new(record.Id, IdempotencyResultKind.Completed, value, false);
    }
    private IdempotencyResult<T> Existing<T>(OperationRecord record, string fingerprint)
    {
        if (record.Fingerprint != fingerprint) throw new OperationIdempotencyConflictException(record.Id);
        if (record.Deadline <= clock.GetUtcNow()) return new(record.Id, IdempotencyResultKind.Expired, default, false);
        if (record.State == "Succeeded")
        {
            try { return new(record.Id, IdempotencyResultKind.Completed, JsonSerializer.Deserialize<T>(record.Data), false); }
            catch (JsonException error) { throw new ResilienceConfigurationException("已完成幂等结果损坏，需要人工核对后处理。", error); }
            catch (NotSupportedException error) { throw new ResilienceConfigurationException("已完成幂等结果类型无法读取，需要人工核对后处理。", error); }
        }
        var state = record.LeaseUntil > clock.GetUtcNow() ? IdempotencyResultKind.Processing : IdempotencyResultKind.Unknown;
        return new(record.Id, state, default, false);
    }
}
