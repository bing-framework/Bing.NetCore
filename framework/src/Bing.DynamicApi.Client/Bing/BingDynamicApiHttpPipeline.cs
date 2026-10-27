using Microsoft.Extensions.Options;

namespace Bing.DynamicApi.Client;

/// <summary>指定重试的唯一执行者。</summary>
public enum BingDynamicApiRetryMode
{
    /// <summary>沿用原有 MaxRetries 和宿主重试策略。</summary>
    Legacy = 0,
    /// <summary>仅发送一次，由已绑定的 HTTP 恢复管线处理重试。</summary>
    HttpPipeline = 1
}

/// <summary>动态客户端产生的 HTTP 请求种类。</summary>
public enum BingDynamicApiRequestKind
{
    /// <summary>共享 API 描述下载。</summary>
    Definition,
    /// <summary>业务接口调用。</summary>
    Invocation
}

/// <summary>保存在请求 Options 中的中性重放资格，不发送为网络请求头。</summary>
public sealed class BingDynamicApiRequestMetadata
{
    private static readonly HttpRequestOptionsKey<BingDynamicApiRequestMetadata> Key = new("Bing.DynamicApi.RequestMetadata");

    internal BingDynamicApiRequestMetadata(string remoteName, BingDynamicApiRequestKind kind, bool replayable, bool streamResult)
    {
        RemoteName = remoteName;
        Kind = kind;
        IsReplayable = replayable;
        IsStreamResult = streamResult;
    }

    /// <summary>远程服务名称。</summary>
    public string RemoteName { get; }
    /// <summary>请求种类。</summary>
    public BingDynamicApiRequestKind Kind { get; }
    /// <summary>客户端是否许可重放；宿主仍需额外批准业务调用。</summary>
    public bool IsReplayable { get; }
    /// <summary>是否返回由调用方管理的流。</summary>
    public bool IsStreamResult { get; }

    /// <summary>尝试读取请求的客户端元数据。</summary>
    public static bool TryGet(HttpRequestMessage request, out BingDynamicApiRequestMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Options.TryGetValue(Key, out metadata);
    }

    internal void Set(HttpRequestMessage request)
    {
        request.Options.Set(Key, this);
        request.Options.Set(new HttpRequestOptionsKey<bool>("Bing.Resilience.IsStreamResult"), IsStreamResult);
        request.Options.Set(new HttpRequestOptionsKey<bool>("Bing.Resilience.IsDefinition"), Kind == BingDynamicApiRequestKind.Definition);
    }
}

/// <summary>适配器登记的远程服务与实际具名 HttpClient 的绑定。</summary>
/// <param name="RemoteName">不区分大小写的远程服务名称。</param>
/// <param name="HttpClientName">注册恢复管线时使用的实际名称，必须精确保留大小写。</param>
public sealed record BingDynamicApiHttpPipelineBinding(string RemoteName, string HttpClientName);

/// <summary>校验中性配置及管线绑定，不创建临时服务提供程序。</summary>
internal sealed class BingDynamicApiPipelineOptionsValidator(IEnumerable<BingDynamicApiHttpPipelineBinding> bindings)
    : IValidateOptions<BingDynamicApiClientOptions>
{
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

/// <summary>一个操作的独立预算；仅将此预算引发的取消识别为超时。</summary>
internal sealed class BingDynamicApiOperationBudget : IDisposable
{
    private readonly CancellationTokenSource _deadline;
    private readonly CancellationToken _caller;

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

    public CancellationToken Token { get; }
    public bool IsTimeout(OperationCanceledException exception) =>
        _deadline != null && _deadline.IsCancellationRequested && !_caller.IsCancellationRequested &&
        exception.CancellationToken == Token;

    public void Dispose() => _deadline?.Dispose();
}
