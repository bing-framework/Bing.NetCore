namespace Bing.Resilience.Operations;

/// <summary>显式终态保留策略。保护期从操作 Deadline 开始；null 表示永久保留对应历史。</summary>
/// <remarks>幂等摘要和操作状态始终保留。载荷清除后不能重新执行或补偿该操作。</remarks>
public sealed record OperationRetentionPolicy(TimeSpan PayloadRetention, TimeSpan ProtectionPeriod,
    TimeSpan? AttemptRetention = null, TimeSpan? AuditRetention = null, int BatchSize = 100, int HistoryBatchSize = 1000)
{
    public void Validate()
    {
        if (PayloadRetention <= TimeSpan.Zero || ProtectionPeriod <= TimeSpan.Zero ||
            AttemptRetention <= TimeSpan.Zero || AuditRetention <= TimeSpan.Zero ||
            PayloadRetention > TimeSpan.FromDays(3650) || ProtectionPeriod > TimeSpan.FromDays(3650) ||
            AttemptRetention > TimeSpan.FromDays(3650) || AuditRetention > TimeSpan.FromDays(3650) ||
            BatchSize is < 1 or > 1000 || HistoryBatchSize is < 1 or > 10000)
            throw new ResilienceConfigurationException("清理必须显式设置正保留期、保护期和有界批次。");
    }
}

public sealed record OperationRetentionResult(int Operations, int Payloads, int Attempts, int Audits);

/// <summary>可选 Store 能力。每批在单个事务内检查终态/租约并清理；不删除幂等记录。</summary>
public interface IOperationRetentionStore
{
    ValueTask<OperationRetentionResult> CleanupAsync(string kind, OperationRetentionPolicy policy,
        Func<OperationRecord, string> redactPayload, CancellationToken token = default);
}

/// <summary>持久载荷已清理的标记；剩余加密文档仅用于权限校验、身份和状态查询。</summary>
public static class OperationRetainedData
{
    public const string Prefix = "retained:v1:";
    public static bool IsRedacted(string data) => data.StartsWith(Prefix, StringComparison.Ordinal);
    public static string Unwrap(string data) => IsRedacted(data) ? data[Prefix.Length..] : data;
}
