namespace Bing.Resilience;

/// <summary>宿主可变配置。登记时 Freeze 为独立不可变定义，后续修改不生效。</summary>
public class ResiliencePipelineOptions<T>
{
    public string? ProviderName { get; set; }
    public string? SwitchGroup { get; set; }
    public string Purpose { get; set; } = "generic";
    public string Partition { get; set; } = "";
    public TimeSpan? ExecutionTimeout { get; set; }
    public TimeSpan? AttemptTimeout { get; set; }
    public ResilienceRetryOptions<T> Retry { get; } = new();
    public ResilienceHedgingOptions<T> Hedging { get; } = new();
    public ResilienceCircuitBreakerOptions<T> CircuitBreaker { get; } = new();
    public ResilienceConcurrencyOptions Concurrency { get; } = new();
    public ResilienceRateLimitOptions RateLimit { get; } = new();
    public ResilienceFallbackOptions<T> Fallback { get; } = new();
    public Func<T, ValueTask>? DiscardResultAsync { get; set; }
    public IList<IResilienceAttemptObserver<T>> AttemptObservers { get; } = new List<IResilienceAttemptObserver<T>>();

    public ResiliencePipelineDefinition<T> Freeze(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(Purpose) || Partition is null)
            throw new ResilienceConfigurationException("管线名称、用途不能为空，分区不能为 null。");
        ValidateTimeout(ExecutionTimeout); ValidateTimeout(AttemptTimeout);
        if (ExecutionTimeout.HasValue && AttemptTimeout > ExecutionTimeout)
            throw new ResilienceConfigurationException("单次预算不能超过总预算。");
        if (Retry.MaxRetryAttempts is < 0 or > 100 || Retry.Delay < TimeSpan.Zero || Retry.MaxDelay < Retry.Delay ||
            Retry.MaxDelay > TimeSpan.FromDays(1))
            throw new ResilienceConfigurationException("重试次数或退避配置无效。");
        if (Retry.MaxRetryAttempts > 0 && Retry.ShouldHandle is null)
            throw new ResilienceConfigurationException("重试需要显式失败谓词。");
        if (Hedging.Enabled && (Retry.MaxRetryAttempts > 0 || !Hedging.AllowParallelExecution ||
            Hedging.MaxHedgedAttempts is < 1 or > 10 || Hedging.Delay < TimeSpan.Zero ||
            Hedging.Delay > TimeSpan.FromDays(1) || Hedging.ShouldHandle is null))
            throw new ResilienceConfigurationException("Hedging 必须独立于重试，并声明并行安全、有效次数和失败谓词。");
        if (Concurrency.Enabled && (Concurrency.PermitLimit < 1 || Concurrency.QueueLimit < 0))
            throw new ResilienceConfigurationException("并发许可配置无效。");
        if (RateLimit.Enabled && (RateLimit.TokenLimit < 1 || RateLimit.TokensPerPeriod < 1 || RateLimit.QueueLimit < 0 ||
            RateLimit.ReplenishmentPeriod <= TimeSpan.Zero || RateLimit.ReplenishmentPeriod > TimeSpan.FromDays(1)))
            throw new ResilienceConfigurationException("TokenBucket 配置无效。");
        if (CircuitBreaker.Enabled && (CircuitBreaker.ShouldHandle is null || !double.IsFinite(CircuitBreaker.FailureRatio) ||
            CircuitBreaker.FailureRatio <= 0 || CircuitBreaker.FailureRatio > 1 || CircuitBreaker.MinimumThroughput < 2 ||
            CircuitBreaker.SamplingDuration < TimeSpan.FromMilliseconds(500) || CircuitBreaker.SamplingDuration > TimeSpan.FromDays(1) ||
            CircuitBreaker.BreakDuration < TimeSpan.FromMilliseconds(500) || CircuitBreaker.BreakDuration > TimeSpan.FromDays(1)))
            throw new ResilienceConfigurationException("熔断配置无效。");
        if (CircuitBreaker.EnableManualControl && !CircuitBreaker.Enabled)
            throw new ResilienceConfigurationException("手动控制需要启用熔断。");
        if (Fallback.Enabled && (Fallback.ShouldHandle is null || Fallback.Action is null))
            throw new ResilienceConfigurationException("降级需要显式谓词和回调。");
        return new(new(Purpose, name, typeof(T), Partition), ProviderName, ExecutionTimeout, AttemptTimeout,
            new(Retry.MaxRetryAttempts, Retry.Delay, Retry.MaxDelay, Retry.UseJitter, Retry.ShouldHandle, Retry.DelayGenerator),
            new(Hedging.Enabled, Hedging.AllowParallelExecution, Hedging.MaxHedgedAttempts, Hedging.Delay, Hedging.ShouldHandle),
            new(CircuitBreaker.Enabled, CircuitBreaker.EnableManualControl, CircuitBreaker.FailureRatio, CircuitBreaker.MinimumThroughput,
                CircuitBreaker.SamplingDuration, CircuitBreaker.BreakDuration, CircuitBreaker.ShouldHandle),
            new(Concurrency.Enabled, Concurrency.PermitLimit, Concurrency.QueueLimit),
            new(RateLimit.Enabled, RateLimit.TokenLimit, RateLimit.TokensPerPeriod, RateLimit.ReplenishmentPeriod, RateLimit.QueueLimit),
            new(Fallback.Enabled, Fallback.ShouldHandle, Fallback.Action), DiscardResultAsync) { SwitchGroup = SwitchGroup, AttemptObservers = Array.AsReadOnly(AttemptObservers.ToArray()) };
    }
    private static void ValidateTimeout(TimeSpan? timeout)
    {
        if (timeout.HasValue && (timeout < TimeSpan.FromMilliseconds(10) || timeout > TimeSpan.FromDays(1)))
            throw new ResilienceConfigurationException("预算必须在 10 毫秒到 1 天之间。");
    }
}
public sealed class ResiliencePipelineOptions : ResiliencePipelineOptions<ResilienceUnit> { }
public sealed class ResilienceRetryOptions<T>
{
    public int MaxRetryAttempts { get; set; }
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);
    public bool UseJitter { get; set; } = true;
    public Func<ResiliencePredicateArguments<T>, bool>? ShouldHandle { get; set; }
    /// <summary>返回服务端等明确延迟；null 使用本地退避，非 null 不受本地 MaxDelay 截短。</summary>
    public Func<ResiliencePredicateArguments<T>, TimeSpan?>? DelayGenerator { get; set; }
}
public sealed class ResilienceHedgingOptions<T>
{
    public bool Enabled { get; set; }
    public bool AllowParallelExecution { get; set; }
    public int MaxHedgedAttempts { get; set; } = 1;
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(200);
    public Func<ResiliencePredicateArguments<T>, bool>? ShouldHandle { get; set; }
}
public sealed class ResilienceCircuitBreakerOptions<T>
{
    public bool Enabled { get; set; }
    public bool EnableManualControl { get; set; }
    public double FailureRatio { get; set; } = .5;
    public int MinimumThroughput { get; set; } = 20;
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(15);
    public Func<ResiliencePredicateArguments<T>, bool>? ShouldHandle { get; set; }
}
public sealed class ResilienceConcurrencyOptions
{
    public bool Enabled { get; set; }
    public int PermitLimit { get; set; } = 32;
    public int QueueLimit { get; set; }
}
public sealed class ResilienceRateLimitOptions
{
    public bool Enabled { get; set; }
    public int TokenLimit { get; set; }
    public int TokensPerPeriod { get; set; }
    public TimeSpan ReplenishmentPeriod { get; set; }
    public int QueueLimit { get; set; }
}
public sealed class ResilienceFallbackOptions<T>
{
    public bool Enabled { get; set; }
    public Func<ResiliencePredicateArguments<T>, bool>? ShouldHandle { get; set; }
    public Func<ResilienceFallbackArguments<T>, ValueTask<T>>? Action { get; set; }
}

public sealed record ResilienceRetryDefinition<T>(int MaxRetryAttempts, TimeSpan Delay, TimeSpan MaxDelay, bool UseJitter,
    Func<ResiliencePredicateArguments<T>, bool>? ShouldHandle, Func<ResiliencePredicateArguments<T>, TimeSpan?>? DelayGenerator);
public sealed record ResilienceHedgingDefinition<T>(bool Enabled, bool AllowParallelExecution, int MaxHedgedAttempts, TimeSpan Delay, Func<ResiliencePredicateArguments<T>, bool>? ShouldHandle);
public sealed record ResilienceCircuitBreakerDefinition<T>(bool Enabled, bool EnableManualControl, double FailureRatio, int MinimumThroughput,
    TimeSpan SamplingDuration, TimeSpan BreakDuration, Func<ResiliencePredicateArguments<T>, bool>? ShouldHandle);
public sealed record ResilienceConcurrencyDefinition(bool Enabled, int PermitLimit, int QueueLimit);
public sealed record ResilienceRateLimitDefinition(bool Enabled, int TokenLimit, int TokensPerPeriod, TimeSpan ReplenishmentPeriod, int QueueLimit);
public sealed record ResilienceFallbackDefinition<T>(bool Enabled, Func<ResiliencePredicateArguments<T>, bool>? ShouldHandle, Func<ResilienceFallbackArguments<T>, ValueTask<T>>? Action);

/// <summary>与引擎无关的冻结配置；策略顺序由本库固定，不接受任意策略树。</summary>
public sealed record ResiliencePipelineDefinition<T>(
    ResiliencePipelineIdentity Identity, string? ProviderName, TimeSpan? ExecutionTimeout, TimeSpan? AttemptTimeout,
    ResilienceRetryDefinition<T> Retry, ResilienceHedgingDefinition<T> Hedging, ResilienceCircuitBreakerDefinition<T> CircuitBreaker,
    ResilienceConcurrencyDefinition Concurrency, ResilienceRateLimitDefinition RateLimit, ResilienceFallbackDefinition<T> Fallback,
    Func<T, ValueTask>? DiscardResultAsync)
{
    public string? SwitchGroup { get; init; }
    public IReadOnlyList<IResilienceAttemptObserver<T>> AttemptObservers { get; init; } = Array.Empty<IResilienceAttemptObserver<T>>();
    public ResilienceCapabilities RequiredCapabilities =>
        (ExecutionTimeout.HasValue || AttemptTimeout.HasValue ? ResilienceCapabilities.Timeout : 0) |
        (Retry.MaxRetryAttempts > 0 ? ResilienceCapabilities.Retry : 0) |
        (Hedging.Enabled ? ResilienceCapabilities.Hedging : 0) |
        (CircuitBreaker.Enabled ? ResilienceCapabilities.CircuitBreaker : 0) |
        (CircuitBreaker.EnableManualControl ? ResilienceCapabilities.ManualCircuitControl : 0) |
        (Concurrency.Enabled ? ResilienceCapabilities.Concurrency : 0) |
        (RateLimit.Enabled ? ResilienceCapabilities.RateLimit : 0) |
        (Fallback.Enabled ? ResilienceCapabilities.Fallback : 0);
}
