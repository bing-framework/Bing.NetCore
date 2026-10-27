namespace Bing.Resilience;

/// <summary>操作的可重放语义；幂等写必须由服务端协议保证同键只产生一次副作用。</summary>
public enum ResilienceOperationKind { ReadOnly, IdempotentWrite }

/// <summary>宿主审核后登记的不可变业务许可。名称和版本使用 Ordinal 比较。</summary>
public sealed record ResilienceOperationDescriptor
{
    public string Name { get; }
    public string ContractVersion { get; }
    public ResilienceOperationKind Kind { get; }
    public string? IdempotencyScope { get; }
    public ResilienceOperationDescriptor(string name, string contractVersion, ResilienceOperationKind kind, string? idempotencyScope = null)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(contractVersion) || !Enum.IsDefined(typeof(ResilienceOperationKind), kind))
            throw new ResilienceConfigurationException("操作名称、契约版本或语义无效。");
        if (kind == ResilienceOperationKind.IdempotentWrite && string.IsNullOrWhiteSpace(idempotencyScope))
            throw new ResilienceConfigurationException("幂等写必须声明服务端幂等作用域。");
        Name = name; ContractVersion = contractVersion; Kind = kind; IdempotencyScope = idempotencyScope;
    }
}

/// <summary>跨 Provider 代共享的准入状态。租约只释放并发许可，不归还已消费的速率令牌。</summary>
public interface IResilienceAdmissionResources
{
    ValueTask<IDisposable> AcquireConcurrencyAsync(ResiliencePipelineIdentity identity, ResilienceConcurrencyDefinition options, CancellationToken token);
    ValueTask<IDisposable> AcquireRateAsync(ResiliencePipelineIdentity identity, ResilienceRateLimitDefinition options, CancellationToken token);
}

/// <summary>跨代保留熔断状态所需的中性参数。</summary>
public sealed record ResilienceCircuitConfiguration(double FailureRatio, int MinimumThroughput, TimeSpan SamplingDuration, TimeSpan BreakDuration);
public interface IResilienceCircuitLease : IDisposable
{
    /// <summary>排除取消、准入拒绝等本地结果；重复报告被忽略。</summary>
    void Complete(bool failure, bool excluded = false);
}
public interface IResilienceSharedCircuit
{
    ResilienceCircuitState State { get; }
    IResilienceCircuitLease Enter();
    ValueTask IsolateAsync(CancellationToken token);
    ValueTask CloseAsync(CancellationToken token);
}
public interface IResilienceCircuitResources
{
    IResilienceSharedCircuit GetCircuit(ResiliencePipelineIdentity identity, ResilienceCircuitConfiguration configuration, Action<string> reportEvent);
}
/// <summary>声明 Provider 在切换时遵守 Core 的共享准入、熔断和资源所有权协议。</summary>
public interface IResilienceRuntimeAwareProvider : IResilienceProvider { }
