using System.Security.Cryptography;
using System.Text;

namespace Bing.Resilience.Operations;

/// <summary>存储中的不可变操作快照；Data 为版本化业务文档，不保存委托或访问令牌。</summary>
public sealed record OperationRecord(
    string Id, string Kind, string ScopeHash, string KeyHash, string Fingerprint, string State, long Version,
    string Data, int Attempts, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset NextAttemptAt,
    DateTimeOffset Deadline, string? LeaseToken = null, DateTimeOffset? LeaseUntil = null);
public sealed record OperationAudit(string OperationId, long Version, string Event, string Actor, string Reason, DateTimeOffset At);
public sealed record OperationAttempt(string OperationId, int Number, string Kind, string LeaseToken, DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt = null, string? Outcome = null, long? Generation = null, string? Endpoint = null);

/// <summary>一个明确的数据库事务；业务数据和 Journal 必须使用相同物理连接/事务。</summary>
public interface IOperationTransaction : IAsyncDisposable
{
    bool OwnsTransaction { get; }
    ValueTask<OperationRecord?> FindAsync(string id, CancellationToken token = default);
    ValueTask<OperationRecord?> FindByKeyAsync(string kind, string scopeHash, string keyHash, CancellationToken token = default);
    ValueTask<bool> TryInsertAsync(OperationRecord record, CancellationToken token = default);
    ValueTask<bool> CompareExchangeAsync(OperationRecord record, long expectedVersion, string? expectedLeaseToken = null, CancellationToken token = default);
    ValueTask<OperationRecord?> ClaimDueAsync(string kind, IReadOnlyCollection<string> states, TimeSpan leaseDuration, CancellationToken token = default);
    ValueTask<bool> RenewLeaseAsync(string id, long version, string leaseToken, TimeSpan duration, CancellationToken token = default);
    ValueTask AppendAuditAsync(OperationAudit audit, CancellationToken token = default);
    ValueTask AppendAttemptAsync(OperationAttempt attempt, CancellationToken token = default);
    ValueTask CompleteAttemptAsync(string operationId, int attempt, string leaseToken, string outcome, DateTimeOffset at, CancellationToken token = default);
    ValueTask RecordAttemptRouteAsync(string operationId, int attempt, string leaseToken, long? generation, string? endpoint, CancellationToken token = default);
    ValueTask<IReadOnlyList<OperationAttempt>> GetAttemptsAsync(string operationId, CancellationToken token = default);
    ValueTask CommitAsync(CancellationToken token = default);
}
public interface IResilienceOperationStore
{
    bool IsDurable { get; }
    ValueTask<IOperationTransaction> BeginAsync(CancellationToken cancellationToken = default);
}
public sealed class OperationConcurrencyException(string operationId) : Exception("操作状态版本或租约已变化。")
{
    public string OperationId { get; } = operationId;
}
public sealed class OperationIdempotencyConflictException(string operationId) : Exception("同一幂等键对应不同请求内容。")
{
    public string OperationId { get; } = operationId;
}
public sealed class OperationCommitUnknownException(Exception inner) : Exception("事务提交结果未知；应按原 OperationId 查询，不能换键重送。", inner);

/// <summary>统一规范化摘要；原始幂等键与身份范围不进入日志或指标。</summary>
public static class OperationFingerprint
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexString(SHA256.HashData(value));
    public static string Scope(string operation, string version, string accessScope) =>
        Hash(string.Join(":", operation.Length, operation, version.Length, version, accessScope.Length, accessScope));
}
