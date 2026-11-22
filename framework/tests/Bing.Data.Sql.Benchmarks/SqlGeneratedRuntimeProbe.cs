using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>使用既有 18 组 SQLite 同语义输入，固定采样当前运行库的时间及分配。</summary>
internal static class SqlGeneratedRuntimeProbe
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
                foreach (var operation in new[] { "NoTransaction", "SharedTransaction", "NonTransactionalUow" })
                {
                    Func<int> action = operation switch
                    {
                        "SharedTransaction" => workload.SharedTransaction,
                        "NonTransactionalUow" => workload.NonTransactionalUow,
                        _ => workload.NoTransaction
                    };
                    for (var i = 0; i < warmup; i++)
                        if (action() != rows) throw new InvalidOperationException("Warmup checksum.");
                    for (var sample = 0; sample < samples; sample++)
                    {
                        var allocated = GC.GetAllocatedBytesForCurrentThread();
                        var timer = Stopwatch.StartNew();
                        var checksum = 0;
                        for (var i = 0; i < iterations; i++) checksum += action();
                        timer.Stop();
                        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                        if (checksum != rows * iterations) throw new InvalidOperationException("Checksum.");
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
            workload = "SqlGeneratedRuntimeProbe-v1", warmup, samples, iterations,
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            comparison = "Current candidate modes with identical existing SQLite workload; bounded diagnostic, not formal BDN",
            results
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return 0;
    }
}
