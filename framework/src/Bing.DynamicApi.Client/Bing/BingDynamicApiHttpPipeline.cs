using Microsoft.Extensions.Options;

namespace Bing.DynamicApi.Client;

/// <summary>
/// 标识动态 API 请求的重试责任方。
/// </summary>
public enum BingDynamicApiRetryMode
{
    /// <summary>
    /// 由原有 MaxRetries 配置和宿主重试策略负责重试。
    /// </summary>
    Legacy = 0,
    /// <summary>
    /// 由绑定的 HTTP 恢复管线负责重试，客户端只发送一次。
    /// </summary>
    HttpPipeline = 1
}

/// <summary>
/// 标识动态 API 客户端发出的 HTTP 请求用途。
/// </summary>
public enum BingDynamicApiRequestKind
{
    /// <summary>
    /// 下载共享的 API 描述。
    /// </summary>
    Definition,
    /// <summary>
    /// 调用业务接口。
    /// </summary>
    Invocation
}

/// <summary>
/// 动态 API 请求的重放资格元数据。
/// </summary>
/// <remarks>保存在请求 Options 中的中性重放资格，不发送为网络请求头。</remarks>
public sealed class BingDynamicApiRequestMetadata
{
    /// <summary>
    /// 定义 HTTP 请求选项中保存动态 API 请求元数据的键。
    /// </summary>
    private static readonly HttpRequestOptionsKey<BingDynamicApiRequestMetadata> Key = new("Bing.DynamicApi.RequestMetadata");

    /// <summary>
    /// 初始化一个 <see cref="BingDynamicApiRequestMetadata" /> 类型的实例。
    /// </summary>
    /// <param name="remoteName">当前远端服务的名称。</param>
    /// <param name="kind">当前对象或操作的种类。</param>
    /// <param name="replayable">指示当前 HTTP 请求正文是否可重放。</param>
    /// <param name="streamResult">指示 HTTP 响应正文是否以流方式返回。</param>
    internal BingDynamicApiRequestMetadata(string remoteName, BingDynamicApiRequestKind kind, bool replayable, bool streamResult)
    {
        RemoteName = remoteName;
        Kind = kind;
        IsReplayable = replayable;
        IsStreamResult = streamResult;
    }

    /// <summary>
    /// 获取远程服务名称。
    /// </summary>
    public string RemoteName { get; }
    /// <summary>
    /// 获取请求种类。
    /// </summary>
    public BingDynamicApiRequestKind Kind { get; }
    /// <summary>
    /// 获取客户端是否许可重放。
    /// </summary>
    /// <remarks>宿主仍需额外批准业务调用。</remarks>
    public bool IsReplayable { get; }
    /// <summary>
    /// 获取是否返回由调用方管理的流。
    /// </summary>
    public bool IsStreamResult { get; }

    /// <summary>
    /// 尝试从 HTTP 请求读取动态 API 请求元数据。
    /// </summary>
    /// <param name="request">包含请求选项的 HTTP 请求。</param>
    /// <param name="metadata">接收读取到的请求元数据。</param>
    /// <returns>元数据存在时为 true 并通过 metadata 返回；否则为 false。</returns>
    public static bool TryGet(HttpRequestMessage request, out BingDynamicApiRequestMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Options.TryGetValue(Key, out metadata);
    }

    /// <summary>
    /// 将动态 API 元数据写入 HTTP 请求选项。
    /// </summary>
    /// <param name="request">要附加元数据的 HTTP 请求。</param>
    /// <remarks>
    /// 同时写入流结果和定义请求标记，供 HTTP 韧性策略判断重试或对冲资格。
    /// </remarks>
    internal void Set(HttpRequestMessage request)
    {
        request.Options.Set(Key, this);
        request.Options.Set(new HttpRequestOptionsKey<bool>("Bing.Resilience.IsStreamResult"), IsStreamResult);
        request.Options.Set(new HttpRequestOptionsKey<bool>("Bing.Resilience.IsDefinition"), Kind == BingDynamicApiRequestKind.Definition);
    }
}

/// <summary>
/// 远程服务与具名 HTTP 客户端的绑定。
/// </summary>
/// <param name="RemoteName">不区分大小写的远程服务名称。</param>
/// <param name="HttpClientName">注册恢复管线时使用的实际名称，必须精确保留大小写。</param>
public sealed record BingDynamicApiHttpPipelineBinding(string RemoteName, string HttpClientName);

/// <summary>
/// 验证动态 API 中性管线配置与绑定的组件。
/// </summary>
/// <remarks>
/// 校验中性配置及管线绑定，不创建临时服务提供程序。
/// </remarks>
/// <param name="bindings">命名动态 API 客户端的 HTTP 管线绑定集合。</param>
internal sealed class BingDynamicApiPipelineOptionsValidator(IEnumerable<BingDynamicApiHttpPipelineBinding> bindings)
    : IValidateOptions<BingDynamicApiClientOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string name, BingDynamicApiClientOptions options)
    {
        var failures = new List<string>();
        foreach (var (remoteName, remote) in options.RemoteServices)
        {
            if (!Enum.IsDefined(typeof(BingDynamicApiRetryMode), remote.RetryMode))
                failures.Add($"远程服务 '{remoteName}' 的 RetryMode 无效。");
            foreach (var timeout in new[] { remote.OperationTimeout, remote.DefinitionTimeout })
                if (timeout.HasValue && (timeout.Value <= TimeSpan.Zero || timeout.Value > TimeSpan.FromDays(1)))
                    failures.Add($"远程服务 '{remoteName}' 的调用预算必须大于零且不超过 1 天。");
            if (remote.RetryMode != BingDynamicApiRetryMode.HttpPipeline)
                continue;
            if (remote.MaxRetries != 0)
                failures.Add($"远程服务 '{remoteName}' 使用 HttpPipeline 时 MaxRetries 必须为 0。");
            var matches = bindings.Where(b => string.Equals(b.RemoteName, remoteName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].HttpClientName))
                failures.Add($"远程服务 '{remoteName}' 使用 HttpPipeline 时必须且只能存在一个有效的适配注册。");
        }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>
/// 区分操作预算取消与其他取消的预算对象。
/// </summary>
/// <remarks>
/// 一个操作的独立预算；仅将此预算引发的取消识别为超时。
/// </remarks>
internal sealed class BingDynamicApiOperationBudget : IDisposable
{
    /// <summary>
    /// 保存操作预算到期时触发取消的取消源。
    /// </summary>
    private readonly CancellationTokenSource _deadline;
    /// <summary>
    /// 保存调用方提供的取消令牌。
    /// </summary>
    private readonly CancellationToken _caller;

    /// <summary>
    /// 初始化一个 <see cref="BingDynamicApiOperationBudget" /> 类型的实例。
    /// </summary>
    /// <param name="timeout">当前操作允许的超时时长。</param>
    /// <param name="caller">用于取消操作的调用方令牌。</param>
    public BingDynamicApiOperationBudget(TimeSpan? timeout, CancellationToken caller)
    {
        _caller = caller;
        if (timeout.HasValue)
        {
            _deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
            _deadline.CancelAfter(timeout.Value);
        }
        Token = _deadline?.Token ?? caller;
    }

    /// <summary>
    /// 获取当前操作的取消令牌。
    /// </summary>
    public CancellationToken Token { get; }
    /// <summary>
    /// 判断取消异常是否由动态 API 操作期限触发。
    /// </summary>
    /// <param name="exception">待判断的操作取消异常。</param>
    /// <returns>操作期限已取消、调用方未取消且异常令牌匹配期限令牌时为 true；否则为 false。</returns>
    public bool IsTimeout(OperationCanceledException exception) =>
        _deadline != null && _deadline.IsCancellationRequested && !_caller.IsCancellationRequested &&
        exception.CancellationToken == Token;

    /// <inheritdoc />
    public void Dispose() => _deadline?.Dispose();
}
