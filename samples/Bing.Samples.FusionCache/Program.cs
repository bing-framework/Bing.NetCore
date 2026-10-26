using Bing.Caching;
using Bing.Caching.FusionCache;
using Bing.Samples.FusionCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed.Redis;
using ZiggyCreatures.Caching.Fusion.OpenTelemetry;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

const string CacheName = "sample";
const string CacheNamespace = "bing-samples:fusioncache:local";
const string RedisVariable = "BING_TEST_FUSIONCACHE_REDIS";

var mode = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.OrdinalIgnoreCase)) ?? "memory";
var useLogging = args.Contains("--logging", StringComparer.OrdinalIgnoreCase);
var useOpenTelemetry = args.Contains("--otel", StringComparer.OrdinalIgnoreCase);
var useDistributedLocker = args.Contains("--locker", StringComparer.OrdinalIgnoreCase);
var useSizeLimit = args.Contains("--size-limit", StringComparer.OrdinalIgnoreCase);
var useWarmup = mode.Equals("warmup", StringComparison.OrdinalIgnoreCase);
var warmupSettings = useWarmup ? new SampleWarmupSettings(
    args.Contains("--warmup-timeout", StringComparer.OrdinalIgnoreCase)
        ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromSeconds(5),
    args.Contains("--warmup-failure", StringComparer.OrdinalIgnoreCase)) : null;
var useRedis = mode.Equals("redis", StringComparison.OrdinalIgnoreCase) ||
               mode.Equals("multi", StringComparison.OrdinalIgnoreCase) ||
               useDistributedLocker;
var redisConnection = useRedis ? GetRedisConnection() : null;

if (mode.Equals("multi", StringComparison.OrdinalIgnoreCase))
{
    await RunMultiInstanceInvalidationAsync(redisConnection!, useLogging, useOpenTelemetry, useDistributedLocker);
    return;
}

if (!mode.Equals("memory", StringComparison.OrdinalIgnoreCase) &&
    !mode.Equals("redis", StringComparison.OrdinalIgnoreCase) &&
    !mode.Equals("failsafe", StringComparison.OrdinalIgnoreCase) &&
    !mode.Equals("shutdown", StringComparison.OrdinalIgnoreCase) && !useWarmup)
{
    PrintUsage();
    return;
}

if (useSizeLimit && !mode.Equals("memory", StringComparison.OrdinalIgnoreCase))
{
    PrintUsage();
    return;
}

using var host = CreateHost(
    useRedis, redisConnection, useLogging, useOpenTelemetry, useDistributedLocker, useSizeLimit, warmupSettings);
using var startupCancellation = new CancellationTokenSource();
if (useWarmup && args.Contains("--warmup-cancel", StringComparer.OrdinalIgnoreCase))
    startupCancellation.CancelAfter(TimeSpan.FromMilliseconds(150));
try
{
    await host.StartAsync(startupCancellation.Token);
}
catch (OperationCanceledException) when (startupCancellation.IsCancellationRequested)
{
    Console.WriteLine("host startup cancelled; no successful startup is reported.");
    return;
}

try
{
    var cache = host.Services.GetRequiredService<IHybridCacheProvider>().GetCache(CacheName);
    switch (mode.ToLowerInvariant())
    {
        case "warmup":
            var report = host.Services.GetRequiredService<SampleWarmupHostedService>().Result!;
            if (report.Succeeded == report.Requested)
            {
                foreach (var key in SampleWarmupHostedService.HotKeys)
                    await cache.GetOrCreateAsync<string>(key,
                        _ => throw new InvalidOperationException("A warmed key unexpectedly required the origin."));
                Console.WriteLine("host started; all hot products hit the cache without another origin call.");
            }
            else
            {
                Console.WriteLine("host started with partial warmup; remaining products will load on demand.");
            }
            break;
        case "memory":
        case "redis":
            await RunReadThroughAsync(host.Services, cache);
            break;
        case "failsafe":
            await RunFailSafeAsync(cache);
            break;
        case "shutdown":
            await cache.SetAsync("shutdown", "old");
            await cache.ExpireAsync("shutdown");
            var lifetime = host.Services.GetRequiredService<SampleFactoryLifetime>();
            var result = await cache.GetOrCreateAsync("shutdown", token => lifetime.RunAsync(async ct =>
            {
                await using var scope = host.Services.CreateAsyncScope();
                var reader = scope.ServiceProvider.GetRequiredService<SampleProductReader>();
                await Task.Delay(800, ct);
                return await reader.ReadAsync(ct);
            }, token));
            Console.WriteLine($"soft timeout returned {result}; StopAsync will drain the background scope.");
            break;
    }
}
finally
{
    await host.StopAsync();
}

static string GetRedisConnection()
{
    var connection = Environment.GetEnvironmentVariable(RedisVariable);
    if (string.IsNullOrWhiteSpace(connection))
    {
        throw new InvalidOperationException(
            $"Redis mode requires the disposable test/dev endpoint in environment variable {RedisVariable}.");
    }

    return connection;
}

static IHost CreateHost(
    bool useRedis,
    string? redisConnection,
    bool useLogging,
    bool useOpenTelemetry,
    bool useDistributedLocker,
    bool useSizeLimit = false,
    SampleWarmupSettings? warmupSettings = null)
{
    var builder = Host.CreateApplicationBuilder();
    builder.Logging.ClearProviders();
    if (useLogging)
    {
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
    }

    if (useOpenTelemetry)
    {
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddFusionCacheInstrumentation()
                .AddSource("Bing.Caching.FusionCache")
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddFusionCacheInstrumentation()
                .AddMeter("Bing.Caching.FusionCache")
                .AddConsoleExporter());
    }

    builder.Services.AddScoped<SampleProductReader>();
    if (useSizeLimit)
    {
        builder.Services.AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000
        }));
    }

    if (useRedis)
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = string.Empty;
        });
        builder.Services.AddSingleton(_ => new RedisBackplane(new RedisBackplaneOptions
        {
            Configuration = redisConnection
        }));
        if (useDistributedLocker)
        {
            builder.Services.AddSingleton(_ => new RedisDistributedLocker(
                Options.Create(new RedisDistributedLockerOptions
                {
                    Configuration = redisConnection
                })));
        }
    }

    builder.Services.AddBingFusionCache(
        CacheName,
        options =>
        {
            options.Namespace = CacheNamespace;
        },
        fusion =>
        {
            if (useSizeLimit)
            {
                fusion.WithMemoryCache(serviceProvider => serviceProvider.GetRequiredService<IMemoryCache>());
            }

            fusion.WithDefaultEntryOptions(options =>
            {
                options.Duration = TimeSpan.FromMinutes(5);
                options.IsFailSafeEnabled = true;
                options.FailSafeMaxDuration = TimeSpan.FromMinutes(10);
                options.FailSafeThrottleDuration = TimeSpan.FromSeconds(10);
                options.FactorySoftTimeout = TimeSpan.FromMilliseconds(200);
                options.FactoryHardTimeout = TimeSpan.FromSeconds(2);
                options.AllowTimedOutFactoryBackgroundCompletion = true;
                options.EagerRefreshThreshold = 0.8f;
                if (warmupSettings is not null)
                {
                    // 预热结果按前台调用统计，不用旧值后备或后台刷新掩盖失败与取消。
                    options.IsFailSafeEnabled = false;
                    options.FactorySoftTimeout = Timeout.InfiniteTimeSpan;
                    options.AllowTimedOutFactoryBackgroundCompletion = false;
                    options.EagerRefreshThreshold = null;
                }
                if (useSizeLimit)
                {
                    options.Size = 1;
                }
            });

            fusion.WithOptions(options =>
            {
                options.AutoRecoveryMaxItems = 1000;
                if (useSizeLimit)
                {
                    // 标签元数据使用 0 单位；保留上游的长保留期，不套用业务 TTL。
                    options.TagsDefaultEntryOptions.Size = 0;
                }
            });
            if (useRedis)
            {
                fusion
                    .WithSerializer(new FusionCacheSystemTextJsonSerializer())
                    .WithRegisteredDistributedCache()
                    .WithBackplane(serviceProvider => serviceProvider.GetRequiredService<RedisBackplane>());
            }

            if (useDistributedLocker)
            {
                fusion.WithDistributedLocker(
                    serviceProvider => serviceProvider.GetRequiredService<RedisDistributedLocker>());
            }
        });

    builder.Services.AddSingleton<SampleFactoryLifetime>();
    if (warmupSettings is not null)
    {
        builder.Services.AddSingleton(warmupSettings);
        builder.Services.AddSingleton<SampleWarmupHostedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<SampleWarmupHostedService>());
    }
    // 最后注册，停止时首先排空真实工厂，然后才允许容器释放作用域资源。
    builder.Services.AddHostedService(sp => sp.GetRequiredService<SampleFactoryLifetime>());
    return builder.Build();
}

static async Task RunReadThroughAsync(IServiceProvider services, IHybridCache cache)
{
    var key = $"product:{Guid.NewGuid():N}";
    var tags = new[] { "catalog" };
    var options = new CacheOptions { Expiration = TimeSpan.FromMinutes(5) };

    var first = await cache.GetOrCreateAsync(
        key,
        token => LoadInOwnScopeAsync(services, token),
        options,
        tags);
    var second = await cache.GetOrCreateAsync(
        key,
        token => LoadInOwnScopeAsync(services, token),
        options,
        tags);

    Console.WriteLine($"first value:  {first}");
    Console.WriteLine($"cached value: {second}");
    await cache.InvalidateTagAsync(tags[0]);
    var afterInvalidation = await cache.TryGetAsync<string>(key);
    Console.WriteLine($"tag invalidated; hit={afterInvalidation.Hit}");
}

static async Task RunFailSafeAsync(IHybridCache cache)
{
    var key = $"failsafe:{Guid.NewGuid():N}";
    var options = new CacheOptions { Expiration = TimeSpan.FromSeconds(1) };
    await cache.SetAsync(key, "last-known-good", options);
    await Task.Delay(TimeSpan.FromSeconds(2));

    var value = await cache.GetOrCreateAsync<string>(
        key,
        _ => Task.FromException<string>(new InvalidOperationException("Simulated origin outage.")),
        options);

    Console.WriteLine($"origin failed; FusionCache returned fail-safe value: {value}");
}

static async Task RunMultiInstanceInvalidationAsync(
    string redisConnection,
    bool useLogging,
    bool useOpenTelemetry,
    bool useDistributedLocker)
{
    using var hostA = CreateHost(true, redisConnection, useLogging, useOpenTelemetry, useDistributedLocker);
    using var hostB = CreateHost(true, redisConnection, useLogging, useOpenTelemetry, useDistributedLocker);
    await hostA.StartAsync();
    await hostB.StartAsync();

    try
    {
        var cacheA = hostA.Services.GetRequiredService<IHybridCacheProvider>().GetCache(CacheName);
        var cacheB = hostB.Services.GetRequiredService<IHybridCacheProvider>().GetCache(CacheName);
        var key = $"multi:{Guid.NewGuid():N}";
        var cacheOptions = new CacheOptions { Expiration = TimeSpan.FromMinutes(2) };

        await cacheA.SetAsync(key, "v1", cacheOptions);
        var warmed = await cacheB.TryGetAsync<string>(key);
        if (!warmed.Hit || warmed.Value != "v1")
        {
            throw new InvalidOperationException("The second cache instance did not read the shared Redis L2 value.");
        }

        await cacheA.SetAsync(key, "v2", cacheOptions);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var current = await cacheB.TryGetAsync<string>(key);
            if (current.Hit && current.Value == "v2")
            {
                Console.WriteLine("second cache instance observed the updated value after Redis Backplane invalidation.");
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException("The second cache instance did not observe the Redis Backplane update within 5 seconds.");
    }
    finally
    {
        await hostB.StopAsync();
        await hostA.StopAsync();
    }
}

static Task<string> LoadInOwnScopeAsync(IServiceProvider services, CancellationToken cancellationToken) =>
    services.GetRequiredService<SampleFactoryLifetime>().RunAsync(async token =>
{
    await using var scope = services.CreateAsyncScope();
    var reader = scope.ServiceProvider.GetRequiredService<SampleProductReader>();
    return await reader.ReadAsync(token);
}, cancellationToken);

static void PrintUsage()
{
    Console.WriteLine("Usage: Bing.Samples.FusionCache [memory|redis|multi|failsafe|shutdown|warmup] [--logging] [--otel] [--locker] [--size-limit]");
    Console.WriteLine("warmup accepts --warmup-failure, --warmup-timeout or --warmup-cancel to demonstrate failure, budget exhaustion or startup cancellation.");
    Console.WriteLine("--size-limit is available only in memory mode and limits a dedicated L1 cache to 1000 entry units.");
    Console.WriteLine("Redis modes read only BING_TEST_FUSIONCACHE_REDIS; use a disposable test/dev Redis endpoint.");
}

/// <summary>
/// 模拟从数据源读取产品名称的作用域服务。
/// </summary>
internal sealed class SampleProductReader
{
    /// <summary>
    /// 全局递增的读取序号，用于让每次回源结果可区分。
    /// </summary>
    private static int _sequence;

    /// <summary>
    /// 模拟异步读取一个产品名称。
    /// </summary>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <returns>包含递增序号的产品名称。</returns>
    public async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        return $"origin-{Interlocked.Increment(ref _sequence)}";
    }
}
