using Bing.Timing;

namespace Bing.RateLimiting;

/// <summary>
/// 进程内按键令牌桶。每个实例独立计额，不跨进程共享。
/// </summary>
public sealed class LocalTokenBucketRateLimiter : ITokenBucketRateLimiter
{
    /// <summary>
    /// 保护限流键集合的同步锁。
    /// </summary>
    private readonly object _sync = new();
    /// <summary>
    /// 保存各限流键对应的令牌桶。
    /// </summary>
    private readonly Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    /// <summary>
    /// 提供令牌补充和闲置回收所用的时钟。
    /// </summary>
    private readonly IClock _clock;
    /// <summary>
    /// 每个限流键的令牌容量。
    /// </summary>
    private readonly int _capacity;
    /// <summary>
    /// 每秒补充的令牌数。
    /// </summary>
    private readonly double _refillTokensPerSecond;
    /// <summary>
    /// 限流键的闲置回收时长。
    /// </summary>
    private readonly TimeSpan _idleTimeout;
    /// <summary>
    /// 当前实例可保留的限流键数上限。
    /// </summary>
    private readonly int _maxKeys;
    /// <summary>
    /// 距离下一次回收检查的请求计数。
    /// </summary>
    private int _requestsSinceSweep;

    /// <summary>
    /// 初始化一个 <see cref="LocalTokenBucketRateLimiter" /> 类型的实例。
    /// </summary>
    /// <remarks>每个键使用固定令牌容量，并受实例键数上限约束。</remarks>
    /// <param name="capacity">每个键的最大令牌数。</param>
    /// <param name="refillTokensPerSecond">每秒补充的令牌数。</param>
    /// <param name="idleTimeout">键连续闲置多久后可回收。</param>
    /// <param name="maxKeys">实例最多保留的键数。</param>
    /// <param name="clock">可注入时钟；默认使用系统时钟。</param>
    public LocalTokenBucketRateLimiter(int capacity, double refillTokensPerSecond,
        TimeSpan idleTimeout, int maxKeys, IClock clock = null)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        if (double.IsNaN(refillTokensPerSecond) || double.IsInfinity(refillTokensPerSecond) || refillTokensPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(refillTokensPerSecond));
        if (idleTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        if (maxKeys <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxKeys));

        _capacity = capacity;
        _refillTokensPerSecond = refillTokensPerSecond;
        _idleTimeout = idleTimeout;
        _maxKeys = maxKeys;
        _clock = clock ?? SystemClock.Instance;
    }

    /// <summary>
    /// 当前保留的键数。
    /// </summary>
    public int KeyCount
    {
        get
        {
            lock (_sync)
                return _buckets.Count;
        }
    }

    /// <inheritdoc />
    public RateLimitResult TryAcquire(string key, int permits = 1)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("限流键不能为空。", nameof(key));
        if (permits <= 0 || permits > _capacity)
            throw new ArgumentOutOfRangeException(nameof(permits));

        lock (_sync)
        {
            var now = _clock.UtcNow;
            if (++_requestsSinceSweep >= 64 || _buckets.Count == _maxKeys)
            {
                PruneIdleCore(now);
                _requestsSinceSweep = 0;
            }

            if (!_buckets.TryGetValue(key, out var bucket))
            {
                if (_buckets.Count == _maxKeys)
                    return new RateLimitResult(false, NextIdleExpiry(now));

                bucket = new Bucket(_capacity, now);
                _buckets.Add(key, bucket);
            }

            // 时间回拨不增加额度，也不把上次观察时间移回过去。
            var elapsed = now > bucket.LastRefill ? (now - bucket.LastRefill).TotalSeconds : 0;
            if (elapsed > 0)
            {
                bucket.Tokens = Math.Min(_capacity, bucket.Tokens + elapsed * _refillTokensPerSecond);
                bucket.LastRefill = now;
            }
            if (now > bucket.LastSeen)
                bucket.LastSeen = now;

            if (bucket.Tokens >= permits)
            {
                bucket.Tokens -= permits;
                return new RateLimitResult(true, TimeSpan.Zero);
            }

            var waitSeconds = (permits - bucket.Tokens) / _refillTokensPerSecond;
            var retryAfter = waitSeconds >= TimeSpan.MaxValue.TotalSeconds
                ? TimeSpan.MaxValue
                : TimeSpan.FromSeconds(waitSeconds);
            return new RateLimitResult(false, retryAfter);
        }
    }

    /// <inheritdoc />
    public Task<RateLimitResult> TryAcquireAsync(string key, int permits = 1, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryAcquire(key, permits));
    }

    /// <summary>
    /// 回收已闲置的限流键。
    /// </summary>
    /// <remarks>普通请求也会定期触发回收。</remarks>
    public int PruneIdle()
    {
        lock (_sync)
            return PruneIdleCore(_clock.UtcNow);
    }

    /// <summary>
    /// 回收已闲置且令牌已补满的键。
    /// </summary>
    /// <param name="now">当前时间。</param>
    /// <returns>回收的键数。</returns>
    private int PruneIdleCore(DateTime now)
    {
        var expired = _buckets.Where(pair => now > pair.Value.LastSeen && now - pair.Value.LastSeen >= _idleTimeout
            && AvailableTokens(pair.Value, now) >= _capacity)
            .Select(pair => pair.Key).ToArray();
        foreach (var key in expired)
            _buckets.Remove(key);
        return expired.Length;
    }

    /// <summary>
    /// 计算任一现存键可回收前的最短等待时间。
    /// </summary>
    /// <param name="now">当前时间。</param>
    /// <returns>最短等待时间。</returns>
    private TimeSpan NextIdleExpiry(DateTime now)
    {
        return _buckets.Values.Select(bucket =>
        {
            var idle = now > bucket.LastSeen ? now - bucket.LastSeen : TimeSpan.Zero;
            var idleWait = idle < _idleTimeout ? _idleTimeout - idle : TimeSpan.Zero;
            var remaining = (_capacity - AvailableTokens(bucket, now)) / _refillTokensPerSecond;
            var refillWait = remaining >= TimeSpan.MaxValue.TotalSeconds
                ? TimeSpan.MaxValue
                : TimeSpan.FromSeconds(Math.Max(0, remaining));
            return idleWait > refillWait ? idleWait : refillWait;
        }).Min();
    }

    /// <summary>
    /// 计算不改变桶状态的当前额度。
    /// </summary>
    private double AvailableTokens(Bucket bucket, DateTime now)
    {
        var elapsed = now > bucket.LastRefill ? (now - bucket.LastRefill).TotalSeconds : 0;
        return Math.Min(_capacity, bucket.Tokens + elapsed * _refillTokensPerSecond);
    }

    /// <summary>
    /// 保存单个限流键的令牌及更新时间。
    /// </summary>
    private sealed class Bucket
    {
        /// <summary>
        /// 初始化一个 <see cref="Bucket" /> 类型的实例。
        /// </summary>
        /// <param name="capacity">令牌容量。</param>
        /// <param name="now">当前时间。</param>
        public Bucket(int capacity, DateTime now)
        {
            Tokens = capacity;
            LastRefill = now;
            LastSeen = now;
        }

        /// <summary>
        /// 获取或设置当前可用令牌数。
        /// </summary>
        public double Tokens { get; set; }

        /// <summary>
        /// 获取或设置上次补充令牌的时间。
        /// </summary>
        public DateTime LastRefill { get; set; }

        /// <summary>
        /// 获取或设置上次访问时间。
        /// </summary>
        public DateTime LastSeen { get; set; }
    }
}
