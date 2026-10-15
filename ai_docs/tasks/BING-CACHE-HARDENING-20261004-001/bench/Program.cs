using Bing.Caching;
using Bing.Caching.CSRedis;
using Bing.Caching.FreeRedis;
using CSRedis;
using FreeRedis;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

if (args.Length is < 4 or > 5)
    throw new ArgumentException("provider endpoint outputPath label [count]");

var providerName = args[0];
var endpoint = args[1];
var outputPath = args[2];
var label = args[3];
var counts = args.Length == 5 ? new[] { int.Parse(args[4]) } : new[] { 1000, 10000 };
var monitorOptions = ConfigurationOptions.Parse(endpoint);
monitorOptions.AllowAdmin = true;
using var monitor = await ConnectionMultiplexer.ConnectAsync(monitorOptions);
var server = monitor.GetServer(monitor.GetEndPoints()[0]);
var results = new List<object>();

long Commands() => long.Parse(server.Info("stats")
    .SelectMany(section => section)
    .First(pair => pair.Key == "total_commands_processed").Value);

static object Distribution(IEnumerable<double> values)
{
    var ordered = values.OrderBy(value => value).ToArray();
    double At(double quantile) => ordered[(int)Math.Ceiling((ordered.Length - 1) * quantile)];
    return new { p50 = At(0.50), p95 = At(0.95), p99 = At(0.99) };
}

foreach (var count in counts)
{
    var writeSamples = new List<double>();
    var readSamples = new List<double>();
    var scanSamples = new List<double>();
    var removeSamples = new List<double>();
    var hitSamples = new List<double>();
    var commandSamples = new List<long>();
    var phaseCommandSamples = new List<Dictionary<string, long>>();
    var factorySamples = new List<int>();
    var allocationSamples = new List<long>();
    var peakBytes = 0L;

    var iterations = count == 1000 ? 10 : 5;
    for (var iteration = 0; iteration < iterations; iteration++)
    {
        var name = "bench-" + Guid.NewGuid().ToString("N").Substring(0, 8);
#if BASELINE
        var prefix = name + ":item:";
        var stampedeKey = name + ":stampede";
#else
        var prefix = "item:";
        var stampedeKey = "stampede";
#endif
        var values = Enumerable.Range(0, count).ToDictionary(index => prefix + index, index => index);
        var keys = values.Keys.ToArray();
        ICache cache;
        IDisposable client;
        if (providerName == "freeredis")
        {
            var redis = new global::FreeRedis.RedisClient(endpoint);
            redis.Serialize = JsonConvert.SerializeObject;
            redis.Deserialize = JsonConvert.DeserializeObject;
#if BASELINE
            cache = new FreeRedisCacheManager(redis);
#else
            cache = new FreeRedisCacheManager(redis, new CacheConfiguration(name, "freeredis"));
#endif
            client = redis;
        }
        else if (providerName == "csredis")
        {
            var redis = new CSRedisClient(endpoint);
#if BASELINE
            RedisHelper.Initialization(redis);
            cache = new CSRedisCacheManager();
#else
            cache = new CSRedisCacheManager(redis, new CacheConfiguration(name, "csredis"));
#endif
            client = redis;
        }
        else throw new ArgumentException("未知 Provider。", nameof(providerName));

        using (client)
        {
            GC.Collect();
            var allocatedBefore = GC.GetTotalAllocatedBytes(true);
            var commandsBefore = Commands();
            var phaseCommands = new Dictionary<string, long>();
            var phaseStart = commandsBefore;
            var watch = Stopwatch.StartNew();
            await cache.SetAllAsync(values);
            writeSamples.Add(watch.Elapsed.TotalMilliseconds);
            phaseCommands["write"] = Commands() - phaseStart - 1;
            phaseStart = Commands();

            for (var hit = 0; hit < 100; hit++)
            {
                watch.Restart();
                if (await cache.GetAsync<int>(keys[0]) != 0) throw new InvalidOperationException("命中值错误。");
                hitSamples.Add(watch.Elapsed.TotalMilliseconds);
            }
            phaseCommands["hit"] = Commands() - phaseStart - 1;
            phaseStart = Commands();

            watch.Restart();
            var read = await cache.GetAllAsync<int>(keys);
            readSamples.Add(watch.Elapsed.TotalMilliseconds);
            if (read.Count != count || read[count - 1] != count - 1)
                throw new InvalidOperationException("批量读取结果错误。");
            phaseCommands["read"] = Commands() - phaseStart - 1;
            phaseStart = Commands();

            watch.Restart();
            var scanned = await cache.GetByPrefixAsync<int>(prefix);
            scanSamples.Add(watch.Elapsed.TotalMilliseconds);
            if (scanned.Count != count) throw new InvalidOperationException("扫描结果错误。");
            phaseCommands["scan"] = Commands() - phaseStart - 1;
            phaseStart = Commands();

            var factories = 0;
            var concurrent = Enumerable.Range(0, 16).Select(_ => cache.GetAsync(stampedeKey, async () =>
            {
                Interlocked.Increment(ref factories);
                await Task.Delay(50);
                return 42;
            }));
            if ((await Task.WhenAll(concurrent)).Any(value => value != 42))
                throw new InvalidOperationException("回源结果错误。");
            factorySamples.Add(factories);
            phaseCommands["stampede"] = Commands() - phaseStart - 1;
            phaseStart = Commands();

            watch.Restart();
            await cache.RemoveAllAsync(keys);
            await cache.RemoveAsync(stampedeKey);
            removeSamples.Add(watch.Elapsed.TotalMilliseconds);
            phaseCommands["remove"] = Commands() - phaseStart - 1;
            commandSamples.Add(phaseCommands.Values.Sum());
            phaseCommandSamples.Add(phaseCommands);
            allocationSamples.Add(GC.GetTotalAllocatedBytes(true) - allocatedBefore);
            peakBytes = Math.Max(peakBytes, Process.GetCurrentProcess().PeakWorkingSet64);
        }
    }

    results.Add(new
    {
        count,
        samples = iterations,
        writeMs = Distribution(writeSamples),
        readMs = Distribution(readSamples),
        scanMs = Distribution(scanSamples),
        removeMs = Distribution(removeSamples),
        hitMs = Distribution(hitSamples),
        redisCommands = commandSamples,
        phaseCommands = phaseCommandSamples,
        factoryCalls = factorySamples,
        allocatedBytes = allocationSamples,
        peakWorkingSetBytes = peakBytes
    });
}

var report = new { label, provider = providerName, runtime = Environment.Version.ToString(),
    endpoint, results };
File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(report,
        new JsonSerializerOptions { WriteIndented = true }),
    new UTF8Encoding(false));
Console.WriteLine(outputPath);
