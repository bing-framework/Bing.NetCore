using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>独立验证 Lambda 包装与绑定路径、First/Single 专用状态的实际分配。</summary>
internal static class SqlGeneratedAllocationProbe
{
    internal static int Run(string output)
    {
        const int warmup = 32, samples = 5, iterations = 100;
        var results = new List<object>();
        foreach (var mode in new[] { SqlExecutionMode.Compatible, SqlExecutionMode.GeneratedOnly })
        foreach (var rows in new[] { 1, 32, 256 })
        {
            var workload = new SqlGeneratedSqliteBenchmarks { Mode = mode, Rows = rows };
            workload.Setup();
            try
            {
                foreach (var operation in new[] { "LambdaWrapped", "LambdaBound", "FirstRow", "SingleRow" })
                {
                    Func<int> action = operation switch
                    {
                        "LambdaWrapped" => workload.LambdaWrapped,
                        "LambdaBound" => workload.LambdaBound,
                        "FirstRow" => workload.FirstRow,
                        _ => workload.SingleRow
                    };
                    var expected = operation.StartsWith("Lambda", StringComparison.Ordinal) ? rows : 1;
                    for (var index = 0; index < warmup; index++)
                        if (action() != expected) throw new InvalidOperationException("Warmup checksum.");
                    for (var sample = 0; sample < samples; sample++)
                    {
                        var allocated = GC.GetAllocatedBytesForCurrentThread();
                        var timer = Stopwatch.StartNew(); var checksum = 0;
                        for (var index = 0; index < iterations; index++) checksum += action();
                        timer.Stop();
                        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                        if (checksum != expected * iterations) throw new InvalidOperationException("Checksum.");
                        results.Add(new { mode = mode.ToString(), rows, operation, sample,
                            nsPerOperation = timer.Elapsed.TotalNanoseconds / iterations,
                            bytesPerOperation = (double)allocated / iterations });
                    }
                }
            }
            finally { workload.Cleanup(); }
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            workload = "SqlGeneratedAllocationProbe-v1", warmup, samples, iterations,
            comparison = "Same candidate and SQLite inputs; manual delegates match generated reader shape; bounded diagnostic, not formal BDN",
            results
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return 0;
    }
}
