using Bing.Resilience;

namespace Bing.Http.Resilience;

/// <summary>具名 HTTP 客户端的恢复配置；注册时冻结，变更需重启宿主。</summary>
public sealed class BingHttpResilienceOptions
{
    /// <summary>获取或设置一次逻辑发送的总预算，包含排队、重试和退避，不包含响应体读取。</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(8);
    /// <summary>获取或设置单次网络发送预算。</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(2);
    /// <summary>获取重试配置。</summary>
    public BingHttpRetryOptions Retry { get; } = new();
    /// <summary>获取熔断配置。</summary>
    public BingHttpCircuitBreakerOptions CircuitBreaker { get; } = new();
    /// <summary>获取并发配置。</summary>
    public BingHttpConcurrencyOptions Concurrency { get; } = new();
    /// <summary>获取或设置业务重放许可；默认不许可。即使许可，也仅重放无请求体的 GET/HEAD。</summary>
    /// <remarks>只应许可已确认幂等且返回普通数据的操作，流结果必须排除。委托应无副作用且不抛异常。</remarks>
    public Func<HttpRequestMessage, bool>? CanRetryRequest { get; set; }

    /// <summary>指定 Provider；为空使用宿主显式登记的默认 Provider。</summary>
    public string? ProviderName { get; set; }
    /// <summary>启用可选发现路由；必须另行登记对应客户端的路由策略。</summary>
    public bool UseDynamicRouting { get; set; }
    public IHttpSyntheticResponsePolicy? SyntheticResponsePolicy { get; set; }
    public IList<ResilienceOperationDescriptor> SyntheticOperations { get; } = new List<ResilienceOperationDescriptor>();
    public IHttpResilienceResponseObserver? ResponseObserver { get; set; }
    public BingHttpResilienceMode Mode { get; set; }
    public ResilienceRateLimitOptions RateLimit { get; } = new();
    public IList<IResilienceAttemptObserver<HttpResponseMessage>> AttemptObservers { get; } = new List<IResilienceAttemptObserver<HttpResponseMessage>>();
    public BingHttpHedgingOptions Hedging { get; } = new();
    /// <summary>独立并行许可；默认不许可，不能用重试白名单替代。</summary>
    public Func<HttpRequestMessage, bool>? CanHedgeRequest { get; set; }
    /// <summary>在 Hedging 模式中保留顺序恢复的元数据请求；默认无。</summary>
    public Func<HttpRequestMessage, bool>? UseSequentialRetry { get; set; }

    /// <summary>把配置复制到另一注册对象；包括集合快照，避免适配器遗漏新增字段。</summary>
    public void CopyTo(BingHttpResilienceOptions target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (ReferenceEquals(this, target)) return;
        target.SyntheticResponsePolicy = SyntheticResponsePolicy; target.ResponseObserver = ResponseObserver;
        target.SyntheticOperations.Clear(); foreach (var operation in SyntheticOperations) target.SyntheticOperations.Add(operation);
        target.ProviderName = ProviderName; target.Mode = Mode; target.UseDynamicRouting = UseDynamicRouting;
        target.RequestTimeout = RequestTimeout; target.AttemptTimeout = AttemptTimeout;
        target.CanRetryRequest = CanRetryRequest; target.CanHedgeRequest = CanHedgeRequest; target.UseSequentialRetry = UseSequentialRetry;
        target.Retry.MaxRetryAttempts = Retry.MaxRetryAttempts; target.Retry.Delay = Retry.Delay;
        target.Retry.MaxDelay = Retry.MaxDelay; target.Retry.UseJitter = Retry.UseJitter;
        target.CircuitBreaker.Enabled = CircuitBreaker.Enabled;
        target.CircuitBreaker.EnableManualControl = CircuitBreaker.EnableManualControl;
        target.CircuitBreaker.FailureRatio = CircuitBreaker.FailureRatio;
        target.CircuitBreaker.MinimumThroughput = CircuitBreaker.MinimumThroughput;
        target.CircuitBreaker.SamplingDuration = CircuitBreaker.SamplingDuration;
        target.CircuitBreaker.BreakDuration = CircuitBreaker.BreakDuration;
        target.Concurrency.Enabled = Concurrency.Enabled;
        target.Concurrency.PermitLimit = Concurrency.PermitLimit; target.Concurrency.QueueLimit = Concurrency.QueueLimit;
        target.RateLimit.Enabled = RateLimit.Enabled; target.RateLimit.TokenLimit = RateLimit.TokenLimit;
        target.RateLimit.TokensPerPeriod = RateLimit.TokensPerPeriod;
        target.RateLimit.ReplenishmentPeriod = RateLimit.ReplenishmentPeriod; target.RateLimit.QueueLimit = RateLimit.QueueLimit;
        target.AttemptObservers.Clear(); foreach (var observer in AttemptObservers) target.AttemptObservers.Add(observer);
        Hedging.CopyTo(target.Hedging);
    }

    internal BingHttpResilienceOptions Snapshot()
    {
        var copy = new BingHttpResilienceOptions(); CopyTo(copy); copy.Validate(); return copy;
    }

    internal void Validate()
    {
        if (SyntheticResponsePolicy is not null && SyntheticOperations.Count == 0) throw new ArgumentException("合成响应需要宿主操作白名单。");
        if (!Enum.IsDefined(typeof(BingHttpResilienceMode), Mode)) throw new ArgumentException("HTTP 恢复模式无效。");
        if (Mode == BingHttpResilienceMode.Hedging || UseDynamicRouting) Hedging.Validate(UseDynamicRouting);
        Positive(RequestTimeout, nameof(RequestTimeout));
        Positive(AttemptTimeout, nameof(AttemptTimeout));
        if (AttemptTimeout > RequestTimeout)
            throw new ArgumentException("AttemptTimeout 不能超过 RequestTimeout。");
        if (Retry.MaxRetryAttempts < 0 || Retry.MaxRetryAttempts > 100 ||
            Retry.Delay < TimeSpan.Zero || Retry.MaxDelay < Retry.Delay || Retry.MaxDelay > TimeSpan.FromDays(1))
            throw new ArgumentException("重试次数必须在 0 到 100 之间，且 0 <= Delay <= MaxDelay。");
        if (CircuitBreaker.Enabled)
        {
            if (CircuitBreaker.FailureRatio <= 0 || CircuitBreaker.FailureRatio > 1 || double.IsNaN(CircuitBreaker.FailureRatio) ||
                CircuitBreaker.MinimumThroughput < 2 || CircuitBreaker.SamplingDuration < TimeSpan.FromMilliseconds(500) ||
                CircuitBreaker.SamplingDuration > TimeSpan.FromDays(1) || CircuitBreaker.BreakDuration < TimeSpan.FromMilliseconds(500))
                throw new ArgumentException("熔断比例、最小吞吐量或采样窗口无效。");
            Positive(CircuitBreaker.BreakDuration, nameof(CircuitBreaker.BreakDuration));
        }
        if (Concurrency.Enabled && (Concurrency.PermitLimit <= 0 || Concurrency.QueueLimit < 0))
            throw new ArgumentException("并发许可必须大于零，队列长度不能为负数。");
    }

    private static void Positive(TimeSpan value, string name)
    {
        if (value < TimeSpan.FromMilliseconds(10) || value > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(name, "预算必须在 10 毫秒到 1 天之间。");
    }
}

/// <summary>HTTP 重试配置。</summary>
public sealed class BingHttpRetryOptions
{
    /// <summary>最大重试次数；零表示省略重试策略。</summary>
    public int MaxRetryAttempts { get; set; } = 2;
    /// <summary>指数退避的初始延迟。</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(200);
    /// <summary>本地退避上限；不截短服务器的 Retry-After。</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);
    /// <summary>是否对本地退避添加随机抖动。</summary>
    public bool UseJitter { get; set; } = true;
}

/// <summary>按实际 HttpClient 名称隔离的熔断配置。</summary>
public sealed class BingHttpCircuitBreakerOptions
{
    /// <summary>是否开放中性手动熔断控制。</summary>
    public bool EnableManualControl { get; set; }
    /// <summary>是否启用熔断。</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>触发熔断的失败比例；429 不计入失败。</summary>
    public double FailureRatio { get; set; } = .5;
    /// <summary>采样窗口中允许熔断的最小尝试次数。</summary>
    public int MinimumThroughput { get; set; } = 20;
    /// <summary>采样窗口。</summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>熔断打开时间。</summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(15);
}

/// <summary>覆盖重试及退避阶段的并发限制配置。</summary>
public sealed class BingHttpConcurrencyOptions
{
    /// <summary>是否限制并发。</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>并发逻辑发送数量。</summary>
    public int PermitLimit { get; set; } = 32;
    /// <summary>等待许可的队列长度；默认不排队。</summary>
    public int QueueLimit { get; set; }
}
