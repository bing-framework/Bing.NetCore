using System.Collections.Concurrent;

namespace Bing.Resilience;

/// <summary>Provider 必须明确实现的可移植能力。</summary>
[Flags]
public enum ResilienceCapabilities
{
    None = 0, Retry = 1, Timeout = 2, CircuitBreaker = 4, Concurrency = 8,
    RateLimit = 16, Fallback = 32, Hedging = 64, ManualCircuitControl = 128,
    All = Retry | Timeout | CircuitBreaker | Concurrency | RateLimit | Fallback | Hedging | ManualCircuitControl
}

/// <summary>稳定、有界的管线身份；不允许使用请求 URL 或用户标识。</summary>
public sealed record ResiliencePipelineIdentity(string Purpose, string Name, Type ResultType, string Partition = "");

/// <summary>区分成功（包括 null）与异常的执行结果。</summary>
public readonly struct ResilienceOutcome<T>
{
    public T? Result { get; }
    public Exception? Exception { get; }
    public bool HasResult => Exception is null;
    private ResilienceOutcome(T? result, Exception? exception) { Result = result; Exception = exception; }
    public static ResilienceOutcome<T> FromResult(T result) => new(result, null);
    public static ResilienceOutcome<T> FromException(Exception exception) => new(default, exception ?? throw new ArgumentNullException(nameof(exception)));
}

/// <summary>类型化属性键；引用类型必须明确提供分支复制规则。</summary>
public sealed class ResiliencePropertyKey<T>(string name, Func<T, T>? clone = null)
{
    public string Name { get; } = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("属性名称不能为空。", nameof(name));
    internal object? Copy(object? value)
    {
        if (clone is not null) return clone((T)value!);
        if (value is null || !System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>() || value is string or CancellationToken) return value;
        throw new ResilienceConfigurationException($"属性 '{Name}' 必须提供引用值复制器。");
    }
}

/// <summary>单次执行上下文。分支复制属性；不可将不安全的可变对象直接共享给并行回调。</summary>
public sealed class ResilienceExecutionContext
{
    private sealed record Entry(object? Value, Func<object?, object?> Copy);
    private readonly ConcurrentDictionary<object, Entry> _properties = new();
    public string? OperationName { get; init; }
    public int AttemptNumber { get; private init; }
    public int ExecutionDepth { get; private init; }
    public CancellationToken CancellationToken { get; private init; }

    public void Set<T>(ResiliencePropertyKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _properties[key] = new(value, key.Copy);
    }
    public bool TryGet<T>(ResiliencePropertyKey<T> key, out T value)
    {
        if (_properties.TryGetValue(key, out var entry)) { value = (T)entry.Value!; return true; }
        value = default!; return false;
    }

    /// <summary>为嵌套执行或新尝试建立独立快照，不复用调用方的可变字典。</summary>
    public ResilienceExecutionContext Fork(CancellationToken cancellationToken, int attemptNumber = 0, bool nestedExecution = false)
    {
        var child = new ResilienceExecutionContext
        {
            OperationName = OperationName, AttemptNumber = attemptNumber,
            ExecutionDepth = ExecutionDepth + (nestedExecution ? 1 : 0), CancellationToken = cancellationToken
        };
        foreach (var pair in _properties)
            child._properties[pair.Key] = pair.Value with { Value = pair.Value.Copy(pair.Value.Value) };
        return child;
    }
}

/// <summary>传给中性失败谓词、退避及降级的尝试信息；AttemptNumber 从零开始。</summary>
public readonly record struct ResiliencePredicateArguments<T>(ResilienceOutcome<T> Outcome, ResilienceExecutionContext Context, int AttemptNumber);
public readonly record struct ResilienceFallbackArguments<T>(ResilienceOutcome<T> Outcome, ResilienceExecutionContext Context, CancellationToken CancellationToken);

/// <summary>异步无结果管线。</summary>
public interface IResiliencePipeline
{
    ValueTask ExecuteAsync(Func<CancellationToken, ValueTask> action, CancellationToken cancellationToken = default);
    ValueTask ExecuteAsync(Func<ResilienceExecutionContext, CancellationToken, ValueTask> action, ResilienceExecutionContext context, CancellationToken cancellationToken = default);
}
/// <summary>异步有结果管线；最终结果所有权交给调用方。</summary>
public interface IResiliencePipeline<T>
{
    ValueTask<T> ExecuteAsync(Func<CancellationToken, ValueTask<T>> action, CancellationToken cancellationToken = default);
    ValueTask<T> ExecuteAsync(Func<ResilienceExecutionContext, CancellationToken, ValueTask<T>> action, ResilienceExecutionContext context, CancellationToken cancellationToken = default);
}
/// <summary>按登记身份获取有状态管线；未知身份明确失败。</summary>
public interface IResiliencePipelineProvider
{
    IResiliencePipeline GetPipeline(string name, string purpose = "generic", string partition = "");
    IResiliencePipeline<T> GetPipeline<T>(string name, string purpose = "generic", string partition = "");
    void Validate();
}

/// <summary>引擎扩展契约；只接受冻结的中性定义。</summary>
public interface IResilienceProvider
{
    string Name { get; }
    ResilienceCapabilities Capabilities { get; }
    IResiliencePipeline<T> CreatePipeline<T>(ResiliencePipelineDefinition<T> definition, ResiliencePipelineBuildContext context);
}

/// <summary>无结果操作在 Provider 边界使用的值类型，不要求应用自行构造。</summary>
public readonly struct ResilienceUnit { }

public enum ResilienceTimeoutKind { Execution, Attempt }
public enum ResilienceRejectionReason { Concurrency, RateLimit }
public enum ResilienceCircuitState { Closed, Open, HalfOpen, Isolated }

/// <summary>可移植超时，区分完整执行与单次尝试。</summary>
public sealed class ResilienceTimeoutException(ResilienceTimeoutKind kind, ResiliencePipelineIdentity identity, Exception? inner = null)
    : TimeoutException($"管线 '{identity.Name}' 的 {kind} 预算耗尽。", inner)
{
    public ResilienceTimeoutKind Kind { get; } = kind;
    public ResiliencePipelineIdentity Identity { get; } = identity;
}
public sealed class ResilienceCircuitOpenException(ResiliencePipelineIdentity identity, Exception? inner = null)
    : Exception($"管线 '{identity.Name}' 的熔断器拒绝执行。", inner)
{
    public ResiliencePipelineIdentity Identity { get; } = identity;
}
public sealed class ResilienceRejectedException(ResilienceRejectionReason reason, ResiliencePipelineIdentity identity, Exception? inner = null)
    : Exception($"管线 '{identity.Name}' 的 {reason} 许可不足。", inner)
{
    public ResilienceRejectionReason Reason { get; } = reason;
    public ResiliencePipelineIdentity Identity { get; } = identity;
}
/// <summary>丢弃结果清理失败；不能再次重试、并行补发或降级成成功。</summary>
public sealed class ResilienceResultCleanupException(ResiliencePipelineIdentity identity, Exception inner)
    : Exception($"管线 '{identity.Name}' 的丢弃结果清理失败。", inner)
{
    public ResiliencePipelineIdentity Identity { get; } = identity;
}
public sealed class ResilienceConfigurationException : InvalidOperationException
{
    public ResilienceConfigurationException(string message) : base(message) { }
    public ResilienceConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>管线熔断控制；仅对显式启用控制能力的登记项有效。</summary>
public interface IResilienceCircuitController
{
    ResilienceCircuitState GetState(ResiliencePipelineIdentity identity);
    ValueTask IsolateAsync(ResiliencePipelineIdentity identity, CancellationToken cancellationToken = default);
    ValueTask CloseAsync(ResiliencePipelineIdentity identity, CancellationToken cancellationToken = default);
}

/// <summary>Provider 构建所需的时钟、销毁登记及中性事件桥接，不提供服务定位器。</summary>
public sealed class ResiliencePipelineBuildContext(
    ResiliencePipelineIdentity identity, TimeProvider timeProvider,
    Action<IDisposable> registerResource,
    Action<Func<ResilienceCircuitState>, Func<CancellationToken, ValueTask>, Func<CancellationToken, ValueTask>> registerCircuit,
    Action<string> reportEvent)
{
    public ResiliencePipelineIdentity Identity { get; } = identity;
    public TimeProvider TimeProvider { get; } = timeProvider;
    /// <summary>可选的宿主级共享准入资源；原有构造签名保持不变。</summary>
    public IResilienceAdmissionResources? AdmissionResources { get; init; }
    public IResilienceCircuitResources? CircuitResources { get; init; }
    public void RegisterResource(IDisposable resource) => registerResource(resource);
    public void RegisterCircuit(Func<ResilienceCircuitState> state, Func<CancellationToken, ValueTask> isolate, Func<CancellationToken, ValueTask> close) => registerCircuit(state, isolate, close);
    public void ReportEvent(string eventName) => reportEvent(eventName);
}
