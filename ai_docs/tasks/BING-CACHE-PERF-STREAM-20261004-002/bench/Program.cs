using Bing.Caching;
#if !BASELINE
using Bing.EasyCaching;
using EasyCaching.Bus.Redis;
using EasyCaching.Redis;
#endif
using Bing.Caching.CSRedis;
using Bing.Caching.FreeRedis;
using CSRedis;
using FreeRedis;
using Newtonsoft.Json;
#if !BASELINE
using Microsoft.Extensions.DependencyInjection;
#endif
using StackExchange.Redis;
using System.Diagnostics;
using System.Text;
using TextJsonSerializer = System.Text.Json.JsonSerializer;

if (args.Length < 4)
    throw new ArgumentException("provider(freeredis|csredis|easyredis|easyhybrid) endpoint outputPath label [--counts=1000,10000,100000] [--warmups=2] [--samples=10] [--requested-concurrency=16] [--scan-noise-count=0]");

var providerName = args[0];
var endpoint = args[1];
var outputPath = args[2];
var label = args[3];
var options = BenchmarkOptions.Parse(args.Skip(4));
var checkpointPath = options.CheckpointPath ?? outputPath + ".checkpoint.json";
var runIdentity = Guid.NewGuid().ToString("N");
var workloadIdentity = BenchmarkCheckpoint.CreateWorkloadIdentity(providerName, endpoint, label, options);
var endpointSummary = BenchmarkCheckpoint.RedactEndpoint(endpoint);
var endpointFingerprint = BenchmarkCheckpoint.CreateEndpointFingerprint(endpoint);
var nodeEndpoints = BenchmarkEndpoints.Parse(endpoint, options);
#if BASELINE
if (options.Topology != "single")
    throw new NotSupportedException("历史基线工程只支持 --topology=single；当前版可运行分片和 Cluster 拓扑。");
#endif
var monitors = await BenchmarkMonitoring.ConnectAsync(nodeEndpoints);
var process = Process.GetCurrentProcess();

long Commands() => monitors.Sum(monitor => monitor.ReadCommands());

var countReports = new List<CountReport>();
var rounds = new List<BenchmarkRoundCheckpoint>();

void WriteCheckpoint(string status, int currentCount, int completedWarmups, int completedSamples,
    CountReportCheckpoint inProgressResult = null)
{
    BenchmarkCheckpoint.Write(checkpointPath, new BenchmarkCheckpoint
    {
        TaskId = "BING-CACHE-PERF-STREAM-20261004-002",
        Label = label,
        Provider = providerName,
        Endpoint = endpointSummary,
        EndpointFingerprint = endpointFingerprint,
        RunIdentity = runIdentity,
        WorkloadIdentity = workloadIdentity,
        DatasetIdentity = workloadIdentity,
        Topology = options.Topology,
        NodeCount = options.NodeCount,
        Status = status,
        CurrentCount = currentCount,
        CompletedWarmups = completedWarmups,
        CompletedSamples = completedSamples,
        ExpectedSamples = options.Samples,
        CompletedResults = countReports,
        InProgressResult = inProgressResult,
        Rounds = rounds.ToArray()
    });
}

WriteCheckpoint("RUNNING", 0, 0, 0);
foreach (var count in options.Counts)
{
    var samples = new List<RoundReport>();
    // 预热只建立连接、序列化和 Redis 客户端内部状态，不进入统计分布。
    for (var warmup = 0; warmup < options.Warmups; warmup++)
    {
        var round = CreateRound(providerName, count, warmup, null, options);
        rounds.Add(round);
        WriteCheckpoint("RUNNING", count, warmup, 0);
        try
        {
            await RunRoundAsync(count, providerName, nodeEndpoints, Commands, monitors.Count, process,
                options, round, false);
            round.Status = "COMPLETED";
        }
        catch
        {
            round.Status = "FAILED";
            WriteCheckpoint("FAILED", count, warmup, 0);
            throw;
        }
        WriteCheckpoint("RUNNING", count, warmup + 1, 0);
    }

    for (var sample = 0; sample < options.Samples; sample++)
    {
        var round = CreateRound(providerName, count, null, sample, options);
        rounds.Add(round);
        WriteCheckpoint("RUNNING", count, options.Warmups, sample,
            new CountReportCheckpoint
            {
                Count = count,
                Warmups = options.Warmups,
                Samples = samples.Count,
                RawSamples = samples
            });
        try
        {
            samples.Add(await RunRoundAsync(count, providerName, nodeEndpoints, Commands, monitors.Count,
                process, options, round, true));
            round.Status = "COMPLETED";
        }
        catch
        {
            round.Status = "FAILED";
            WriteCheckpoint("FAILED", count, options.Warmups, sample,
                new CountReportCheckpoint
                {
                    Count = count,
                    Warmups = options.Warmups,
                    Samples = samples.Count,
                    RawSamples = samples
                });
            throw;
        }
        WriteCheckpoint("RUNNING", count, options.Warmups, sample + 1,
            new CountReportCheckpoint
            {
                Count = count,
                Warmups = options.Warmups,
                Samples = samples.Count,
                RawSamples = samples
            });
    }

    countReports.Add(CountReport.Create(count, options, samples));
    WriteCheckpoint("RUNNING", count, options.Warmups, options.Samples);
}

var report = new BenchmarkReport
{
    TaskId = "BING-CACHE-PERF-STREAM-20261004-002",
    Label = label,
    Provider = providerName,
    Runtime = Environment.Version.ToString(),
    Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
    Endpoint = endpointSummary,
    EndpointFingerprint = endpointFingerprint,
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Configuration = options,
    Topology = options.Topology,
    NodeCount = options.NodeCount,
    MonitoredNodeCount = monitors.Count,
    RunIdentity = runIdentity,
    WorkloadIdentity = workloadIdentity,
    DatasetIdentity = workloadIdentity,
    CheckpointPath = checkpointPath,
    CheckpointStatus = "COMPLETE",
    Rounds = rounds.ToArray(),
#if BASELINE
    StreamingApiStatus = "NOT_APPLICABLE_BASELINE_API_MISSING",
#else
    StreamingApiStatus = "MEASURED_WHEN_RUN",
#endif
    Results = countReports
};

var json = TextJsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
});
File.WriteAllText(outputPath, json, new UTF8Encoding(false));
WriteCheckpoint("COMPLETE", options.Counts.Length == 0 ? 0 : options.Counts[^1], options.Warmups,
    options.Samples, null);
Console.WriteLine(outputPath);
foreach (var monitor in monitors)
    monitor.Dispose();

/// <summary>
/// 创建一轮基准测试的检查点。
/// </summary>
/// <param name="providerName">缓存 Provider 名称。</param>
/// <param name="count">本轮处理的数据项数量。</param>
/// <param name="warmupIndex">预热轮次索引；采样轮次时为 <see langword="null" />。</param>
/// <param name="sampleIndex">采样轮次索引；预热轮次时为 <see langword="null" />。</param>
/// <param name="options">基准测试选项。</param>
/// <returns>包含本轮标识、命名空间和清理状态的检查点。</returns>
static BenchmarkRoundCheckpoint CreateRound(string providerName, int count, int? warmupIndex,
    int? sampleIndex, BenchmarkOptions options)
{
    var name = "bench-" + Guid.NewGuid().ToString("N").Substring(0, 12);
#if BASELINE
    var logicalPrefix = name + ":item:";
    var physicalPattern = name + "*";
    var noisePrefix = name + ":noise:";
#else
    var logicalPrefix = "item:";
    var physicalPattern = CacheKeyFormat.GetNamespacePattern(CreateConfiguration(name, providerName, options));
    var noisePrefix = "noise:";
#endif
    return new BenchmarkRoundCheckpoint
    {
        RoundIdentity = Guid.NewGuid().ToString("N"),
        Count = count,
        WarmupIndex = warmupIndex,
        SampleIndex = sampleIndex,
        Stage = warmupIndex.HasValue ? "WARMUP" : "SAMPLE",
        Namespace = name,
        LogicalPrefix = logicalPrefix,
        NoiseLogicalPrefix = noisePrefix,
        NoiseCount = options.ScanNoiseCount,
        PhysicalPattern = physicalPattern,
        Status = "RUNNING",
        CleanupStatus = "PENDING"
    };
}

/// <summary>
/// 执行一轮基准测试并返回测量结果。
/// </summary>
/// <param name="count">本轮处理的数据项数量。</param>
/// <param name="providerName">缓存 Provider 名称。</param>
/// <param name="nodeEndpoints">用于连接和监控的 Redis 端点。</param>
/// <param name="commands">读取已处理 Redis 命令数的委托。</param>
/// <param name="monitorCount">监控连接数量，用于扣除监控自身产生的命令。</param>
/// <param name="process">用于读取进程工作集的进程对象。</param>
/// <param name="options">基准测试选项。</param>
/// <param name="round">当前轮次的检查点。</param>
/// <param name="capture">是否将本轮作为正式采样结果保存。</param>
/// <returns>完成本轮各阶段测量后的结果。</returns>
static async Task<RoundReport> RunRoundAsync(int count, string providerName, IReadOnlyList<string> nodeEndpoints,
    Func<long> commands, int monitorCount, Process process, BenchmarkOptions options,
    BenchmarkRoundCheckpoint round, bool capture)
{
    var name = round.Namespace;
#if BASELINE
    var prefix = name + ":item:";
    var stampedeKey = name + ":stampede";
    var noisePrefix = name + ":noise:";
#else
    var prefix = "item:";
    var stampedeKey = "stampede";
    var noisePrefix = "noise:";
#endif
    var values = Enumerable.Range(0, count).ToDictionary(index => prefix + index, index => index);
    var keys = values.Keys.ToArray();
    var noiseValues = options.ScanNoiseCount == 0
        ? null
        : Enumerable.Range(0, options.ScanNoiseCount)
            .ToDictionary(index => noisePrefix + index, index => index);
    var clients = CreateClients(providerName, nodeEndpoints, options, name);
    var cache = clients.Cache;

    using (clients)
    {
        var phases = new Dictionary<string, PhaseReport>(StringComparer.Ordinal);
        var hitLatency = new List<double>();
        try
        {
            // 噪声键只用于构造稀疏扫描数据集，准备过程不计入正式写入阶段。
            if (noiseValues != null)
                await cache.SetAllAsync(noiseValues);
            phases["write"] = await MeasureAsync("write", options, commands, monitorCount,
                () => cache.SetAllAsync(values));

#if !BASELINE
            if (options.AllocationComponents)
            {
                if (options.Topology != "single" || clients.RawFetch == null || clients.DecodeRaw == null)
                    throw new NotSupportedException("分配归因仅支持单节点 FreeRedis/CSRedis。");
                var configuration = CreateConfiguration(name, providerName, options);
                var physicalKeys = CacheKeyFormat.GetPhysicalKeys(configuration, keys);
                var rawValues = await clients.RawFetch(physicalKeys);
                phases["keyFormat"] = await MeasureAsync("keyFormat", options, commands, monitorCount,
                    () =>
                    {
                        GC.KeepAlive(CacheKeyFormat.GetPhysicalKeys(configuration, keys));
                        return Task.CompletedTask;
                    });
                phases["rawMGet"] = await MeasureAsync("rawMGet", options, commands, monitorCount,
                    async () => GC.KeepAlive(await clients.RawFetch(physicalKeys)));
                phases["typedMGet"] = await MeasureAsync("typedMGet", options, commands, monitorCount,
                    async () => GC.KeepAlive(await clients.TypedFetch(physicalKeys)));
                phases["decodeRaw"] = await MeasureAsync("decodeRaw", options, commands, monitorCount,
                    () =>
                    {
                        clients.DecodeRaw(rawValues);
                        return Task.CompletedTask;
                    });
                phases["decodeJsonBatch"] = await MeasureAsync("decodeJsonBatch", options,
                    commands, monitorCount, () =>
                    {
                        GC.KeepAlive(DecodeJsonBatch(rawValues, keys.Length));
                        return Task.CompletedTask;
                    });
                phases["keyParse"] = await MeasureAsync("keyParse", options, commands, monitorCount,
                    () =>
                    {
                        foreach (var physicalKey in physicalKeys)
                            if (!CacheKeyFormat.TryGetLogicalKey(configuration, physicalKey, out _))
                                throw new InvalidOperationException("物理键校验失败。");
                        return Task.CompletedTask;
                    });
            }
#endif

            var hit = await MeasureHitsAsync(cache, keys[0], options.HitReads, commands, monitorCount, options);
            phases["hit"] = hit.Phase;
            hitLatency.AddRange(hit.RequestLatencyMs);

            phases["batchRead"] = await MeasureAsync("batchRead", options, commands, monitorCount, async () =>
            {
                var read = await cache.GetAllAsync<int>(keys);
                if (read.Count != count || read[count - 1] != count - 1)
                    throw new InvalidOperationException("批量读取结果错误。");
            });

#if !BASELINE
            phases["batchReadStream"] = await MeasureAsync("batchReadStream", options, commands, monitorCount, async () =>
            {
                var read = 0;
                CacheEntry<int> last = default;
                await foreach (var entry in cache.GetAllStreamAsync<int>(keys))
                {
                    last = entry;
                    read++;
                }
                if (read != count || !last.Hit || last.Value != count - 1)
                    throw new InvalidOperationException("流式批量读取结果错误。");
            });
#endif

            phases["scan"] = await MeasureAsync("scan", options, commands, monitorCount, async () =>
            {
                var scanned = await cache.GetByPrefixAsync<int>(prefix);
                if (scanned.Count != count)
                    throw new InvalidOperationException("前缀扫描结果错误。");
            });

#if !BASELINE
            phases["scanStream"] = await MeasureAsync("scanStream", options, commands, monitorCount, async () =>
            {
                var scanned = 0;
                await foreach (var entry in cache.GetByPrefixStreamAsync<int>(prefix))
                {
                    if (!entry.Hit)
                        throw new InvalidOperationException("流式前缀扫描出现未命中项。");
                    scanned++;
                }
                if (scanned != count)
                    throw new InvalidOperationException("流式前缀扫描结果错误。");
            });
#endif

            var stampede = await MeasureStampedeAsync(cache, stampedeKey, options, commands, monitorCount);
            phases["stampede"] = stampede.Phase;

            phases["remove"] = await MeasureAsync("remove", options, commands, monitorCount, async () =>
            {
                await cache.RemoveAllAsync(keys);
                await cache.RemoveAsync(stampedeKey);
            });

            return new RoundReport
            {
                Phases = phases,
                HitRequestLatencyMs = hitLatency,
                PeakWorkingSetBytes = process.PeakWorkingSet64,
                WorkingSetBytes = process.WorkingSet64,
                Captured = capture
            };
        }
        finally
        {
            // 清理失败必须保留为该轮异常，不能静默伪造完整样本；正常路径的删除已在上面计时。
            try
            {
                await cache.ClearAsync();
                round.CleanupStatus = "CLEANED";
            }
            catch
            {
                round.CleanupStatus = "FAILED";
                throw;
            }
        }
    }
}

/// <summary>
/// 测量一个异步基准阶段并收集资源指标。
/// </summary>
/// <param name="name">阶段名称。</param>
/// <param name="options">基准测试选项。</param>
/// <param name="commands">读取已处理 Redis 命令数的委托。</param>
/// <param name="monitorCount">监控连接数量，用于扣除监控自身产生的命令。</param>
/// <param name="operation">要测量的异步操作。</param>
/// <returns>包含耗时、分配量、命令数和并发指标的阶段结果。</returns>
static async Task<PhaseReport> MeasureAsync(string name, BenchmarkOptions options,
    Func<long> commands, int monitorCount, Func<Task> operation)
{
    GC.Collect();
    var allocatedBefore = GC.GetTotalAllocatedBytes(true);
    var commandBefore = commands();
#if !BASELINE
    using var probe = options.CommandProbe ? CacheCommandProbe.Begin() : null;
#endif
    using var memory = options.ResourceProbe ? new WorkingSetProbe() : null;
    var watch = Stopwatch.StartNew();
    await operation();
    watch.Stop();
    if (memory != null) await memory.FinishAsync();
    var commandCount = Math.Max(0, commands() - commandBefore - monitorCount);
    var allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
#if !BASELINE
    var commandBreakdown = probe?.GetBreakdown() ?? new Dictionary<string, long>(StringComparer.Ordinal);
    var commandMaximumParallelism = probe?.GetMaximumBreakdown() ??
        new Dictionary<string, int>(StringComparer.Ordinal);
    var scanPages = probe?.ScanPages ?? -1;
    var scanReturnedItems = probe?.ScanItems ?? -1;
#else
    var commandBreakdown = new Dictionary<string, long>(StringComparer.Ordinal);
    var commandMaximumParallelism = new Dictionary<string, int>(StringComparer.Ordinal);
    var scanPages = -1L;
    var scanReturnedItems = -1L;
#endif
    return new PhaseReport
    {
        Name = name,
        ElapsedMs = watch.Elapsed.TotalMilliseconds,
        AllocatedBytes = allocated,
        RedisCommands = commandCount,
        CommandBreakdown = commandBreakdown,
        CommandMaximumParallelism = commandMaximumParallelism,
        ScanPages = scanPages,
        ScanReturnedItems = scanReturnedItems,
        NetworkRequests = -1,
        SampledPhasePeakWorkingSetBytes = memory?.Maximum ?? -1,
        RequestedConcurrency = 1,
        MaxActualParallelism =
#if !BASELINE
            probe != null && probe.Calls > 0 ? probe.Maximum :
#endif
            -1,
        NativeClientCalls =
#if !BASELINE
            probe != null && probe.Calls > 0 ? probe.Calls :
#endif
            -1,
        MaxQueuedRequests = -1,
        RetrieverCalls = 0,
        ConcurrencyMeasurement =
#if !BASELINE
            probe != null && probe.Calls > 0 ? "INSTRUMENTED_LOGICAL_CACHE_CALLS" :
#endif
            "API_CALL_ONLY_INTERNAL_PROVIDER_UNAVAILABLE"
    };
}

/// <summary>
/// 测量连续命中读取并记录单次请求延迟。
/// </summary>
/// <param name="cache">待测缓存实例。</param>
/// <param name="key">重复读取的逻辑键。</param>
/// <param name="requests">读取请求次数。</param>
/// <param name="commands">读取已处理 Redis 命令数的委托。</param>
/// <param name="monitorCount">监控连接数量，用于扣除监控自身产生的命令。</param>
/// <param name="options">基准测试选项。</param>
/// <returns>命中阶段结果及每次读取的延迟毫秒数。</returns>
static async Task<(PhaseReport Phase, IReadOnlyList<double> RequestLatencyMs)> MeasureHitsAsync(
    ICache cache, string key, int requests, Func<long> commands, int monitorCount, BenchmarkOptions options)
{
    GC.Collect();
    var allocatedBefore = GC.GetTotalAllocatedBytes(true);
    var commandBefore = commands();
#if !BASELINE
    using var probe = options.CommandProbe ? CacheCommandProbe.Begin() : null;
#endif
    var phaseWatch = Stopwatch.StartNew();
    var latencies = new List<double>(requests);
    for (var index = 0; index < requests; index++)
    {
        var watch = Stopwatch.StartNew();
        var value = await cache.GetAsync<int>(key);
        watch.Stop();
        if (value != 0)
            throw new InvalidOperationException("命中值错误。");
        latencies.Add(watch.Elapsed.TotalMilliseconds);
    }
    phaseWatch.Stop();
#if !BASELINE
    var commandBreakdown = probe?.GetBreakdown() ?? new Dictionary<string, long>(StringComparer.Ordinal);
    var commandMaximumParallelism = probe?.GetMaximumBreakdown() ??
        new Dictionary<string, int>(StringComparer.Ordinal);
    var scanPages = probe?.ScanPages ?? -1;
    var scanReturnedItems = probe?.ScanItems ?? -1;
#else
    var commandBreakdown = new Dictionary<string, long>(StringComparer.Ordinal);
    var commandMaximumParallelism = new Dictionary<string, int>(StringComparer.Ordinal);
    var scanPages = -1L;
    var scanReturnedItems = -1L;
#endif
    return (new PhaseReport
    {
        Name = "hit",
        ElapsedMs = phaseWatch.Elapsed.TotalMilliseconds,
        AllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocatedBefore,
        RedisCommands = Math.Max(0, commands() - commandBefore - monitorCount),
        CommandBreakdown = commandBreakdown,
        CommandMaximumParallelism = commandMaximumParallelism,
        ScanPages = scanPages,
        ScanReturnedItems = scanReturnedItems,
        NativeClientCalls =
#if !BASELINE
            probe != null && probe.Calls > 0 ? probe.Calls :
#endif
            -1,
        NetworkRequests = -1,
        RequestedConcurrency = 1,
        MaxActualParallelism =
#if !BASELINE
            probe != null && probe.Calls > 0 ? probe.Maximum :
#endif
            -1,
        MaxQueuedRequests = -1,
        RetrieverCalls = 0,
        ConcurrencyMeasurement =
#if !BASELINE
            probe != null && probe.Calls > 0 ? "INSTRUMENTED_SINGLE_READ_CALLS" :
#endif
            "API_CALL_ONLY_INTERNAL_PROVIDER_UNAVAILABLE"
    }, latencies);
}

/// <summary>
/// 测量并发回源场景下的请求合并和回源次数。
/// </summary>
/// <param name="cache">待测缓存实例。</param>
/// <param name="key">并发读取的逻辑键。</param>
/// <param name="options">基准测试选项。</param>
/// <param name="commands">读取已处理 Redis 命令数的委托。</param>
/// <param name="monitorCount">监控连接数量，用于扣除监控自身产生的命令。</param>
/// <returns>回源阶段结果及实际执行的回源次数。</returns>
static async Task<(PhaseReport Phase, int RetrieverCalls)> MeasureStampedeAsync(
    ICache cache, string key, BenchmarkOptions options, Func<long> commands, int monitorCount)
{
    GC.Collect();
    var allocatedBefore = GC.GetTotalAllocatedBytes(true);
    var commandBefore = commands();
#if !BASELINE
    using var probe = options.CommandProbe ? CacheCommandProbe.Begin() : null;
#endif
    var activeRequests = 0;
    var maxQueuedRequests = 0;
    var activeFactories = 0;
    var maxActualParallelism = 0;
    var factories = 0;
    var watch = Stopwatch.StartNew();
    var tasks = Enumerable.Range(0, options.RequestedConcurrency).Select(async _ =>
    {
        var active = Interlocked.Increment(ref activeRequests);
        var activeFactoryCount = Volatile.Read(ref activeFactories);
        UpdateMaximum(ref maxQueuedRequests, Math.Max(0, active - activeFactoryCount));
        try
        {
            var value = await cache.GetAsync(key, async () =>
            {
                Interlocked.Increment(ref factories);
                var factoryActive = Interlocked.Increment(ref activeFactories);
                UpdateMaximum(ref maxActualParallelism, factoryActive);
                try
                {
                    await Task.Delay(options.RetrieverDelayMs);
                    return 42;
                }
                finally
                {
                    Interlocked.Decrement(ref activeFactories);
                }
            });
            if (value != 42)
                throw new InvalidOperationException("回源结果错误。");
        }
        finally
        {
            Interlocked.Decrement(ref activeRequests);
        }
    });
    await Task.WhenAll(tasks);
    watch.Stop();
#if !BASELINE
    var commandBreakdown = probe?.GetBreakdown() ?? new Dictionary<string, long>(StringComparer.Ordinal);
    var commandMaximumParallelism = probe?.GetMaximumBreakdown() ??
        new Dictionary<string, int>(StringComparer.Ordinal);
    var nativeClientCalls = probe != null && probe.Calls > 0 ? probe.Calls : -1;
    var scanPages = probe?.ScanPages ?? -1;
    var scanReturnedItems = probe?.ScanItems ?? -1;
#else
    var commandBreakdown = new Dictionary<string, long>(StringComparer.Ordinal);
    var commandMaximumParallelism = new Dictionary<string, int>(StringComparer.Ordinal);
    var nativeClientCalls = -1L;
    var scanPages = -1L;
    var scanReturnedItems = -1L;
#endif
    return (new PhaseReport
    {
        Name = "stampede",
        ElapsedMs = watch.Elapsed.TotalMilliseconds,
        AllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocatedBefore,
        RedisCommands = Math.Max(0, commands() - commandBefore - monitorCount),
        NativeClientCalls = nativeClientCalls,
        CommandBreakdown = commandBreakdown,
        CommandMaximumParallelism = commandMaximumParallelism,
        ScanPages = scanPages,
        ScanReturnedItems = scanReturnedItems,
        NetworkRequests = -1,
        RequestedConcurrency = options.RequestedConcurrency,
        MaxActualParallelism = maxActualParallelism,
        MaxQueuedRequests = maxQueuedRequests,
        RetrieverCalls = factories,
        ConcurrencyMeasurement = "REQUEST_AND_FACTORY_OBSERVED"
    }, factories);
}

/// <summary>
/// 以原子方式更新并发度指标的最大值。
/// </summary>
/// <param name="target">要更新的最大值。</param>
/// <param name="value">待比较的新值。</param>
static void UpdateMaximum(ref int target, int value)
{
    while (true)
    {
        var current = Volatile.Read(ref target);
        if (current >= value || Interlocked.CompareExchange(ref target, value, current) == current)
            return;
    }
}

/// <summary>
/// 按 Provider 和拓扑创建基准测试客户端集合。
/// </summary>
/// <param name="providerName">缓存 Provider 名称。</param>
/// <param name="nodeEndpoints">Redis 节点端点。</param>
/// <param name="options">基准测试选项。</param>
/// <param name="name">本轮测试命名空间。</param>
/// <returns>缓存实例及其需要释放的底层资源。</returns>
static BenchmarkClientSet CreateClients(string providerName, IReadOnlyList<string> nodeEndpoints,
    BenchmarkOptions options, string name)
{
#if BASELINE
    if (providerName.Equals("easyredis", StringComparison.OrdinalIgnoreCase) ||
        providerName.Equals("easyhybrid", StringComparison.OrdinalIgnoreCase))
        throw new NotSupportedException("历史基线没有等价的 EasyCaching Redis/Hybrid benchmark 路径。");
#endif

#if !BASELINE
    if (providerName.Equals("easyredis", StringComparison.OrdinalIgnoreCase) ||
        providerName.Equals("easyhybrid", StringComparison.OrdinalIgnoreCase))
        return CreateEasyCachingClient(providerName, nodeEndpoints, options, name);
#endif

    if (providerName.Equals("freeredis", StringComparison.OrdinalIgnoreCase))
    {
        var resources = new List<IDisposable>();
        global::FreeRedis.RedisClient router;
        IReadOnlyList<global::FreeRedis.RedisClient> scanClients;

        if (options.Topology == "single")
        {
            router = CreateFreeRedisClient(nodeEndpoints[0]);
            scanClients = new[] { router };
            resources.Add(router);
        }
        else if (options.Topology == "sharded")
        {
            var nodes = nodeEndpoints.Select(CreateFreeRedisClient).ToArray();
            router = nodes[0];
            scanClients = nodes;
            resources.AddRange(nodes);
        }
        else
        {
            router = new global::FreeRedis.RedisClient(nodeEndpoints
                .Select(value => (ConnectionStringBuilder)value).ToArray());
            ConfigureFreeRedis(router);
            var masters = nodeEndpoints.Select(CreateFreeRedisClient).ToArray();
            scanClients = masters;
            resources.Add(router);
            resources.AddRange(masters);
        }

#if BASELINE
        if (options.Topology != "single")
            throw new NotSupportedException("历史基线工程只支持单节点 FreeRedis。 ");
        var cache = new FreeRedisCacheManager(router);
#else
        var cache = new FreeRedisCacheManager(router,
            CreateConfiguration(name, "freeredis", options), scanClients);
#endif
#if BASELINE
        return new BenchmarkClientSet(cache, resources);
#else
        return new BenchmarkClientSet(cache, resources,
            async request => await router.MGetAsync(request).ConfigureAwait(false),
            raw =>
            {
                foreach (var value in (string[])raw)
                    if (value != null)
                        GC.KeepAlive(router.Deserialize(value, typeof(int)));
            },
            async request => await router.MGetAsync<int>(request).ConfigureAwait(false));
#endif
    }

    if (providerName.Equals("csredis", StringComparison.OrdinalIgnoreCase))
    {
        var resources = new List<IDisposable>();
        CSRedisClient router;
        IReadOnlyList<CSRedisClient> scanClients;

        if (options.Topology == "single")
        {
            router = new CSRedisClient(nodeEndpoints[0]);
            scanClients = new[] { router };
            resources.Add(router);
        }
        else if (options.Topology == "sharded")
        {
            router = new CSRedisClient(null, nodeEndpoints.ToArray());
            var nodes = nodeEndpoints.Select(value => new CSRedisClient(value)).ToArray();
            scanClients = nodes;
            resources.Add(router);
            resources.AddRange(nodes);
        }
        else
        {
            // CSRedis discovers the remaining Cluster masters from the seed node.
            router = new CSRedisClient(nodeEndpoints[0]);
            scanClients = new[] { router };
            resources.Add(router);
        }

#if BASELINE
        if (options.Topology != "single")
            throw new NotSupportedException("历史基线工程只支持单节点 CSRedis。 ");
        RedisHelper.Initialization(router);
        var cache = new CSRedisCacheManager();
#else
        var cache = new CSRedisCacheManager(router,
            CreateConfiguration(name, "csredis", options), scanClients);
#endif
#if BASELINE
        return new BenchmarkClientSet(cache, resources);
#else
        return new BenchmarkClientSet(cache, resources,
            async request => await router.MGetAsync<byte[]>(request).ConfigureAwait(false),
            raw =>
            {
                foreach (var value in (byte[][])raw)
                    if (value != null)
                        GC.KeepAlive(JsonConvert.DeserializeObject<int>(Encoding.UTF8.GetString(value)));
            },
            async request => await router.MGetAsync<int>(request).ConfigureAwait(false));
#endif
    }

    throw new ArgumentException("未知 Provider。", nameof(providerName));
}

#if !BASELINE
/// <summary>
/// 将原始批量值组装并解析为整数数组。
/// </summary>
/// <param name="rawValues">来自 Redis 客户端的字符串或字节数组。</param>
/// <param name="count">期望解析的元素数量。</param>
/// <returns>按输入顺序解析出的整数数组。</returns>
static int[] DecodeJsonBatch(object rawValues, int count)
{
    var json = new StringBuilder(count * 5).Append('[');
    for (var index = 0; index < count; index++)
    {
        if (index > 0)
            json.Append(',');
        var value = rawValues is string[] strings ? strings[index] :
            Encoding.UTF8.GetString(((byte[][])rawValues)[index]);
        json.Append(value ?? "null");
    }
    var values = JsonConvert.DeserializeObject<int[]>(json.Append(']').ToString());
    if (values == null || values.Length != count || values[count - 1] != count - 1)
        throw new InvalidOperationException("批量 JSON 诊断结果错误。");
    return values;
}

/// <summary>
/// 创建 EasyCaching Redis 或 Hybrid 基准客户端。
/// </summary>
/// <param name="providerName">EasyCaching Provider 名称。</param>
/// <param name="nodeEndpoints">Redis 节点端点。</param>
/// <param name="options">基准测试选项。</param>
/// <param name="name">本轮测试命名空间。</param>
/// <returns>包含 EasyCaching 实例和服务提供程序资源的客户端集合。</returns>
static BenchmarkClientSet CreateEasyCachingClient(string providerName,
    IReadOnlyList<string> nodeEndpoints, BenchmarkOptions options, string name)
{
    var configuration = new CacheConfiguration(name, providerName,
        maxConcurrency: options.RequestedConcurrency,
        topology: options.Topology switch
        {
            "sharded" => CacheTopology.ClientSharded,
            "cluster" => CacheTopology.RedisCluster,
            _ => CacheTopology.SingleNode
        },
        expectedShardCount: options.Topology == "sharded" ? options.NodeCount : null);
    var services = new ServiceCollection();
    services.AddLogging();
    if (options.Topology == "sharded")
    {
        var shardConfigurations = nodeEndpoints
            .Select(endpoint => new Action<RedisOptions>(redis =>
                redis.DBConfig.ConfigurationOptions = CreateRedisConfiguration(new[] { endpoint })))
            .ToArray();
        if (providerName.Equals("easyredis", StringComparison.OrdinalIgnoreCase))
        {
            services.AddShardedRedisCaching(configuration, shardConfigurations);
        }
        else
        {
            // 客户端分片的 Hybrid 总线必须使用一个明确、所有实例都能访问的 Redis 端点。
            // 基准沿用集成测试约定，将首个分片作为总线端点；它不是分片故障转移地址。
            var busEndpoint = nodeEndpoints[0];
            services.AddShardedHybridCaching(configuration, shardConfigurations,
                bus => bus.ConfigurationOptions = CreateRedisConfiguration(new[] { busEndpoint }));
        }
    }
    else
    {
        var redisConfiguration = CreateRedisConfiguration(nodeEndpoints);
        if (providerName.Equals("easyredis", StringComparison.OrdinalIgnoreCase))
        {
            services.AddRedisCaching(configuration,
                redis => redis.DBConfig.ConfigurationOptions = redisConfiguration);
        }
        else
        {
            var distributedProviderName = name + ":distributed";
            var busConfiguration = CreateRedisConfiguration(nodeEndpoints);
            services.AddHybridCaching(configuration, distributedProviderName,
                redis => redis.DBConfig.ConfigurationOptions = redisConfiguration,
                bus => bus.ConfigurationOptions = busConfiguration);
        }
    }

    var serviceProvider = services.BuildServiceProvider();
    var cache = serviceProvider.GetRequiredService<ICache>();
    return new BenchmarkClientSet(cache, new[] { serviceProvider });
}

/// <summary>
/// 创建允许连接失败后继续重试的 Redis 配置。
/// </summary>
/// <param name="endpoints">Redis 端点列表。</param>
/// <returns>配置好的 Redis 连接选项。</returns>
static ConfigurationOptions CreateRedisConfiguration(IReadOnlyList<string> endpoints)
{
    if (endpoints == null || endpoints.Count == 0)
        throw new ArgumentException("至少需要一个 Redis 端点。", nameof(endpoints));

    var configuration = ConfigurationOptions.Parse(string.Join(",", endpoints));
    configuration.AbortOnConnectFail = false;
    return configuration;
}
#endif

#if !BASELINE
/// <summary>
/// 根据基准选项创建缓存配置。
/// </summary>
/// <param name="name">缓存命名空间。</param>
/// <param name="provider">缓存 Provider 名称。</param>
/// <param name="options">基准测试选项。</param>
/// <returns>与测试拓扑和并发度匹配的缓存配置。</returns>
static CacheConfiguration CreateConfiguration(string name, string provider, BenchmarkOptions options)
{
    var topology = options.Topology switch
    {
        "sharded" => CacheTopology.ClientSharded,
        "cluster" => CacheTopology.RedisCluster,
        _ => CacheTopology.SingleNode
    };
    return new CacheConfiguration(name, provider, maxConcurrency: options.RequestedConcurrency,
        topology: topology,
        expectedShardCount: topology == CacheTopology.ClientSharded ? options.NodeCount : null);
}
#endif

/// <summary>
/// 创建并配置 FreeRedis 客户端。
/// </summary>
/// <param name="endpoint">Redis 连接端点。</param>
/// <returns>已设置 JSON 序列化委托的 FreeRedis 客户端。</returns>
static global::FreeRedis.RedisClient CreateFreeRedisClient(string endpoint)
{
    var client = new global::FreeRedis.RedisClient(endpoint);
    ConfigureFreeRedis(client);
    return client;
}

/// <summary>
/// 为 FreeRedis 客户端设置 JSON 序列化委托。
/// </summary>
/// <param name="client">待配置的 FreeRedis 客户端。</param>
static void ConfigureFreeRedis(global::FreeRedis.RedisClient client)
{
    client.Serialize = JsonConvert.SerializeObject;
    client.Deserialize = JsonConvert.DeserializeObject;
}

/// <summary>
/// 封装基准测试使用的缓存实例和底层资源。
/// </summary>
sealed class BenchmarkClientSet : IDisposable
{
    /// <summary>
    /// 获取需要按逆序释放的底层资源。
    /// </summary>
    private readonly IReadOnlyList<IDisposable> _resources;

    /// <summary>
    /// 初始化 BenchmarkClientSet 类的新实例。
    /// </summary>
    /// <param name="cache">待测缓存实例。</param>
    /// <param name="resources">需要由集合释放的底层资源。</param>
    /// <param name="rawFetch">可选的原始值批量读取委托。</param>
    /// <param name="decodeRaw">可选的原始值解码委托。</param>
    /// <param name="typedFetch">可选的强类型批量读取委托。</param>
    public BenchmarkClientSet(ICache cache, IReadOnlyList<IDisposable> resources,
        Func<string[], Task<object>> rawFetch = null, Action<object> decodeRaw = null,
        Func<string[], Task<object>> typedFetch = null)
    {
        Cache = cache;
        _resources = resources;
        RawFetch = rawFetch;
        DecodeRaw = decodeRaw;
        TypedFetch = typedFetch;
    }

    /// <summary>
    /// 获取待测缓存实例。
    /// </summary>
    public ICache Cache { get; }

    /// <summary>
    /// 获取原始值批量读取委托。
    /// </summary>
    public Func<string[], Task<object>> RawFetch { get; }

    /// <summary>
    /// 获取原始值解码委托。
    /// </summary>
    public Action<object> DecodeRaw { get; }

    /// <summary>
    /// 获取强类型批量读取委托。
    /// </summary>
    public Func<string[], Task<object>> TypedFetch { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        for (var index = _resources.Count - 1; index >= 0; index--)
            _resources[index].Dispose();
    }
}

/// <summary>
/// 解析并校验基准测试使用的 Redis 端点。
/// </summary>
static class BenchmarkEndpoints
{
    /// <summary>
    /// 按拓扑要求解析 Redis 端点列表。
    /// </summary>
    /// <param name="endpoint">以分号分隔的 Redis 端点字符串。</param>
    /// <param name="options">包含拓扑和节点数量约束的基准选项。</param>
    /// <returns>去除空白后的端点列表。</returns>
    public static IReadOnlyList<string> Parse(string endpoint, BenchmarkOptions options)
    {
        var endpoints = endpoint.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();
        if (endpoints.Length == 0)
            throw new ArgumentException("至少需要一个 Redis 端点。", nameof(endpoint));
        if (options.Topology == "single")
        {
            if (options.NodeCount != 1 || endpoints.Length != 1)
                throw new ArgumentException("single 拓扑必须使用一个端点并设置 --nodes=1。", nameof(endpoint));
        }
        else if (endpoints.Length != options.NodeCount)
        {
            throw new ArgumentException(
                $"{options.Topology} 拓扑需要通过分号提供 {options.NodeCount} 个端点，实际收到 {endpoints.Length} 个。",
                nameof(endpoint));
        }
        return endpoints;
    }
}

/// <summary>
/// 读取 Redis 服务端命令计数的监控器。
/// </summary>
sealed class BenchmarkMonitor : IDisposable
{
    /// <summary>
    /// 获取用于查询监控数据的 Redis 连接。
    /// </summary>
    private readonly ConnectionMultiplexer _connection;
    /// <summary>
    /// 获取用于读取服务端信息的 Redis 服务器对象。
    /// </summary>
    private readonly StackExchange.Redis.IServer _server;

    /// <summary>
    /// 初始化 BenchmarkMonitor 类的新实例。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <param name="server">Redis 服务器对象。</param>
    private BenchmarkMonitor(ConnectionMultiplexer connection, StackExchange.Redis.IServer server)
    {
        _connection = connection;
        _server = server;
    }

    /// <summary>
    /// 连接 Redis 并创建命令计数监控器。
    /// </summary>
    /// <param name="endpoint">Redis 连接端点。</param>
    /// <returns>已连接的监控器。</returns>
    public static async Task<BenchmarkMonitor> ConnectAsync(string endpoint)
    {
        var options = ConfigurationOptions.Parse(endpoint);
        options.AllowAdmin = true;
        var connection = await ConnectionMultiplexer.ConnectAsync(options);
        var server = connection.GetServer(connection.GetEndPoints()[0]);
        return new BenchmarkMonitor(connection, server);
    }

    /// <summary>
    /// 读取 Redis 已处理命令总数。
    /// </summary>
    /// <returns>当前服务端报告的命令总数；无法解析时返回零。</returns>
    public long ReadCommands()
    {
        var stats = _server.Info("stats").SelectMany(section => section);
        var value = stats.FirstOrDefault(pair => pair.Key == "total_commands_processed").Value;
        return long.TryParse(value, out var commands) ? commands : 0;
    }

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// 并行连接多个 Redis 端点并创建监控器。
/// </summary>
static class BenchmarkMonitoring
{
    /// <summary>
    /// 为所有端点建立监控连接。
    /// </summary>
    /// <param name="endpoints">Redis 端点列表。</param>
    /// <returns>按输入顺序返回的监控器列表。</returns>
    public static async Task<IReadOnlyList<BenchmarkMonitor>> ConnectAsync(
        IReadOnlyList<string> endpoints)
    {
        var monitors = await Task.WhenAll(endpoints.Select(BenchmarkMonitor.ConnectAsync));
        return monitors;
    }
}

/// <summary>
/// 定义基准测试的规模、拓扑和诊断选项。
/// </summary>
sealed class BenchmarkOptions
{
    /// <summary>
    /// 获取或初始化每轮测试的数据项数量。
    /// </summary>
    public int[] Counts { get; init; } = new[] { 1000, 10000, 100000 };
    /// <summary>
    /// 获取或初始化每个规模的预热轮数。
    /// </summary>
    public int Warmups { get; init; } = 2;
    /// <summary>
    /// 获取或初始化每个规模的正式采样轮数。
    /// </summary>
    public int Samples { get; init; } = 10;
    /// <summary>
    /// 获取或初始化命中读取次数。
    /// </summary>
    public int HitReads { get; init; } = 100;
    /// <summary>
    /// 获取或初始化并发回源请求数。
    /// </summary>
    public int RequestedConcurrency { get; init; } = 16;
    /// <summary>
    /// 获取或初始化模拟回源操作的延迟毫秒数。
    /// </summary>
    public int RetrieverDelayMs { get; init; } = 50;
    /// <summary>
    /// 获取或初始化扫描数据集之外的噪声键数量。
    /// </summary>
    public int ScanNoiseCount { get; init; }
    /// <summary>
    /// 获取或初始化 Redis 拓扑名称。
    /// </summary>
    public string Topology { get; init; } = "single";
    /// <summary>
    /// 获取或初始化拓扑中的节点数量。
    /// </summary>
    public int NodeCount { get; init; } = 1;
    /// <summary>
    /// 获取或初始化是否测量批量读取的分配组成。
    /// </summary>
    public bool AllocationComponents { get; init; }
    /// <summary>
    /// 获取或初始化是否启用 Redis 命令探针。
    /// </summary>
    public bool CommandProbe { get; init; }
    /// <summary>
    /// 获取或初始化是否启用工作集资源探针。
    /// </summary>
    public bool ResourceProbe { get; init; }
    /// <summary>
    /// 获取或初始化检查点文件路径。
    /// </summary>
    public string CheckpointPath { get; init; }

    /// <summary>
    /// 解析命令行基准测试选项。
    /// </summary>
    /// <param name="arguments">形如名称和值的命令行选项集合。</param>
    /// <returns>解析后的基准测试选项。</returns>
    public static BenchmarkOptions Parse(IEnumerable<string> arguments)
    {
        var values = arguments.ToDictionary(argument => argument[..argument.IndexOf('=')],
            argument => argument[(argument.IndexOf('=') + 1)..], StringComparer.OrdinalIgnoreCase);
        var counts = ParsePositiveList(values, "--counts", new[] { 1000, 10000, 100000 });
        return new BenchmarkOptions
        {
            Counts = counts,
            Warmups = ParsePositive(values, "--warmups", 2),
            Samples = ParsePositive(values, "--samples", 10),
            HitReads = ParsePositive(values, "--hit-reads", 100),
            RequestedConcurrency = ParsePositive(values, "--requested-concurrency", 16),
            RetrieverDelayMs = ParsePositive(values, "--retriever-delay-ms", 50),
            ScanNoiseCount = ParseNonNegative(values, "--scan-noise-count", 0),
            Topology = ParseTopology(values),
            NodeCount = ParsePositive(values, "--nodes", 1),
            AllocationComponents = values.TryGetValue("--allocation-components", out var enabled) &&
                bool.Parse(enabled),
            CommandProbe = values.TryGetValue("--command-probe", out var probe) && bool.Parse(probe),
            ResourceProbe = values.TryGetValue("--resource-probe", out var memory) && bool.Parse(memory),
            CheckpointPath = values.TryGetValue("--checkpoint-path", out var checkpoint) &&
                !string.IsNullOrWhiteSpace(checkpoint) ? checkpoint : null
        };
    }

    /// <summary>
    /// 解析并校验 Redis 拓扑选项。
    /// </summary>
    /// <param name="values">命令行选项字典。</param>
    /// <returns>规范化为小写的拓扑名称。</returns>
    private static string ParseTopology(IReadOnlyDictionary<string, string> values)
    {
        if (!values.TryGetValue("--topology", out var topology))
            return "single";
        if (!string.Equals(topology, "single", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(topology, "sharded", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(topology, "cluster", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--topology 必须是 single、sharded 或 cluster。", "--topology");
        return topology.ToLowerInvariant();
    }

    /// <summary>
    /// 解析一个必须为正整数的选项。
    /// </summary>
    /// <param name="values">命令行选项字典。</param>
    /// <param name="name">选项名称。</param>
    /// <param name="fallback">选项缺失时使用的默认值。</param>
    /// <returns>解析后的正整数。</returns>
    private static int ParsePositive(IReadOnlyDictionary<string, string> values, string name, int fallback)
    {
        if (!values.TryGetValue(name, out var raw))
            return fallback;
        if (!int.TryParse(raw, out var value) || value <= 0)
            throw new ArgumentException($"{name} 必须是正整数。", name);
        return value;
    }

    /// <summary>
    /// 解析一个必须为非负整数的选项。
    /// </summary>
    /// <param name="values">命令行选项字典。</param>
    /// <param name="name">选项名称。</param>
    /// <param name="fallback">选项缺失时使用的默认值。</param>
    /// <returns>解析后的非负整数。</returns>
    private static int ParseNonNegative(IReadOnlyDictionary<string, string> values, string name, int fallback)
    {
        if (!values.TryGetValue(name, out var raw))
            return fallback;
        if (!int.TryParse(raw, out var value) || value < 0)
            throw new ArgumentException($"{name} 必须是非负整数。", name);
        return value;
    }

    /// <summary>
    /// 解析逗号分隔的正整数列表。
    /// </summary>
    /// <param name="values">命令行选项字典。</param>
    /// <param name="name">选项名称。</param>
    /// <param name="fallback">选项缺失时使用的默认列表。</param>
    /// <returns>解析后的正整数数组。</returns>
    private static int[] ParsePositiveList(IReadOnlyDictionary<string, string> values, string name, int[] fallback)
    {
        if (!values.TryGetValue(name, out var raw))
            return fallback;
        var result = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
            .ToArray();
        if (result.Length == 0 || result.Any(value => value <= 0))
            throw new ArgumentException($"{name} 必须是逗号分隔的正整数。", name);
        return result;
    }
}

/// <summary>
/// 表示一次基准测试运行的汇总报告。
/// </summary>
sealed class BenchmarkReport
{
    /// <summary>
    /// 获取或初始化任务标识。
    /// </summary>
    public string TaskId { get; init; }
    /// <summary>
    /// 获取或初始化报告标签。
    /// </summary>
    public string Label { get; init; }
    /// <summary>
    /// 获取或初始化缓存 Provider 名称。
    /// </summary>
    public string Provider { get; init; }
    /// <summary>
    /// 获取或初始化运行时版本。
    /// </summary>
    public string Runtime { get; init; }
    /// <summary>
    /// 获取或初始化运行框架描述。
    /// </summary>
    public string Framework { get; init; }
    /// <summary>
    /// 获取或初始化操作系统描述。
    /// </summary>
    public string OperatingSystem { get; init; }
    /// <summary>
    /// 获取或初始化进程架构名称。
    /// </summary>
    public string ProcessArchitecture { get; init; }
    /// <summary>
    /// 获取或初始化脱敏后的连接端点。
    /// </summary>
    public string Endpoint { get; init; }
    /// <summary>
    /// 获取或初始化连接端点指纹。
    /// </summary>
    public string EndpointFingerprint { get; init; }
    /// <summary>
    /// 获取或初始化报告生成时间（UTC）。
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; }
    /// <summary>
    /// 获取或初始化本次运行的基准配置。
    /// </summary>
    public BenchmarkOptions Configuration { get; init; }
    /// <summary>
    /// 获取或初始化流式 API 测量状态。
    /// </summary>
    public string StreamingApiStatus { get; init; }
    /// <summary>
    /// 获取或初始化各数据规模的汇总结果。
    /// </summary>
    public IReadOnlyList<CountReport> Results { get; init; }
    /// <summary>
    /// 获取或初始化 Redis 拓扑名称。
    /// </summary>
    public string Topology { get; init; }
    /// <summary>
    /// 获取或初始化拓扑节点数量。
    /// </summary>
    public int NodeCount { get; init; }
    /// <summary>
    /// 获取或初始化实际监控节点数量。
    /// </summary>
    public int MonitoredNodeCount { get; init; }
    /// <summary>
    /// 获取或初始化本次运行标识。
    /// </summary>
    public string RunIdentity { get; init; }
    /// <summary>
    /// 获取或初始化工作负载指纹。
    /// </summary>
    public string WorkloadIdentity { get; init; }
    /// <summary>
    /// 获取或初始化数据集指纹。
    /// </summary>
    public string DatasetIdentity { get; init; }
    /// <summary>
    /// 获取或初始化检查点文件路径。
    /// </summary>
    public string CheckpointPath { get; init; }
    /// <summary>
    /// 获取或初始化检查点完成状态。
    /// </summary>
    public string CheckpointStatus { get; init; }
    /// <summary>
    /// 获取或初始化各预热和采样轮的检查点。
    /// </summary>
    public IReadOnlyList<BenchmarkRoundCheckpoint> Rounds { get; init; }
}

/// <summary>
/// 表示可恢复的基准测试检查点。
/// </summary>
sealed class BenchmarkCheckpoint
{
    /// <summary>
    /// 获取或初始化检查点类型标识。
    /// </summary>
    public string Kind { get; init; } = "BENCHMARK_CHECKPOINT";
    /// <summary>
    /// 获取或初始化任务标识。
    /// </summary>
    public string TaskId { get; init; }
    /// <summary>
    /// 获取或初始化报告标签。
    /// </summary>
    public string Label { get; init; }
    /// <summary>
    /// 获取或初始化缓存 Provider 名称。
    /// </summary>
    public string Provider { get; init; }
    /// <summary>
    /// 获取或初始化脱敏后的连接端点。
    /// </summary>
    public string Endpoint { get; init; }
    /// <summary>
    /// 获取或初始化连接端点指纹。
    /// </summary>
    public string EndpointFingerprint { get; init; }
    /// <summary>
    /// 获取或初始化本次运行标识。
    /// </summary>
    public string RunIdentity { get; init; }
    /// <summary>
    /// 获取或初始化工作负载指纹。
    /// </summary>
    public string WorkloadIdentity { get; init; }
    /// <summary>
    /// 获取或初始化数据集指纹。
    /// </summary>
    public string DatasetIdentity { get; init; }
    /// <summary>
    /// 获取或初始化 Redis 拓扑名称。
    /// </summary>
    public string Topology { get; init; }
    /// <summary>
    /// 获取或初始化拓扑节点数量。
    /// </summary>
    public int NodeCount { get; init; }
    /// <summary>
    /// 获取或初始化当前检查点状态。
    /// </summary>
    public string Status { get; init; }
    /// <summary>
    /// 获取或初始化当前处理的数据规模。
    /// </summary>
    public int CurrentCount { get; init; }
    /// <summary>
    /// 获取或初始化已完成的预热轮数。
    /// </summary>
    public int CompletedWarmups { get; init; }
    /// <summary>
    /// 获取或初始化已完成的采样轮数。
    /// </summary>
    public int CompletedSamples { get; init; }
    /// <summary>
    /// 获取或初始化预期采样轮数。
    /// </summary>
    public int ExpectedSamples { get; init; }
    /// <summary>
    /// 获取或初始化已完成数据规模的结果。
    /// </summary>
    public IReadOnlyList<CountReport> CompletedResults { get; init; }
    /// <summary>
    /// 获取或初始化当前数据规模的进行中结果。
    /// </summary>
    public CountReportCheckpoint InProgressResult { get; init; }
    /// <summary>
    /// 获取或初始化各预热和采样轮的检查点。
    /// </summary>
    public IReadOnlyList<BenchmarkRoundCheckpoint> Rounds { get; init; }
    /// <summary>
    /// 获取或初始化检查点更新时间（UTC）。
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 根据运行参数创建工作负载指纹。
    /// </summary>
    /// <param name="provider">缓存 Provider 名称。</param>
    /// <param name="endpoint">Redis 连接端点。</param>
    /// <param name="label">报告标签。</param>
    /// <param name="options">基准测试选项。</param>
    /// <returns>由工作负载参数计算出的十六进制指纹。</returns>
    public static string CreateWorkloadIdentity(string provider, string endpoint, string label,
        BenchmarkOptions options)
    {
        var input = string.Join("|", "cachebench-workload-v2", provider, endpoint, label, options.Topology,
            options.NodeCount, string.Join(",", options.Counts), options.Warmups,
            options.Samples, options.HitReads, options.RequestedConcurrency, options.RetrieverDelayMs,
            options.ScanNoiseCount, options.AllocationComponents, options.CommandProbe, options.ResourceProbe);
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    /// <summary>
    /// 创建连接端点的稳定指纹。
    /// </summary>
    /// <param name="endpoint">Redis 连接端点。</param>
    /// <returns>连接端点的十六进制指纹。</returns>
    public static string CreateEndpointFingerprint(string endpoint) =>
        HashText(endpoint ?? string.Empty);

    /// <summary>
    /// 移除连接端点中的敏感信息并保留可识别部分。
    /// </summary>
    /// <param name="endpoint">Redis 连接端点字符串。</param>
    /// <returns>脱敏后的端点字符串。</returns>
    public static string RedactEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return endpoint;
        var entries = endpoint.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(RedactEndpointEntry)
            .ToArray();
        return entries.Length == 0 ? "<redacted>" : string.Join(";", entries);
    }

    /// <summary>
    /// 脱敏单个 Redis 端点条目。
    /// </summary>
    /// <param name="entry">端点条目。</param>
    /// <returns>脱敏后的端点条目。</returns>
    private static string RedactEndpointEntry(string entry)
    {
        var value = entry.Trim();
        if (value.Length == 0)
            return "<redacted>";
        var firstOption = value.IndexOf(',');
        var host = firstOption < 0 ? value : value[..firstOption];
        if (host.Contains('=') || host.Contains('@'))
            return "<redacted>";
        if (host.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(host, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
                return "<redacted>";
            var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port;
            return uri.Scheme + "://" + uri.Host + port;
        }
        return host;
    }

    /// <summary>
    /// 计算文本的十六进制 SHA-256 指纹。
    /// </summary>
    /// <param name="value">待计算文本。</param>
    /// <returns>小写十六进制指纹。</returns>
    private static string HashText(string value)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    /// <summary>
    /// 将检查点以 UTF-8 JSON 原子写入文件。
    /// </summary>
    /// <param name="path">检查点文件路径。</param>
    /// <param name="checkpoint">待写入的检查点。</param>
    public static void Write(string path, BenchmarkCheckpoint checkpoint)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("基准检查点路径不能为空。", nameof(path));
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        Directory.CreateDirectory(directory!);
        var temporary = path + ".tmp." + Guid.NewGuid().ToString("N");
        var json = TextJsonSerializer.Serialize(checkpoint, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        try
        {
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}

/// <summary>
/// 表示单轮基准测试的执行状态。
/// </summary>
sealed class BenchmarkRoundCheckpoint
{
    /// <summary>
    /// 获取或初始化本轮唯一标识。
    /// </summary>
    public string RoundIdentity { get; init; }
    /// <summary>
    /// 获取或初始化本轮数据项数量。
    /// </summary>
    public int Count { get; init; }
    /// <summary>
    /// 获取或初始化预热轮次索引。
    /// </summary>
    public int? WarmupIndex { get; init; }
    /// <summary>
    /// 获取或初始化采样轮次索引。
    /// </summary>
    public int? SampleIndex { get; init; }
    /// <summary>
    /// 获取或初始化本轮阶段名称。
    /// </summary>
    public string Stage { get; init; }
    /// <summary>
    /// 获取或初始化本轮缓存命名空间。
    /// </summary>
    public string Namespace { get; init; }
    /// <summary>
    /// 获取或初始化业务数据键前缀。
    /// </summary>
    public string LogicalPrefix { get; init; }
    /// <summary>
    /// 获取或初始化噪声键前缀。
    /// </summary>
    public string NoiseLogicalPrefix { get; init; }
    /// <summary>
    /// 获取或初始化噪声键数量。
    /// </summary>
    public int NoiseCount { get; init; }
    /// <summary>
    /// 获取或初始化物理扫描模式。
    /// </summary>
    public string PhysicalPattern { get; init; }
    /// <summary>
    /// 本轮执行状态。
    /// </summary>
    public string Status { get; set; }
    /// <summary>
    /// 本轮清理状态。
    /// </summary>
    public string CleanupStatus { get; set; }
}

/// <summary>
/// 表示一个数据规模的进行中采样结果。
/// </summary>
sealed class CountReportCheckpoint
{
    /// <summary>
    /// 获取或初始化数据项数量。
    /// </summary>
    public int Count { get; init; }
    /// <summary>
    /// 获取或初始化预热轮数。
    /// </summary>
    public int Warmups { get; init; }
    /// <summary>
    /// 获取或初始化已完成采样轮数。
    /// </summary>
    public int Samples { get; init; }
    /// <summary>
    /// 获取或初始化已采集的原始样本。
    /// </summary>
    public IReadOnlyList<RoundReport> RawSamples { get; init; }
}

/// <summary>
/// 汇总一个数据规模的基准测试结果。
/// </summary>
sealed class CountReport
{
    /// <summary>
    /// 获取或初始化数据项数量。
    /// </summary>
    public int Count { get; init; }
    /// <summary>
    /// 获取或初始化预热轮数。
    /// </summary>
    public int Warmups { get; init; }
    /// <summary>
    /// 获取或初始化正式采样轮数。
    /// </summary>
    public int Samples { get; init; }
    /// <summary>
    /// 获取或初始化各阶段指标摘要。
    /// </summary>
    public Dictionary<string, PhaseSummary> Phases { get; init; }
    /// <summary>
    /// 获取或初始化命中请求延迟摘要（毫秒）。
    /// </summary>
    public MetricSummary HitRequestLatencyMs { get; init; }
    /// <summary>
    /// 获取或初始化进程峰值工作集摘要（字节）。
    /// </summary>
    public MetricSummary PeakWorkingSetBytes { get; init; }
    /// <summary>
    /// 获取或初始化进程结束时工作集摘要（字节）。
    /// </summary>
    public MetricSummary WorkingSetBytes { get; init; }
    /// <summary>
    /// 获取或初始化各轮原始结果。
    /// </summary>
    public IReadOnlyList<RoundReport> RawSamples { get; init; }

    /// <summary>
    /// 根据各轮结果创建数据规模汇总。
    /// </summary>
    /// <param name="count">数据项数量。</param>
    /// <param name="options">基准测试选项。</param>
    /// <param name="samples">正式采样结果。</param>
    /// <returns>包含阶段和进程资源指标摘要的报告。</returns>
    public static CountReport Create(int count, BenchmarkOptions options, IReadOnlyList<RoundReport> samples)
    {
        var phaseNames = samples.SelectMany(sample => sample.Phases.Keys).Distinct(StringComparer.Ordinal);
        var phases = phaseNames.ToDictionary(name => name, name => PhaseSummary.Create(
            samples.Select(sample => sample.Phases[name]), count, options));
        return new CountReport
        {
            Count = count,
            Warmups = options.Warmups,
            Samples = samples.Count,
            Phases = phases,
            HitRequestLatencyMs = MetricSummary.From(samples.SelectMany(sample => sample.HitRequestLatencyMs)),
            PeakWorkingSetBytes = MetricSummary.From(samples.Select(sample => (double)sample.PeakWorkingSetBytes)),
            WorkingSetBytes = MetricSummary.From(samples.Select(sample => (double)sample.WorkingSetBytes)),
            RawSamples = samples
        };
    }
}

/// <summary>
/// 汇总一个基准阶段的耗时、资源和并发指标。
/// </summary>
sealed class PhaseSummary
{
    /// <summary>
    /// 获取或初始化阶段耗时摘要（毫秒）。
    /// </summary>
    public MetricSummary ElapsedMs { get; init; }
    /// <summary>
    /// 获取或初始化每秒处理项数摘要。
    /// </summary>
    public MetricSummary ThroughputItemsPerSecond { get; init; }
    /// <summary>
    /// 获取或初始化托管分配量摘要（字节）。
    /// </summary>
    public MetricSummary AllocatedBytes { get; init; }
    /// <summary>
    /// 获取或初始化 Redis 命令数摘要。
    /// </summary>
    public MetricSummary RedisCommands { get; init; }
    /// <summary>
    /// 获取或初始化原生客户端调用数摘要。
    /// </summary>
    public MetricSummary NativeClientCalls { get; init; }
    /// <summary>
    /// 获取或初始化按命令类型拆分的计数摘要。
    /// </summary>
    public Dictionary<string, MetricSummary> CommandBreakdown { get; init; }
    /// <summary>
    /// 获取或初始化按命令类型拆分的最大并发度摘要。
    /// </summary>
    public Dictionary<string, MetricSummary> CommandMaximumParallelism { get; init; }
    /// <summary>
    /// 获取或初始化扫描页数摘要。
    /// </summary>
    public MetricSummary ScanPages { get; init; }
    /// <summary>
    /// 获取或初始化扫描返回项数摘要。
    /// </summary>
    public MetricSummary ScanReturnedItems { get; init; }
    /// <summary>
    /// 获取或初始化阶段采样峰值工作集摘要（字节）。
    /// </summary>
    public MetricSummary SampledPhasePeakWorkingSetBytes { get; init; }
    /// <summary>
    /// 获取或初始化网络请求数摘要。
    /// </summary>
    public MetricSummary NetworkRequests { get; init; }
    /// <summary>
    /// 获取或初始化请求并发度摘要。
    /// </summary>
    public MetricSummary RequestedConcurrency { get; init; }
    /// <summary>
    /// 获取或初始化实际最大并发度摘要。
    /// </summary>
    public MetricSummary MaxActualParallelism { get; init; }
    /// <summary>
    /// 获取或初始化最大排队请求数摘要。
    /// </summary>
    public MetricSummary MaxQueuedRequests { get; init; }
    /// <summary>
    /// 获取或初始化回源执行次数摘要。
    /// </summary>
    public MetricSummary RetrieverCalls { get; init; }
    /// <summary>
    /// 获取或初始化并发度测量方式。
    /// </summary>
    public string ConcurrencyMeasurement { get; init; }

    /// <summary>
    /// 根据各轮阶段结果创建指标汇总。
    /// </summary>
    /// <param name="reports">阶段原始结果。</param>
    /// <param name="count">数据项数量。</param>
    /// <param name="options">基准测试选项。</param>
    /// <returns>按指标计算出的阶段摘要。</returns>
    public static PhaseSummary Create(IEnumerable<PhaseReport> reports, int count, BenchmarkOptions options)
    {
        var values = reports.ToArray();
        var commandKinds = values.SelectMany(value => value.CommandBreakdown.Keys)
            .Distinct(StringComparer.Ordinal);
        var commandMaximumKinds = values.SelectMany(value => value.CommandMaximumParallelism.Keys)
            .Distinct(StringComparer.Ordinal);
        return new PhaseSummary
        {
            ElapsedMs = MetricSummary.From(values.Select(value => value.ElapsedMs)),
            ThroughputItemsPerSecond = MetricSummary.From(values.Select(value =>
            {
                var workItems = value.Name switch
                {
                    "hit" => options.HitReads,
                    "stampede" => options.RequestedConcurrency,
                    _ => count
                };
                return value.ElapsedMs <= 0 ? 0 : workItems * 1000d / value.ElapsedMs;
            })),
            AllocatedBytes = MetricSummary.From(values.Select(value => (double)value.AllocatedBytes)),
            RedisCommands = MetricSummary.From(values.Select(value => (double)value.RedisCommands)),
            NativeClientCalls = MetricSummary.From(values.Select(value => (double)value.NativeClientCalls)),
            CommandBreakdown = commandKinds.ToDictionary(kind => kind, kind => MetricSummary.From(
                values.Select(value => value.CommandBreakdown.TryGetValue(kind, out var count)
                    ? (double)count
                    : 0d)), StringComparer.Ordinal),
            CommandMaximumParallelism = commandMaximumKinds.ToDictionary(kind => kind,
                kind => MetricSummary.From(values.Select(value =>
                    value.CommandMaximumParallelism.TryGetValue(kind, out var maximum)
                        ? (double)maximum
                        : 0d)), StringComparer.Ordinal),
            ScanPages = MetricSummary.From(values.Select(value => (double)value.ScanPages)),
            ScanReturnedItems = MetricSummary.From(values.Select(value => (double)value.ScanReturnedItems)),
            SampledPhasePeakWorkingSetBytes = MetricSummary.From(values.Select(value => (double)value.SampledPhasePeakWorkingSetBytes)),
            NetworkRequests = MetricSummary.From(values.Select(value => (double)value.NetworkRequests)),
            RequestedConcurrency = MetricSummary.From(values.Select(value => (double)value.RequestedConcurrency)),
            MaxActualParallelism = MetricSummary.From(values.Select(value => (double)value.MaxActualParallelism)),
            MaxQueuedRequests = MetricSummary.From(values.Select(value => (double)value.MaxQueuedRequests)),
            RetrieverCalls = MetricSummary.From(values.Select(value => (double)value.RetrieverCalls)),
            ConcurrencyMeasurement = string.Join(",", values.Select(value => value.ConcurrencyMeasurement)
                .Distinct(StringComparer.Ordinal))
        };
    }
}

/// <summary>
/// 保存单轮基准测试的阶段和进程资源结果。
/// </summary>
sealed class RoundReport
{
    /// <summary>
    /// 获取或初始化各阶段结果。
    /// </summary>
    public Dictionary<string, PhaseReport> Phases { get; init; }
    /// <summary>
    /// 获取或初始化命中读取的单次延迟列表（毫秒）。
    /// </summary>
    public IReadOnlyList<double> HitRequestLatencyMs { get; init; }
    /// <summary>
    /// 获取或初始化进程峰值工作集（字节）。
    /// </summary>
    public long PeakWorkingSetBytes { get; init; }
    /// <summary>
    /// 获取或初始化进程结束时工作集（字节）。
    /// </summary>
    public long WorkingSetBytes { get; init; }
    /// <summary>
    /// 获取或初始化是否纳入正式采样结果。
    /// </summary>
    public bool Captured { get; init; }
}

/// <summary>
/// 保存单个基准阶段的原始测量指标。
/// </summary>
sealed class PhaseReport
{
    /// <summary>
    /// 获取或初始化阶段名称。
    /// </summary>
    public string Name { get; init; }
    /// <summary>
    /// 获取或初始化阶段耗时（毫秒）。
    /// </summary>
    public double ElapsedMs { get; init; }
    /// <summary>
    /// 获取或初始化托管分配量（字节）。
    /// </summary>
    public long AllocatedBytes { get; init; }
    /// <summary>
    /// 获取或初始化 Redis 命令数。
    /// </summary>
    public long RedisCommands { get; init; }
    /// <summary>
    /// 获取或初始化原生客户端调用数；不可用时为负数。
    /// </summary>
    public long NativeClientCalls { get; init; } = -1;
    /// <summary>
    /// 获取或初始化按命令类型统计的调用次数。
    /// </summary>
    public IReadOnlyDictionary<string, long> CommandBreakdown { get; init; } =
        new Dictionary<string, long>(StringComparer.Ordinal);
    /// <summary>
    /// 获取或初始化按命令类型统计的最大并发度。
    /// </summary>
    public IReadOnlyDictionary<string, int> CommandMaximumParallelism { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    /// <summary>
    /// 获取或初始化扫描页数；不可用时为负数。
    /// </summary>
    public long ScanPages { get; init; } = -1;
    /// <summary>
    /// 获取或初始化扫描返回项数；不可用时为负数。
    /// </summary>
    public long ScanReturnedItems { get; init; } = -1;
    /// <summary>
    /// 获取或初始化阶段采样到的峰值工作集（字节）；不可用时为负数。
    /// </summary>
    public long SampledPhasePeakWorkingSetBytes { get; init; } = -1;
    /// <summary>
    /// 获取或初始化网络请求数；不可用时为负数。
    /// </summary>
    public long NetworkRequests { get; init; }
    /// <summary>
    /// 获取或初始化请求并发度。
    /// </summary>
    public int RequestedConcurrency { get; init; }
    /// <summary>
    /// 获取或初始化实际最大并发度。
    /// </summary>
    public int MaxActualParallelism { get; init; }
    /// <summary>
    /// 获取或初始化最大排队请求数。
    /// </summary>
    public int MaxQueuedRequests { get; init; }
    /// <summary>
    /// 获取或初始化回源执行次数。
    /// </summary>
    public int RetrieverCalls { get; init; }
    /// <summary>
    /// 获取或初始化并发度测量方式。
    /// </summary>
    public string ConcurrencyMeasurement { get; init; }
}

/// <summary>
/// 采样基准阶段内的进程驻留工作集。
/// </summary>
/// <remarks>
/// 每 10 毫秒采样一次并补充开始、结束采样；结果不是操作系统精确峰值，也不替代累计的 PeakWorkingSet64。
/// </remarks>
sealed class WorkingSetProbe : IDisposable
{
    /// <summary>
    /// 获取被采样的当前进程。
    /// </summary>
    private readonly Process _process = Process.GetCurrentProcess();
    /// <summary>
    /// 获取用于停止后台采样的取消源。
    /// </summary>
    private readonly CancellationTokenSource _stop = new();
    /// <summary>
    /// 获取后台采样任务。
    /// </summary>
    private readonly Task _sampling;
    /// <summary>
    /// 获取采样到的最大驻留工作集（字节）。
    /// </summary>
    public long Maximum { get; private set; }

    /// <summary>
    /// 初始化 WorkingSetProbe 类的新实例。
    /// </summary>
    public WorkingSetProbe()
    {
        Sample();
        _sampling = SampleAsync();
    }

    /// <summary>
    /// 读取当前进程工作集并更新最大值。
    /// </summary>
    private void Sample() { _process.Refresh(); Maximum = Math.Max(Maximum, _process.WorkingSet64); }

    /// <summary>
    /// 按固定间隔执行工作集采样，直到收到停止信号。
    /// </summary>
    private async Task SampleAsync()
    {
        try
        {
            while (true) { await Task.Delay(10, _stop.Token); Sample(); }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }
    /// <summary>
    /// 停止后台采样并执行最终采样。
    /// </summary>
    public async Task FinishAsync() { _stop.Cancel(); await _sampling; Sample(); }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        // 异常阶段也观察采样任务，并等它退出后释放进程句柄，避免后台读取已释放对象。
        _ = _sampling.ContinueWith(task =>
        {
            _ = task.Exception;
            _stop.Dispose();
            _process.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}

/// <summary>
/// 表示一组数值的分位数和平均值摘要。
/// </summary>
sealed class MetricSummary
{
    /// <summary>
    /// 获取或初始化样本数量。
    /// </summary>
    public int Samples { get; init; }
    /// <summary>
    /// 获取或初始化第 50 百分位数。
    /// </summary>
    public double P50 { get; init; }
    /// <summary>
    /// 获取或初始化第 95 百分位数。
    /// </summary>
    public double P95 { get; init; }
    /// <summary>
    /// 获取或初始化第 99 百分位数。
    /// </summary>
    public double P99 { get; init; }
    /// <summary>
    /// 获取或初始化样本平均值。
    /// </summary>
    public double Mean { get; init; }

    /// <summary>
    /// 从阶段报告的耗时创建指标摘要。
    /// </summary>
    /// <param name="values">阶段报告集合。</param>
    /// <returns>阶段耗时的分位数和平均值摘要。</returns>
    public static MetricSummary From(IEnumerable<PhaseReport> values) =>
        From(values.Select(value => value.ElapsedMs));

    /// <summary>
    /// 从数值序列创建指标摘要。
    /// </summary>
    /// <param name="values">待汇总的数值序列。</param>
    /// <returns>数值的分位数和平均值摘要；输入为空时返回空摘要。</returns>
    public static MetricSummary From(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
            return new MetricSummary();
        static double At(double[] source, double quantile) =>
            source[(int)Math.Ceiling((source.Length - 1) * quantile)];
        return new MetricSummary
        {
            Samples = ordered.Length,
            P50 = At(ordered, 0.50),
            P95 = At(ordered, 0.95),
            P99 = At(ordered, 0.99),
            Mean = ordered.Average()
        };
    }
}
