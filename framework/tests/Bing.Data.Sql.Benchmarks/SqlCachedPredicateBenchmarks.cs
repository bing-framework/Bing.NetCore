using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Bing.Data.Sql.Benchmarks.Models;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>比较生成查询中内联与静态缓存条件的构建成本。</summary>
/// <remarks>每次调用均创建新 Builder，不包含数据库 I/O。</remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SqlCachedPredicateBenchmarks
{
    private const int WarmupIterations = 5000;
    private const int Iterations = 50000;
    private const int Repetitions = 5;
    private SqlBuilderServices _services;
    private ISqlQueryPlanExecutor _executor;

    /// <summary>是否应用可选条件。</summary>
    [Params(false, true)]
    public bool Enabled { get; set; }

    /// <summary>初始化共享服务并确认两组查询的完整 SQL 一致。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _services = SqlBuilderServices.CreateDefault();
        _executor = DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();
        var expected = "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r`" +
            (Enabled ? " \r\nWhere `r`.`Id`>=@_p_0" : "");
        if (InlineTemplate() != expected || CachedTemplate() != expected ||
            InlineValue() != expected || CachedValue() != expected)
            throw new InvalidOperationException("内联与缓存条件生成的 SQL 不一致。");

        var expectedProjection = "Select Count(`r`.`Id`) As `Total` \r\nFrom `PredicateBenchmarkRows` As `r`" +
            (Enabled ? " \r\nWhere `r`.`Id`>=@_p_0" : "");
        var inlineProjectionBuilder = NewBuilder();
        var cachedProjectionBuilder = NewBuilder();
        var inlineProjection = GeneratedPredicateWorkload.InlineProjection(_executor, inlineProjectionBuilder, Enabled, 11);
        var cachedProjection = GeneratedPredicateWorkload.CachedProjection(_executor, cachedProjectionBuilder, Enabled, 11);
        if (inlineProjection != expectedProjection || cachedProjection != expectedProjection)
            throw new InvalidOperationException("内联与缓存投影的完整 SQL 不一致。");

        var expectedGrouped = expected + " \r\nGroup By `r`.`Id` \r\nOrder By `r`.`Id`";
        if (InlineGrouped() != expectedGrouped || CachedGrouped() != expectedGrouped)
            throw new InvalidOperationException("内联与缓存分组的完整 SQL 不一致。");

        const string expectedRequiredWhere =
            "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r` \r\nWhere `r`.`Id`>=@_p_0";
        if (RequiredWhereBridge() != expectedRequiredWhere || RequiredWhereTemplate() != expectedRequiredWhere)
            throw new InvalidOperationException("逐调用桥接与完整模板的必需 Where SQL 不一致。");

        var expectedExcluded = "Select `r`.`Id` \r\nFrom `ExcludedPredicateBenchmarkRows` As `r`" +
            (Enabled ? " \r\nWhere `r`.`Id`>=@_p_0" : "");
        if (ExcludedBridge() != expectedExcluded || ExcludedTemplate() != expectedExcluded)
            throw new InvalidOperationException("逐调用与模板排除列的完整 SQL 不一致。");
    }

    /// <summary>内联单参数条件与生成模板。</summary>
    [Benchmark(Baseline = true), BenchmarkCategory("Template")]
    public string InlineTemplate() => GeneratedPredicateWorkload.InlineTemplate(_executor, NewBuilder(), Enabled);

    /// <summary>静态缓存单参数条件与生成模板。</summary>
    [Benchmark, BenchmarkCategory("Template")]
    public string CachedTemplate() => GeneratedPredicateWorkload.CachedTemplate(_executor, NewBuilder(), Enabled);

    /// <summary>内联双参数条件与显式参数值。</summary>
    [Benchmark(Baseline = true), BenchmarkCategory("ExplicitValue")]
    public string InlineValue() => GeneratedPredicateWorkload.InlineValue(_executor, NewBuilder(), Enabled, 11);

    /// <summary>静态缓存双参数条件与显式参数值。</summary>
    [Benchmark, BenchmarkCategory("ExplicitValue")]
    public string CachedValue() => GeneratedPredicateWorkload.CachedValue(_executor, NewBuilder(), Enabled, 11);

    /// <summary>内联聚合投影。</summary>
    [Benchmark(Baseline = true), BenchmarkCategory("Projection")]
    public string InlineProjection() => GeneratedPredicateWorkload.InlineProjection(_executor, NewBuilder(), Enabled, 11);

    /// <summary>静态只读聚合投影。</summary>
    [Benchmark, BenchmarkCategory("Projection")]
    public string CachedProjection() => GeneratedPredicateWorkload.CachedProjection(_executor, NewBuilder(), Enabled, 11);

    /// <summary>内联分组和排序列。</summary>
    [Benchmark(Baseline = true), BenchmarkCategory("GroupedColumns")]
    public string InlineGrouped() => GeneratedPredicateWorkload.InlineGrouped(_executor, NewBuilder(), Enabled, 11);

    /// <summary>静态只读分组和排序列。</summary>
    [Benchmark, BenchmarkCategory("GroupedColumns")]
    public string CachedGrouped() => GeneratedPredicateWorkload.CachedGrouped(_executor, NewBuilder(), Enabled, 11);

    /// <summary>通过逐调用桥接构造纯 Where 查询。</summary>
    [Benchmark(Baseline = true), BenchmarkCategory("RequiredWhere")]
    public string RequiredWhereBridge() =>
        GeneratedPredicateWorkload.RequiredWhereBridge(_executor, NewBuilder(), 11);

    /// <summary>通过完整查询模板构造相同的纯 Where 查询。</summary>
    [Benchmark, BenchmarkCategory("RequiredWhere")]
    public string RequiredWhereTemplate() =>
        GeneratedPredicateWorkload.RequiredWhereTemplate(_executor, NewBuilder(), 11);

    /// <summary>使用逐调用生成桥接构建排除列查询。</summary>
    [Benchmark(Baseline = true), BenchmarkCategory("ExcludedColumns")]
    public string ExcludedBridge() => GeneratedPredicateWorkload.ExcludedBridge(_executor, NewBuilder(), Enabled, 11);

    /// <summary>使用完整查询模板构建相同排除列查询。</summary>
    [Benchmark, BenchmarkCategory("ExcludedColumns")]
    public string ExcludedTemplate() => GeneratedPredicateWorkload.ExcludedTemplate(_executor, NewBuilder(), Enabled, 11);

    private SqliteBuilder NewBuilder() => new(_services);

    /// <summary>采集逐调用与完整模板的等 SQL 配对观察值。</summary>
    internal static int RunExcludedProbe(string output)
    {
        var results = new List<object>();
        foreach (var enabled in new[] { false, true })
        {
            var benchmark = new SqlCachedPredicateBenchmarks { Enabled = enabled };
            benchmark.Setup();
            results.Add(new { enabled, samples = MeasurePair(benchmark.ExcludedBridge, benchmark.ExcludedTemplate) });
        }
        var report = new
        {
            probe = "ExcludedGeneratedTemplate-v1",
            runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            warmupIterationsPerPath = WarmupIterations,
            iterationsPerSample = Iterations,
            repetitions = Repetitions,
            paths = "Inline means generated bridge; Cached means complete query template.",
            ordering = "Even samples: bridge then template; odd samples: template then bridge.",
            measurement = "Allocation is current-thread bytes; elapsed is wall-clock; CPU is process-wide. No database I/O.",
            results
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            new UTF8Encoding(false));
        Console.WriteLine("EXCLUDED_GENERATED_TEMPLATE_PROBE_PASS");
        return 0;
    }

    /// <summary>采集配对分配、经过时间及进程 CPU 观察值。</summary>
    internal static int RunAllocationProbe(string output)
    {
        var results = new List<object>();
        foreach (var enabled in new[] { false, true })
        {
            var benchmark = new SqlCachedPredicateBenchmarks { Enabled = enabled };
            benchmark.Setup();
            results.Add(new { enabled, kind = "Template", samples = MeasurePair(benchmark.InlineTemplate, benchmark.CachedTemplate) });
            results.Add(new { enabled, kind = "ExplicitValue", samples = MeasurePair(benchmark.InlineValue, benchmark.CachedValue) });
            results.Add(new { enabled, kind = "Projection", samples = MeasurePair(benchmark.InlineProjection, benchmark.CachedProjection) });
            results.Add(new { enabled, kind = "GroupedColumns", samples = MeasurePair(benchmark.InlineGrouped, benchmark.CachedGrouped) });
        }

        var requiredWhere = new SqlCachedPredicateBenchmarks();
        requiredWhere.Setup();
        results.Add(new { kind = "RequiredWhere", samples = MeasurePair(requiredWhere.RequiredWhereBridge, requiredWhere.RequiredWhereTemplate) });

        var report = new
        {
            probe = "CachedGeneratedPredicate-v4",
            runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            warmupIterationsPerPath = WarmupIterations,
            iterationsPerSample = Iterations,
            repetitions = Repetitions,
            ordering = "Even samples: inline then cached; odd samples: cached then inline.",
            measurement = "Allocation is current-thread bytes; elapsed is wall-clock; CPU is process-wide and includes other runtime threads. RequiredWhere compares a local-variable bridge with a complete query template. All workloads measure query construction only; no database I/O.",
            results
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            new UTF8Encoding(false));
        Console.WriteLine("CACHED_GENERATED_PREDICATE_PROBE_PASS");
        return 0;
    }

    private static PairSamples MeasurePair(Func<string> inline, Func<string> cached)
    {
        for (var index = 0; index < WarmupIterations; index++) { inline(); cached(); }
        var inlineSamples = new Sample[Repetitions];
        var cachedSamples = new Sample[Repetitions];
        using var process = Process.GetCurrentProcess();
        for (var sample = 0; sample < Repetitions; sample++)
        {
            if (sample % 2 == 0)
            {
                inlineSamples[sample] = Measure(inline, process);
                cachedSamples[sample] = Measure(cached, process);
            }
            else
            {
                cachedSamples[sample] = Measure(cached, process);
                inlineSamples[sample] = Measure(inline, process);
            }
        }
        return new PairSamples(inlineSamples, cachedSamples);
    }

    private static Sample Measure(Func<string> operation, Process process)
    {
        process.Refresh();
        var cpuBefore = process.TotalProcessorTime.Ticks;
        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        string last = null;
        for (var index = 0; index < Iterations; index++) last = operation();
        var elapsed = Stopwatch.GetTimestamp() - started;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
        process.Refresh();
        var cpuTicks = process.TotalProcessorTime.Ticks - cpuBefore;
        GC.KeepAlive(last);
        return new Sample(bytes / (double)Iterations, elapsed * (1_000_000_000d / Stopwatch.Frequency) / Iterations,
            cpuTicks * 100d / Iterations);
    }

    private sealed record PairSamples(Sample[] Inline, Sample[] Cached);
    private readonly record struct Sample(double AllocatedBytesPerOperation, double ElapsedNanosecondsPerOperation,
        double ProcessCpuNanosecondsPerOperation);

    private class NoOpExecutor : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}
