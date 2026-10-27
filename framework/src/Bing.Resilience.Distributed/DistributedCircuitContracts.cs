namespace Bing.Resilience.Distributed;

public enum DistributedCircuitFailureMode { FailClosed, FailOpenWithLocalCircuit }
public enum DistributedCircuitScope { Service, Endpoint }
public enum DistributedCircuitOutcome { Success, Failure, Excluded }
public enum DistributedCircuitReadMode { AuthoritativeRead, CachedRead }

/// <summary>协议参数为不可变值；配置版本不能以清空存储状态的方式升级。</summary>
public sealed record DistributedCircuitConfiguration(string Version, double FailureRatio = .5, int MinimumThroughput = 20,
    int SamplingMilliseconds = 30000, int BreakMilliseconds = 15000, int ProbeLeaseMilliseconds = 5000)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Version) || !double.IsFinite(FailureRatio) || FailureRatio <= 0 || FailureRatio > 1 ||
            MinimumThroughput < 2 || SamplingMilliseconds < 500 || BreakMilliseconds < 500 || ProbeLeaseMilliseconds < 100 ||
            SamplingMilliseconds > 86400000 || BreakMilliseconds > 86400000 || ProbeLeaseMilliseconds > 86400000)
            throw new ResilienceConfigurationException("分布式熔断协议参数无效。");
    }
}
public sealed record DistributedCircuitAdmission(bool Allowed, string CircuitKey, string AttemptId, long Epoch,
    string ConfigVersion, string? ProbeLeaseToken, DateTimeOffset? LeaseExpiresAt, TimeSpan RetryAfter);
public sealed record DistributedCircuitSnapshot(ResilienceCircuitState State, long Epoch, string ConfigVersion,
    DateTimeOffset ObservedAt, DateTimeOffset? NextProbeAt, DateTimeOffset? ProbeLeaseExpiresAt);

public interface IDistributedCircuitStateStore
{
    ValueTask<DistributedCircuitAdmission> AdmitAsync(string key, DistributedCircuitConfiguration configuration, string attemptId, CancellationToken cancellationToken = default);
    ValueTask ReportAsync(DistributedCircuitAdmission admission, DistributedCircuitConfiguration configuration, DistributedCircuitOutcome outcome, CancellationToken cancellationToken = default);
    ValueTask<DistributedCircuitSnapshot> GetSnapshotAsync(string key, DistributedCircuitConfiguration configuration, CancellationToken cancellationToken = default);
    ValueTask<DistributedCircuitSnapshot> ControlAsync(string key, DistributedCircuitConfiguration configuration, long expectedEpoch, bool isolate, CancellationToken cancellationToken = default);
}
public sealed class DistributedCircuitRejectedException(string key, DistributedCircuitScope scope, TimeSpan retryAfter)
    : Exception("分布式熔断拒绝本次尝试。"), IResilienceScopedRejection
{
    public bool CanMoveToNextEndpoint => Scope == DistributedCircuitScope.Endpoint;
    public string CircuitKey { get; } = key;
    public DistributedCircuitScope Scope { get; } = scope;
    public TimeSpan RetryAfter { get; } = retryAfter;
}
public sealed class ResilienceStateStoreUnavailableException(Exception inner)
    : Exception("分布式状态存储不可用，按 FailClosed 拒绝执行。", inner), IResilienceNonRecoverableFailure;
public sealed class DistributedCircuitVersionException(string message) : Exception(message), IResilienceNonRecoverableFailure;

/// <summary>失联行为必须显式选择；CachedRead 必须声明可接受的传播滞后。</summary>
public sealed class DistributedCircuitOptions
{
    public string CircuitKey { get; set; } = "";
    public DistributedCircuitConfiguration Configuration { get; set; } = new("1");
    public DistributedCircuitFailureMode? FailureMode { get; set; }
    public DistributedCircuitScope Scope { get; set; }
    public DistributedCircuitReadMode ReadMode { get; set; }
    public TimeSpan CacheDuration { get; set; }
    /// <summary>本地失联保护及拒绝缓存的分区上限；达到上限时拒绝创建新状态。</summary>
    public int MaximumLocalCircuits { get; set; } = 128;
    public TimeSpan StoreTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
    public Action<Exception>? OnStoreFailure { get; set; }
}
