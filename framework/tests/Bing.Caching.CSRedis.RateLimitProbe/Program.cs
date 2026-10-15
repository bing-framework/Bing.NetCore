using System.Globalization;
using System.Text;
using Bing.RateLimiting;
using CSRedis;

namespace Bing.Caching.CSRedis.RateLimitProbe;

/// <summary>
/// 在独立进程中竞争同一 Redis 令牌桶策略。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 运行受界限的令牌申请探针。
    /// </summary>
    /// <param name="args">策略名、分桶键、容量和申请次数。</param>
    private static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            if (args.Length != 4 || !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var capacity) ||
                !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
                capacity <= 0 || capacity > 100 || iterations <= 0 || iterations > 100)
                throw new ArgumentException("探针要求策略名、分桶键、1 至 100 的容量和申请次数。");
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("探针要求 ConnectionStrings__RedisConnection。");
            RedisHelper.Initialization(new CSRedisClient(connection));
            var limiter = new RedisTokenBucketRateLimiter(args[0], capacity, 0.001,
                TimeSpan.FromMinutes(2), 10);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var allowed = 0;
            for (var index = 0; index < iterations; index++)
            {
                if ((await limiter.TryAcquireAsync(args[1], cancellationToken: deadline.Token)).IsAllowed)
                    allowed++;
            }
            Console.WriteLine(string.Concat("PID=", Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                " ALLOWED=", allowed.ToString(CultureInfo.InvariantCulture)));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.GetType().Name);
            return 1;
        }
    }
}
