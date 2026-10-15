using System.Diagnostics;
using System.Globalization;
using Bing.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// 验证集群共享令牌桶的额度与键回收。
/// </summary>
public class RedisTokenBucketRateLimiterTest
{
    /// <summary>
    /// 独立注册 Redis 限流器时同时提供 HTTP 所需的选项基础服务。
    /// </summary>
    [Fact]
    public void AddBingRedisRateLimiting_ShouldRegisterOptionsAndLimiter()
    {
        var services = new ServiceCollection();
        services.AddBingRedisRateLimiting("sample", 2, 1, TimeSpan.FromMinutes(1), 100);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<RegistrationOptions>>().Value.ShouldNotBeNull();
        provider.GetRequiredService<ITokenBucketRateLimiter>().ShouldBeOfType<RedisTokenBucketRateLimiter>();
    }

    /// <summary>
    /// 多实例共享同一策略额度。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_MultipleInstances_ShouldShareQuota()
    {
        var policy = $"bing-rate-test:{Guid.NewGuid():N}";
        var first = Create(policy, 2, 1, 2);
        var second = Create(policy, 2, 1, 2);

        (await first.TryAcquireAsync("tenant-a", 2)).IsAllowed.ShouldBeTrue();
        var rejected = await second.TryAcquireAsync("tenant-a");
        rejected.IsAllowed.ShouldBeFalse();
        rejected.RetryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
        (await second.TryAcquireAsync("tenant-b")).IsAllowed.ShouldBeTrue();
        (await second.TryAcquireAsync("tenant-c")).IsAllowed.ShouldBeFalse();
    }

    /// <summary>
    /// 同一键的并发请求不能超过容量。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_ConcurrentCalls_ShouldAllowOnlyCapacity()
    {
        var limiter = Create($"bing-rate-test:{Guid.NewGuid():N}", 5, 0.01, 10);
        var results = await Task.WhenAll(Enumerable.Range(0, 40)
            .Select(_ => limiter.TryAcquireAsync("shared")));
        results.Count(result => result.IsAllowed).ShouldBe(5);
    }

    /// <summary>
    /// 独立容器中的限流器并发竞争同一策略时不扩大集群额度。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_IndependentProviders_ShouldShareConcurrentQuota()
    {
        var policy = $"bing-rate-test:{Guid.NewGuid():N}";
        var firstServices = new ServiceCollection();
        var secondServices = new ServiceCollection();
        firstServices.AddBingRedisRateLimiting(policy, 5, 0.001, TimeSpan.FromSeconds(1), 10);
        secondServices.AddBingRedisRateLimiting(policy, 5, 0.001, TimeSpan.FromSeconds(1), 10);
        using var firstProvider = firstServices.BuildServiceProvider();
        using var secondProvider = secondServices.BuildServiceProvider();
        var first = firstProvider.GetRequiredService<ITokenBucketRateLimiter>();
        var second = secondProvider.GetRequiredService<ITokenBucketRateLimiter>();
        ReferenceEquals(first, second).ShouldBeFalse();

        try
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 64)
                .Select(index => (index % 2 == 0 ? first : second).TryAcquireAsync("tenant-a")));

            results.Count(result => result.IsAllowed).ShouldBe(5);
            (await first.TryAcquireAsync("tenant-a")).IsAllowed.ShouldBeFalse();
            (await second.TryAcquireAsync("tenant-b", 5)).IsAllowed.ShouldBeTrue();
        }
        finally
        {
            // 低补充速率需要长保留期，只删除本测试独占策略，避免遗留测试分桶。
            var digest = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(policy));
            await RedisHelper.DelAsync("bing:rate:" + Convert.ToHexString(digest));
        }
    }

    /// <summary>
    /// 键达到上限后，闲置键可以回收并供新键使用。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_IdleKey_ShouldBeReclaimed()
    {
        var policy = $"bing-rate-test:{Guid.NewGuid():N}";
        var limiter = new RedisTokenBucketRateLimiter(policy, 1, 20, TimeSpan.FromMilliseconds(120), 1);
        (await limiter.TryAcquireAsync("old")).IsAllowed.ShouldBeTrue();
        (await limiter.TryAcquireAsync("new")).IsAllowed.ShouldBeFalse();
        await Task.Delay(240);
        (await limiter.TryAcquireAsync("new")).IsAllowed.ShouldBeTrue();
    }

    /// <summary>
    /// 相同键在补满前不能因 Redis 闲置过期获得新额度。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_IdleBeforeRefill_ShouldKeepExhaustedQuota()
    {
        var limiter = new RedisTokenBucketRateLimiter($"bing-rate-test:{Guid.NewGuid():N}",
            1, 0.05, TimeSpan.FromMilliseconds(100), 1);
        (await limiter.TryAcquireAsync("same")).IsAllowed.ShouldBeTrue();

        await Task.Delay(250);

        (await limiter.TryAcquireAsync("same")).IsAllowed.ShouldBeFalse();
        (await limiter.TryAcquireAsync("other")).IsAllowed.ShouldBeFalse();
    }

    /// <summary>
    /// 分桶达到上限时返回最早可回收时间，而不是固定的重试时间。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_WhenMaxKeysIsFull_ShouldReturnReclaimWait()
    {
        var limiter = new RedisTokenBucketRateLimiter($"bing-rate-test:{Guid.NewGuid():N}",
            1, 0.01, TimeSpan.FromSeconds(10), 1);

        (await limiter.TryAcquireAsync("old")).IsAllowed.ShouldBeTrue();
        var rejected = await limiter.TryAcquireAsync("new");

        rejected.IsAllowed.ShouldBeFalse();
        rejected.RetryAfter.ShouldBeGreaterThan(TimeSpan.FromSeconds(8));
    }

    /// <summary>
    /// 同一策略中的短恢复桶不能缩短其他桶的状态保留时间。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_OtherBucket_ShouldNotShortenSharedRedisExpiry()
    {
        var limiter = new RedisTokenBucketRateLimiter($"bing-rate-test:{Guid.NewGuid():N}",
            20, 1, TimeSpan.FromMilliseconds(100), 2);
        (await limiter.TryAcquireAsync("slow", 20)).IsAllowed.ShouldBeTrue();
        (await limiter.TryAcquireAsync("fast")).IsAllowed.ShouldBeTrue();

        await Task.Delay(1200);

        (await limiter.TryAcquireAsync("slow", 20)).IsAllowed.ShouldBeFalse();
    }

    /// <summary>
    /// 同名策略的容量和补充速率不一致时应明确拒绝。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_SamePolicyWithDifferentSettings_ShouldFailFast()
    {
        var policy = $"bing-rate-test:{Guid.NewGuid():N}";
        var first = new RedisTokenBucketRateLimiter(policy, 2, 1, TimeSpan.FromSeconds(5), 2);
        var second = new RedisTokenBucketRateLimiter(policy, 3, 1, TimeSpan.FromSeconds(5), 2);
        (await first.TryAcquireAsync("same")).IsAllowed.ShouldBeTrue();

        await Should.ThrowAsync<InvalidOperationException>(() => second.TryAcquireAsync("same"));
        (await first.TryAcquireAsync("same")).IsAllowed.ShouldBeTrue();
    }

    /// <summary>
    /// 同步入口与异步入口共享同一状态。
    /// </summary>
    [Fact]
    public async Task TryAcquire_SyncAndAsync_ShouldShareQuota()
    {
        var limiter = Create($"bing-rate-test:{Guid.NewGuid():N}", 1, 0.01, 1);
        limiter.TryAcquire("shared").IsAllowed.ShouldBeTrue();
        (await limiter.TryAcquireAsync("shared")).IsAllowed.ShouldBeFalse();
    }

    /// <summary>
    /// 调用方在 Redis 操作前取消时不消耗令牌额度。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_WhenCancelledBeforeRedisCall_ShouldNotConsumePermit()
    {
        var limiter = Create($"bing-rate-test:{Guid.NewGuid():N}", 1, 0.01, 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            limiter.TryAcquireAsync("cancelled", cancellationToken: cancellation.Token));
        (await limiter.TryAcquireAsync("cancelled")).IsAllowed.ShouldBeTrue();
    }

    /// <summary>
    /// 验证两个独立 dotnet 进程竞争同一 Redis 策略时不会各自获得完整容量。
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_TwoIndependentProcesses_ShouldShareCapacity()
    {
        var policy = $"bing-rate-process:{Guid.NewGuid():N}";
        const string key = "shared-process-key";
        const int capacity = 5;
        Process first = null;
        Process second = null;
        try
        {
            first = StartProbe(policy, key, capacity, capacity);
            second = StartProbe(policy, key, capacity, capacity);
            var firstResult = await WaitProbeAsync(first);
            var secondResult = await WaitProbeAsync(second);

            first.Id.ShouldNotBe(second.Id);
            (firstResult.Allowed + secondResult.Allowed).ShouldBeLessThanOrEqualTo(capacity);
            (firstResult.Allowed + secondResult.Allowed).ShouldBe(capacity);
        }
        finally
        {
            await StopProbeAsync(first);
            await StopProbeAsync(second);
            var digest = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(policy));
            await RedisHelper.DelAsync("bing:rate:" + Convert.ToHexString(digest));
        }
    }

    /// <summary>
    /// 启动当前测试专属的 Redis 限流探针进程。
    /// </summary>
    private static Process StartProbe(string policy, string key, int capacity, int iterations)
    {
        var assembly = Path.Combine(AppContext.BaseDirectory, "RedisRateLimitProbe",
            "Bing.Caching.CSRedis.RateLimitProbe.dll");
        Assert.True(File.Exists(assembly), "Redis 限流进程探针未复制到测试输出目录。");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add(policy);
        start.ArgumentList.Add(key);
        start.ArgumentList.Add(capacity.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(iterations.ToString(CultureInfo.InvariantCulture));
        return Assert.IsType<Process>(Process.Start(start));
    }

    /// <summary>
    /// 等待独立探针退出并读取有限结果。
    /// </summary>
    private static async Task<(int ProcessId, int Allowed)> WaitProbeAsync(Process process)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await process.WaitForExitAsync(deadline.Token);
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        Assert.Equal(0, process.ExitCode);
        Assert.True(output.Contains("PID=", StringComparison.Ordinal));
        var marker = "ALLOWED=";
        var markerIndex = output.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "探针未返回额度结果。");
        var value = output[(markerIndex + marker.Length)..].Trim();
        Assert.True(int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var allowed),
            $"探针输出格式无效：{error}");
        var pidMarker = "PID=";
        var pidIndex = output.IndexOf(pidMarker, StringComparison.Ordinal);
        var pidText = output[(pidIndex + pidMarker.Length)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        Assert.True(int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out var processId),
            "探针进程标识无效。");
        return (processId, allowed);
    }

    /// <summary>
    /// 仅终止当前测试创建的探针进程。
    /// </summary>
    private static async Task StopProbeAsync(Process process)
    {
        if (process == null || process.HasExited)
            return;
        process.Kill(entireProcessTree: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(deadline.Token);
    }

    /// <summary>
    /// 创建测试限流器。
    /// </summary>
    private static RedisTokenBucketRateLimiter Create(string policy, int capacity, double rate, int maxKeys) =>
        new RedisTokenBucketRateLimiter(policy, capacity, rate, TimeSpan.FromSeconds(5), maxKeys);

    /// <summary>
    /// 验证通用选项服务的测试类型。
    /// </summary>
    private sealed class RegistrationOptions { }
}
