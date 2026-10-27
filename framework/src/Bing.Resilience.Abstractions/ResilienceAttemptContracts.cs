namespace Bing.Resilience;

/// <summary>本地控制故障不得被重试、Hedging 或降级掩盖。</summary>
public interface IResilienceNonRecoverableFailure { }
public sealed record ResilienceAttemptContext(ResiliencePipelineIdentity Identity, ResilienceExecutionContext Execution);

/// <summary>每次尝试的准入和结果观察扩展；不得修改业务结果或取得其所有权。</summary>
public interface IResilienceAttemptObserver<T>
{
    ValueTask<IResilienceAttemptObservation<T>> BeginAsync(ResilienceAttemptContext context, CancellationToken cancellationToken);
}
public interface IResilienceAttemptObservation<T> : IAsyncDisposable
{
    ValueTask CompleteAsync(ResilienceOutcome<T> outcome, CancellationToken cancellationToken);
}

/// <summary>区分可更换端点的拒绝与必须结束调用的全局拒绝。</summary>
public interface IResilienceScopedRejection { bool CanMoveToNextEndpoint { get; } }
