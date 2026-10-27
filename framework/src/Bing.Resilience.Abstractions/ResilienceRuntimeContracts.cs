namespace Bing.Resilience;

/// <summary>可选的运行时切换配置。候选代必须完整构建，旧代按租约排空。</summary>
public sealed class ResilienceRuntimeOptions
{
    public int MaxRetainedGenerations { get; set; } = 4;
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
public sealed record ResilienceRuntimeSnapshot(string Group, long Version, string? ProviderName, int ActiveLeases, int RetiredGenerations);
public sealed record ResilienceSwitchResult(ResilienceRuntimeSnapshot Snapshot, bool PreviousGenerationDrained);
public interface IResilienceRuntimeManager
{
    ResilienceRuntimeSnapshot GetSnapshot(string group);
    ValueTask<ResilienceSwitchResult> SwitchAsync(string group, string providerName, long expectedVersion, CancellationToken cancellationToken = default);
}
/// <summary>将同组嵌套执行固定在同一代。HTTP 流通过延长此租约保持旧代存活。</summary>
public interface IResilienceExecutionLease : IDisposable
{
    long Version { get; }
    ResilienceExecutionContext Context { get; }
}
public interface IResilienceRuntimeLeaseProvider
{
    bool IsRuntimeSwitchingEnabled { get; }
    IResilienceExecutionLease AcquireLease(string group, ResilienceExecutionContext? context = null);
}
