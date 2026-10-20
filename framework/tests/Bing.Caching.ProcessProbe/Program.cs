using Bing.Caching;
using Bing.Caching.CSRedis;
using Bing.EasyCaching;
using Bing.Caching.FreeRedis;
using CSRedis;
using EasyCaching.Core;
using FreeRedis;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Text.Json;
using StackExchange.Redis;

const string defaultKey = "same-key";

if (args.Length < 4)
    throw new ArgumentException("provider connection namespace startUtcTicks [mode] [options]");

var provider = args[0];
var connection = args[1];
var cacheNamespace = args[2];
var startTicks = long.Parse(args[3], CultureInfo.InvariantCulture);
var options = ProbeOptions.Parse(args);

var start = new DateTime(startTicks, DateTimeKind.Utc);
var delay = start - DateTime.UtcNow;
if (delay > TimeSpan.Zero)
    await Task.Delay(delay);

using var context = CreateContext(provider, connection, cacheNamespace, options);
Environment.ExitCode = await RunAsync(context, options, defaultKey);

static ProbeContext CreateContext(string provider, string connection, string cacheNamespace, ProbeOptions options)
{
    var coordination = options.UseDefaultCoordination
        ? null
        : new CacheCoordinationOptions(
            TimeSpan.FromMilliseconds(options.LeaseMs),
            TimeSpan.FromMilliseconds(options.RenewMs),
            TimeSpan.FromMilliseconds(options.WaitMs),
            TimeSpan.FromMilliseconds(options.RetrievalMs));
    var configuration = new CacheConfiguration(cacheNamespace, provider, coordination: coordination);

    if (string.Equals(provider, "freeredis", StringComparison.OrdinalIgnoreCase))
    {
        var redis = new global::FreeRedis.RedisClient(connection);
        redis.Serialize = value => JsonSerializer.Serialize(value);
        redis.Deserialize = (json, type) => JsonSerializer.Deserialize(json, type);
        var cache = new FreeRedisCacheManager(redis, configuration);
        return new ProbeContext(cache, redis, leaseKey => redis.Del(leaseKey), configuration);
    }

    if (string.Equals(provider, "csredis", StringComparison.OrdinalIgnoreCase))
    {
        var redis = new CSRedisClient(connection);
        var cache = new CSRedisCacheManager(redis, configuration);
        return new ProbeContext(cache, redis, leaseKey => redis.Del(leaseKey), configuration);
    }

    if (string.Equals(provider, "easyredis", StringComparison.OrdinalIgnoreCase))
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCaching(configuration, options =>
            options.DBConfig.ConfigurationOptions = ConfigurationOptions.Parse(connection));
        var serviceProvider = services.BuildServiceProvider();
        var cache = serviceProvider.GetRequiredService<IRedisCache>();
        var providerInstance = serviceProvider.GetRequiredService<IEasyCachingProviderFactory>()
            .GetCachingProvider(configuration.ProviderName);
        var database = providerInstance.Database as IDatabase ??
            throw new NotSupportedException("EasyCaching Redis Provider 未提供 StackExchange.Redis 数据库。");
        return new ProbeContext(cache, serviceProvider, leaseKey => database.KeyDelete(leaseKey), configuration);
    }

    throw new ArgumentException("Unknown provider. Use freeredis, csredis, or easyredis.", nameof(provider));
}

static async Task<int> RunAsync(ProbeContext context, ProbeOptions options, string defaultKey)
{
    var key = options.Key ?? defaultKey;
    return options.Mode switch
    {
        ProbeMode.Normal => await RunNormalAsync(context, options, key),
        ProbeMode.CrashHolder => await RunCrashHolderAsync(context, options, key),
        ProbeMode.TimeoutHolder => await RunTimeoutHolderAsync(context, options, key),
        ProbeMode.LostLeaseHolder => await RunLostLeaseHolderAsync(context, options, key),
        ProbeMode.WaiterTimeout => await RunWaiterTimeoutAsync(context, options, key),
        _ => throw new ArgumentOutOfRangeException()
    };
}

static async Task<int> RunNormalAsync(ProbeContext context, ProbeOptions options, string key)
{
    try
    {
        var value = await context.Cache.GetAsync<int>(key, async cancellationToken =>
        {
            WriteEvent("FACTORY");
            await Task.Delay(options.FactoryMs, cancellationToken);
            return options.Value;
        });
        WriteEvent($"RESULT={value}");
        return 0;
    }
    catch (Exception exception)
    {
        ReportError(exception);
        return 1;
    }
}

static async Task<int> RunCrashHolderAsync(ProbeContext context, ProbeOptions options, string key)
{
    try
    {
        await context.Cache.GetAsync<int>(key, async _ =>
        {
            WriteEvent("FACTORY");
            await Task.Delay(options.CrashAfterMs);
            WriteEvent($"HOLDER_EXIT={options.ExitCode}");
            Environment.Exit(options.ExitCode);
            return options.Value;
        });
        WriteEvent("UNEXPECTED_RESULT");
        return 1;
    }
    catch (Exception exception)
    {
        ReportError(exception);
        return 1;
    }
}

static async Task<int> RunTimeoutHolderAsync(ProbeContext context, ProbeOptions options, string key)
{
    try
    {
        var value = await context.Cache.GetAsync<int>(key, async _ =>
        {
            WriteEvent("FACTORY");
            // 故意忽略租约回源令牌，让旧回源在超时后仍返回，验证它不会写回。
            await Task.Delay(options.FactoryMs);
            WriteEvent("FACTORY_RETURNED");
            return options.Value;
        });
        WriteEvent($"UNEXPECTED_RESULT={value}");
        return 1;
    }
    catch (Exception exception)
    {
        ReportError(exception);
        if (options.LingerMs > 0)
            await Task.Delay(options.LingerMs);
        WriteEvent($"CACHE_PRESENT={context.Cache.Exists(key)}");
        return 1;
    }
}

static async Task<int> RunLostLeaseHolderAsync(ProbeContext context, ProbeOptions options, string key)
{
    try
    {
        var value = await context.Cache.GetAsync<int>(key, async _ =>
        {
            WriteEvent("FACTORY");
            await Task.Delay(options.DropLeaseAfterMs);
            context.DeleteLease(key);
            WriteEvent("LEASE_DELETED");
            await Task.Delay(options.FactoryMs - options.DropLeaseAfterMs);
            WriteEvent("FACTORY_RETURNED");
            return options.Value;
        });
        WriteEvent($"UNEXPECTED_RESULT={value}");
        return 1;
    }
    catch (Exception exception)
    {
        ReportError(exception);
        WriteEvent($"CACHE_PRESENT={context.Cache.Exists(key)}");
        return 1;
    }
}

static async Task<int> RunWaiterTimeoutAsync(ProbeContext context, ProbeOptions options, string key)
{
    try
    {
        var value = await context.Cache.GetAsync<int>(key, async cancellationToken =>
        {
            WriteEvent("FACTORY");
            await Task.Delay(options.FactoryMs, cancellationToken);
            return options.Value;
        });
        WriteEvent($"UNEXPECTED_RESULT={value}");
        return 1;
    }
    catch (Exception exception)
    {
        ReportError(exception);
        return 1;
    }
}

static void WriteEvent(string value)
{
    Console.WriteLine(value);
    Console.Out.Flush();
}

static void ReportError(Exception exception)
{
    WriteEvent($"ERROR={exception.GetType().Name}");
    WriteEvent($"ERROR_MESSAGE={exception.Message.Replace('\r', ' ').Replace('\n', ' ')}");
}

/// <summary>
/// 定义回源协调跨进程探针的运行模式。
/// </summary>
internal enum ProbeMode
{
    /// <summary>
    /// 执行正常的持有者流程。
    /// </summary>
    Normal,
    /// <summary>
    /// 执行持有租约后崩溃的流程。
    /// </summary>
    CrashHolder,
    /// <summary>
    /// 执行持有租约后超时的流程。
    /// </summary>
    TimeoutHolder,
    /// <summary>
    /// 执行持有租约后丢失所有权的流程。
    /// </summary>
    LostLeaseHolder,
    /// <summary>
    /// 执行等待租约超时的流程。
    /// </summary>
    WaiterTimeout
}

/// <summary>
/// 封装回源协调探针使用的缓存、客户端和租约删除操作。
/// </summary>
internal sealed class ProbeContext : IDisposable
{
    /// <summary>
    /// 保存 Redis 客户端的可释放资源。
    /// </summary>
    private readonly IDisposable _client;
    /// <summary>
    /// 保存删除租约委托。
    /// </summary>
    private readonly Action<string> _deleteLease;

    /// <summary>
    /// 初始化一个 <see cref="ProbeContext"/> 类型的实例。
    /// </summary>
    /// <param name="cache">待测试的缓存实例。</param>
    /// <param name="client">Redis 测试客户端。</param>
    /// <param name="deleteLease">删除缓存租约的回调。</param>
    /// <param name="configuration">缓存键和租约配置。</param>
    internal ProbeContext(ICache cache, IDisposable client, Action<string> deleteLease,
        CacheConfiguration configuration)
    {
        Cache = cache;
        _client = client;
        _deleteLease = deleteLease;
        Configuration = configuration;
    }

    /// <summary>
    /// 获取用于执行探针操作的缓存实例。
    /// </summary>
    internal ICache Cache { get; }

    /// <summary>
    /// 获取缓存键和租约配置。
    /// </summary>
    internal CacheConfiguration Configuration { get; }

    /// <summary>
    /// 释放指定缓存键对应的协调租约。
    /// </summary>
    /// <param name="key">逻辑缓存键。</param>
    internal void DeleteLease(string key) =>
        _deleteLease(CacheKeyFormat.GetLeaseKey(Configuration, key));

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();
}

/// <summary>
/// 保存跨进程回源协调探针的运行参数。
/// </summary>
internal sealed class ProbeOptions
{
    /// <summary>
    /// 初始化一个 <see cref="ProbeOptions"/> 类型的实例。
    /// </summary>
    /// <param name="mode">探针运行模式。</param>
    private ProbeOptions(ProbeMode mode)
    {
        Mode = mode;
        switch (mode)
        {
            case ProbeMode.Normal:
                FactoryMs = 500;
                Value = 42;
                LeaseMs = 30_000;
                RenewMs = 10_000;
                WaitMs = 30_000;
                RetrievalMs = 120_000;
                break;
            case ProbeMode.CrashHolder:
                FactoryMs = 5_000;
                Value = 1;
                LeaseMs = 1_000;
                RenewMs = 200;
                WaitMs = 5_000;
                RetrievalMs = 120_000;
                CrashAfterMs = 500;
                ExitCode = 17;
                UseDefaultCoordination = false;
                break;
            case ProbeMode.TimeoutHolder:
                FactoryMs = 1_000;
                Value = 1;
                LeaseMs = 2_000;
                RenewMs = 500;
                WaitMs = 5_000;
                RetrievalMs = 200;
                LingerMs = 1_200;
                UseDefaultCoordination = false;
                break;
            case ProbeMode.LostLeaseHolder:
                FactoryMs = 800;
                Value = 1;
                LeaseMs = 5_000;
                RenewMs = 1_000;
                WaitMs = 5_000;
                RetrievalMs = 5_000;
                DropLeaseAfterMs = 150;
                UseDefaultCoordination = false;
                break;
            case ProbeMode.WaiterTimeout:
                FactoryMs = 10;
                Value = 2;
                LeaseMs = 5_000;
                RenewMs = 1_000;
                WaitMs = 300;
                RetrievalMs = 5_000;
                UseDefaultCoordination = false;
                break;
        }
    }

    /// <summary>
    /// 获取探针运行模式。
    /// </summary>
    internal ProbeMode Mode { get; }

    /// <summary>
    /// 获取或设置缓存键。
    /// </summary>
    internal string? Key { get; private set; }

    /// <summary>
    /// 获取或设置回源委托返回值。
    /// </summary>
    internal int Value { get; private set; }

    /// <summary>
    /// 获取或设置回源延迟时间，单位为毫秒。
    /// </summary>
    internal int FactoryMs { get; private set; }

    /// <summary>
    /// 获取或设置租约有效期，单位为毫秒。
    /// </summary>
    internal int LeaseMs { get; private set; }

    /// <summary>
    /// 获取或设置租约续期间隔，单位为毫秒。
    /// </summary>
    internal int RenewMs { get; private set; }

    /// <summary>
    /// 获取或设置等待租约的超时时间，单位为毫秒。
    /// </summary>
    internal int WaitMs { get; private set; }

    /// <summary>
    /// 获取或设置回源总超时时间，单位为毫秒。
    /// </summary>
    internal int RetrievalMs { get; private set; }

    /// <summary>
    /// 获取或设置持有者退出前的延迟时间，单位为毫秒。
    /// </summary>
    internal int CrashAfterMs { get; private set; }

    /// <summary>
    /// 获取或设置主动释放租约前的延迟时间，单位为毫秒。
    /// </summary>
    internal int DropLeaseAfterMs { get; private set; }

    /// <summary>
    /// 获取或设置回源完成后保持进程的时长，单位为毫秒。
    /// </summary>
    internal int LingerMs { get; private set; }

    /// <summary>
    /// 获取或设置探针进程的退出代码。
    /// </summary>
    internal int ExitCode { get; private set; } = 17;

    /// <summary>
    /// 获取或设置是否使用默认回源协调配置。
    /// </summary>
    internal bool UseDefaultCoordination { get; private set; } = true;

    /// <summary>
    /// 解析命令行中的探针选项。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>解析得到的探针选项。</returns>
    internal static ProbeOptions Parse(string[] args)
    {
        var mode = args.Length == 4 ? ProbeMode.Normal : ParseMode(args[4]);
        var options = new ProbeOptions(mode);
        var named = ParseNamedOptions(args, args.Length == 4 ? 4 : 5);
        foreach (var item in named)
        {
            switch (item.Key)
            {
                case "key":
                    options.Key = RequireText(item.Key, item.Value);
                    break;
                case "value":
                    options.Value = ParseInt(item.Key, item.Value);
                    break;
                case "factory-ms":
                case "delay-ms":
                    options.FactoryMs = ParseInt(item.Key, item.Value);
                    break;
                case "lease-ms":
                    options.LeaseMs = ParseInt(item.Key, item.Value);
                    options.UseDefaultCoordination = false;
                    break;
                case "renew-ms":
                case "renewal-ms":
                    options.RenewMs = ParseInt(item.Key, item.Value);
                    options.UseDefaultCoordination = false;
                    break;
                case "wait-ms":
                case "wait-timeout-ms":
                    options.WaitMs = ParseInt(item.Key, item.Value);
                    options.UseDefaultCoordination = false;
                    break;
                case "retrieval-ms":
                case "retrieval-timeout-ms":
                    options.RetrievalMs = ParseInt(item.Key, item.Value);
                    options.UseDefaultCoordination = false;
                    break;
                case "crash-after-ms":
                    options.CrashAfterMs = ParseInt(item.Key, item.Value);
                    break;
                case "drop-lease-after-ms":
                case "lose-lease-after-ms":
                    options.DropLeaseAfterMs = ParseInt(item.Key, item.Value);
                    break;
                case "linger-ms":
                    options.LingerMs = ParseInt(item.Key, item.Value);
                    break;
                case "exit-code":
                    options.ExitCode = ParseInt(item.Key, item.Value);
                    break;
                default:
                    throw new ArgumentException($"Unknown probe option: {item.Key}");
            }
        }

        options.Validate();
        return options;
    }

    /// <summary>
    /// 将命令行文本转换为探针运行模式。
    /// </summary>
    /// <param name="value">命令行模式文本。</param>
    /// <returns>返回对应的探针运行模式。</returns>
    private static ProbeMode ParseMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "normal" or "coalesce" => ProbeMode.Normal,
            "crash-holder" or "holder-crash" or "owner-crash" => ProbeMode.CrashHolder,
            "timeout-holder" or "old-holder-timeout" or "expired-holder" => ProbeMode.TimeoutHolder,
            "lost-lease" or "holder-lost-lease" => ProbeMode.LostLeaseHolder,
            "waiter-timeout" or "wait-timeout" => ProbeMode.WaiterTimeout,
            _ => throw new ArgumentException(
                "Unknown mode. Use normal, crash-holder, timeout-holder, lost-lease, or waiter-timeout.")
        };
    }

    /// <summary>
    /// 解析命令行中的命名选项。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="start">起始位置。</param>
    /// <returns>返回选项名称和值的集合。</returns>
    private static IReadOnlyList<KeyValuePair<string, string>> ParseNamedOptions(string[] args, int start)
    {
        var result = new List<KeyValuePair<string, string>>();
        for (var index = start; index < args.Length; index++)
        {
            var token = args[index];
            if (token.StartsWith("--", StringComparison.Ordinal))
                token = token[2..];
            var separator = token.IndexOf('=');
            if (separator >= 0)
            {
                result.Add(new KeyValuePair<string, string>(
                    token[..separator].ToLowerInvariant(), token[(separator + 1)..]));
                continue;
            }

            if (index + 1 >= args.Length)
                throw new ArgumentException($"Probe option '{token}' requires a value.");
            result.Add(new KeyValuePair<string, string>(token.ToLowerInvariant(), args[++index]));
        }

        return result;
    }

    /// <summary>
    /// 验证探针配置的时间参数和退出码。
    /// </summary>
    private void Validate()
    {
        if (FactoryMs < 0 || CrashAfterMs < 0 || DropLeaseAfterMs < 0 || LingerMs < 0)
            throw new ArgumentOutOfRangeException("delay", "Probe delays cannot be negative.");
        if (LeaseMs <= 0 || RenewMs <= 0 || WaitMs <= 0 || RetrievalMs <= 0 || RenewMs >= LeaseMs)
            throw new ArgumentOutOfRangeException("coordination", "Lease and coordination timings are invalid.");
        if (Mode == ProbeMode.TimeoutHolder && FactoryMs <= RetrievalMs)
            throw new ArgumentException("timeout-holder requires factory-ms > retrieval-ms.");
        if (Mode == ProbeMode.LostLeaseHolder && DropLeaseAfterMs >= FactoryMs)
            throw new ArgumentException("lost-lease requires drop-lease-after-ms < factory-ms.");
        if (ExitCode <= 0 || ExitCode > 255)
            throw new ArgumentOutOfRangeException(nameof(ExitCode), "exit-code must be between 1 and 255.");
    }

    /// <summary>
    /// 解析整数选项值。
    /// </summary>
    /// <param name="name">选项名称。</param>
    /// <param name="value">选项文本。</param>
    /// <returns>返回解析后的整数。</returns>
    private static int ParseInt(string name, string value) =>
        int.Parse(RequireText(name, value), NumberStyles.Integer, CultureInfo.InvariantCulture);

    /// <summary>
    /// 验证选项值非空。
    /// </summary>
    /// <param name="name">选项名称。</param>
    /// <param name="value">选项文本。</param>
    /// <returns>返回非空选项值。</returns>
    private static string RequireText(string name, string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"Probe option '{name}' requires a value.") : value;
}
