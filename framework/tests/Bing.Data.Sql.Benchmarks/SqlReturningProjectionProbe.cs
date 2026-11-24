using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>有界比较缓存、运行时内联和生成式 Returning 投影的分配。</summary>
internal static class SqlReturningProjectionProbe
{
    private const int WarmupIterations = 2048;
    private const int Samples = 7;
    private const int IterationsPerSample = 5000;

    internal static int Run(string output)
    {
        var outputPath = Path.GetFullPath(output);
        var benchmarkRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
            "artifacts", "benchmarks"));
        var allowedPrefix = benchmarkRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
        if (!outputPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("输出路径必须位于 artifacts/benchmarks/ 下。", nameof(output));

        var benchmark = new SqlReturningProjectionBenchmarks();
        benchmark.Setup();
        var scenarios = new (string Name, Func<SqlWriteCommand> Build, string ExpectedSql)[]
        {
            ("cached-anonymous", benchmark.BuildAnonymousProjection, SqlReturningProjectionBenchmarks.ExpectedSql),
            ("cached-dto", benchmark.BuildDtoMemberInitProjection, SqlReturningProjectionBenchmarks.ExpectedSql),
            ("runtime-inline-anonymous", benchmark.BuildRuntimeInlineAnonymousProjection, SqlReturningProjectionBenchmarks.ExpectedSql),
            ("runtime-inline-dto", benchmark.BuildRuntimeInlineDtoProjection, SqlReturningProjectionBenchmarks.ExpectedSql),
            ("generated-anonymous", benchmark.BuildGeneratedAnonymousProjection, SqlReturningProjectionBenchmarks.ExpectedSql),
            ("generated-dto", benchmark.BuildGeneratedDtoProjection, SqlReturningProjectionBenchmarks.ExpectedSql),
            ("cached-computed-arithmetic", benchmark.BuildCachedComputedProjection, SqlReturningProjectionBenchmarks.ExpectedComputedSql),
            ("runtime-inline-computed-arithmetic", benchmark.BuildRuntimeInlineComputedProjection, SqlReturningProjectionBenchmarks.ExpectedComputedSql),
            ("generated-inline-computed-arithmetic", benchmark.BuildGeneratedInlineComputedProjection, SqlReturningProjectionBenchmarks.ExpectedComputedSql),
            ("cached-remapped-computed-arithmetic", benchmark.BuildCachedRemappedComputedProjection, SqlReturningProjectionBenchmarks.ExpectedRemappedComputedSql),
            ("generated-remapped-computed-arithmetic", benchmark.BuildGeneratedRemappedComputedProjection, SqlReturningProjectionBenchmarks.ExpectedRemappedComputedSql),
            ("generated-quoted-columns", benchmark.BuildGeneratedQuotedProjection, SqlReturningProjectionBenchmarks.ExpectedQuotedSql),
            ("generated-quoted-arithmetic", benchmark.BuildGeneratedQuotedComputedProjection, SqlReturningProjectionBenchmarks.ExpectedQuotedComputedSql),
            ("generated-cached-columns", benchmark.BuildGeneratedCachedProjection, SqlReturningProjectionBenchmarks.ExpectedSql),
            ("generated-cached-arithmetic", benchmark.BuildGeneratedCachedComputedProjection, SqlReturningProjectionBenchmarks.ExpectedComputedSql)
        };
        var results = new List<object>(scenarios.Length * Samples);
        foreach (var scenario in scenarios)
        {
            for (var index = 0; index < WarmupIterations; index++)
                if (scenario.Build().Sql != scenario.ExpectedSql)
                    throw new InvalidOperationException($"{scenario.Name} warmup SQL differs from expected SQL.");

            for (var sample = 0; sample < Samples; sample++)
            {
                var checksum = 0;
                var timer = new Stopwatch();
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                timer.Start();
                for (var iteration = 0; iteration < IterationsPerSample; iteration++)
                    checksum += scenario.Build().Sql.Length;
                timer.Stop();
                var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                if (checksum != scenario.ExpectedSql.Length * IterationsPerSample)
                    throw new InvalidOperationException($"{scenario.Name} checksum differs from expected SQL.");
                results.Add(new
                {
                    scenario = scenario.Name,
                    sample,
                    iterations = IterationsPerSample,
                    nsPerOperation = timer.Elapsed.TotalNanoseconds / IterationsPerSample,
                    bytesPerOperation = (double)allocatedBytes / IterationsPerSample
                });
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new
        {
            workload = "SqlReturningProjectionProbe-v5",
            runtime = Environment.Version.ToString(),
            os = Environment.OSVersion.ToString(),
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            expectedSql = SqlReturningProjectionBenchmarks.ExpectedSql,
            computedExpectedSql = SqlReturningProjectionBenchmarks.ExpectedComputedSql,
            remappedComputedExpectedSql = SqlReturningProjectionBenchmarks.ExpectedRemappedComputedSql,
            quotedExpectedSql = SqlReturningProjectionBenchmarks.ExpectedQuotedSql,
            quotedComputedExpectedSql = SqlReturningProjectionBenchmarks.ExpectedQuotedComputedSql,
            warmupIterations = WarmupIterations,
            samples = Samples,
            iterationsPerSample = IterationsPerSample,
            measurement = "Command construction; inline expression tree creation is included; cached expressions initialize once before measurement; no database I/O.",
            results
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return 0;
    }
}
