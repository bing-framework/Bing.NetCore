using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bing.RateLimiting;

namespace Bing.RateLimiting;

/// <summary>
/// 使用 Redis 服务端时间和原子脚本共享令牌额度。
/// </summary>
public sealed class RedisTokenBucketRateLimiter : ITokenBucketRateLimiter
{
    /// <summary>
    /// 单个 Redis Hash 中保存各分桶及其闲置时间。
    /// </summary>
    private const string Script = @"
local key = KEYS[1]
local field = ARGV[1]
local capacity = tonumber(ARGV[2])
local refill = tonumber(ARGV[3])
local permits = tonumber(ARGV[4])
local idle = tonumber(ARGV[5])
local maxkeys = tonumber(ARGV[6])
local configured = redis.call('HGET', key, '__config')
if configured and configured ~= ARGV[7] then return -2 end
if not configured then redis.call('HSET', key, '__config', ARGV[7]) end
local function expiry(tokens)
  return math.max(idle, math.ceil((capacity - tokens) / refill))
end
local function keep_expiry(required)
  local current = redis.call('PTTL', key)
  if current < required then redis.call('PEXPIRE', key, required) end
end
local time = redis.call('TIME')
local now = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)
local record = redis.call('HGET', key, field)
if not record then
  if redis.call('HLEN', key) - 1 - (redis.call('HEXISTS', key, '__sweep_at')) >= maxkeys then
    local sweep = tonumber(redis.call('HGET', key, '__sweep_at') or '0')
    if now >= sweep then
      local values = redis.call('HGETALL', key)
      for i = 1, #values, 2 do
        if values[i] ~= '__sweep_at' and values[i] ~= '__config' then
          local stored, updated, seen = string.match(values[i + 1], '^([^|]+)|([^|]+)|([^|]+)$')
          if now - tonumber(seen) >= idle and
             tonumber(stored) + math.max(0, now - tonumber(updated)) * refill >= capacity then
            redis.call('HDEL', key, values[i])
          end
        end
      end
      redis.call('HSET', key, '__sweep_at', now + 1000)
    end
    if redis.call('HLEN', key) - 1 - redis.call('HEXISTS', key, '__sweep_at') >= maxkeys then
      local next = 2147483647
      local current = redis.call('HGETALL', key)
      for i = 1, #current, 2 do
        if current[i] ~= '__sweep_at' and current[i] ~= '__config' then
          local stored, updated, seen = string.match(current[i + 1], '^([^|]+)|([^|]+)|([^|]+)$')
          local available = math.min(capacity, tonumber(stored) + math.max(0, now - tonumber(updated)) * refill)
          local idle_wait = math.max(0, idle - (now - tonumber(seen)))
          local refill_wait = math.max(0, math.ceil((capacity - available) / refill))
          local wait = math.max(idle_wait, refill_wait)
          if wait < next then next = wait end
        end
      end
      return math.min(2147483647, math.max(1, next))
    end
  end
  record = tostring(capacity) .. '|' .. tostring(now) .. '|' .. tostring(now)
end
local tokens, last = string.match(record, '^([^|]+)|([^|]+)|')
tokens = math.min(capacity, tonumber(tokens) + math.max(0, now - tonumber(last)) * refill)
if tokens < permits then
  redis.call('HSET', key, field, tostring(tokens) .. '|' .. tostring(now) .. '|' .. tostring(now))
  keep_expiry(expiry(tokens))
  return math.min(2147483647, math.max(1, math.ceil((permits - tokens) / refill)))
end
redis.call('HSET', key, field, tostring(tokens - permits) .. '|' .. tostring(now) .. '|' .. tostring(now))
keep_expiry(expiry(tokens - permits))
return -1";

    /// <summary>
    /// Redis 中的策略键。
    /// </summary>
    private readonly string _redisKey;

    /// <summary>
    /// 每个分桶的令牌容量。
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 每毫秒补充令牌数。
    /// </summary>
    private readonly double _refillPerMillisecond;

    /// <summary>
    /// 闲置回收时长。
    /// </summary>
    private readonly long _idleMilliseconds;

    /// <summary>
    /// 最多保留的分桶数量。
    /// </summary>
    private readonly int _maxKeys;

    /// <summary>
    /// 初始化一个 <see cref="RedisTokenBucketRateLimiter" /> 类型的实例。
    /// </summary>
    public RedisTokenBucketRateLimiter(string policyName, int capacity, double refillTokensPerSecond,
        TimeSpan idleTimeout, int maxKeys)
    {
        if (string.IsNullOrWhiteSpace(policyName))
            throw new ArgumentException("策略名称不能为空。", nameof(policyName));
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        if (double.IsNaN(refillTokensPerSecond) || double.IsInfinity(refillTokensPerSecond) || refillTokensPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(refillTokensPerSecond));
        if (idleTimeout <= TimeSpan.Zero || idleTimeout.TotalMilliseconds > long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        if (maxKeys <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxKeys));
        _redisKey = $"bing:rate:{Hash(policyName)}";
        _capacity = capacity;
        _refillPerMillisecond = refillTokensPerSecond / 1000;
        if (_refillPerMillisecond == 0 || capacity / _refillPerMillisecond > 9007199254740991d)
            throw new ArgumentOutOfRangeException(nameof(refillTokensPerSecond));
        _idleMilliseconds = Math.Max(1, (long)Math.Ceiling(idleTimeout.TotalMilliseconds));
        _maxKeys = maxKeys;
    }

    /// <inheritdoc />
    public RateLimitResult TryAcquire(string key, int permits = 1) =>
        ConvertResult(RedisHelper.Eval(Script, _redisKey, Arguments(key, permits)));

    /// <inheritdoc />
    public async Task<RateLimitResult> TryAcquireAsync(string key, int permits = 1, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await RedisHelper.EvalAsync(Script, _redisKey, Arguments(key, permits)).ConfigureAwait(false);
        // Redis 脚本已经原子提交额度；此处不能再因调用方随后取消而伪装成未执行，否则重试可能重复消耗额度。
        return ConvertResult(result);
    }

    /// <summary>
    /// 构造脚本参数。
    /// </summary>
    private object[] Arguments(string key, int permits)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("限流键不能为空。", nameof(key));
        if (permits <= 0 || permits > _capacity)
            throw new ArgumentOutOfRangeException(nameof(permits));
        var refill = _refillPerMillisecond.ToString("R", CultureInfo.InvariantCulture);
        return new object[] { Hash(key), _capacity, refill, permits, _idleMilliseconds, _maxKeys,
            string.Concat(_capacity.ToString(CultureInfo.InvariantCulture), "|", refill, "|",
                _idleMilliseconds.ToString(CultureInfo.InvariantCulture), "|", _maxKeys.ToString(CultureInfo.InvariantCulture)) };
    }

    /// <summary>
    /// 转换脚本返回的等待毫秒数。
    /// </summary>
    private static RateLimitResult ConvertResult(object result)
    {
        var milliseconds = Convert.ToInt64(result, CultureInfo.InvariantCulture);
        if (milliseconds == -2)
            throw new InvalidOperationException("Redis 中同名限流策略的配置与当前实例不一致。");
        return milliseconds < 0
            ? new RateLimitResult(true, TimeSpan.Zero)
            : new RateLimitResult(false, TimeSpan.FromMilliseconds(milliseconds));
    }

    /// <summary>
    /// 用固定长度摘要作为 Redis 字段。
    /// </summary>
    private static string Hash(string value)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
    }
}
