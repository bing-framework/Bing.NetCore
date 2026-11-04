using System;
using BenchmarkDotNet.Running;
using Bing.Ddd.Domain.Benchmarks;

// 快速核对工作负载语义，避免把错误或空操作计入性能结果。
if (args.Length == 1 && args[0] == "--smoke")
{
    foreach (var count in new[] { 100, 1000 })
    {
        var workload = new ChangeGraphBenchmarks { Count = count };
        workload.Setup();
        workload.Capture();
        workload.CaptureIgnoredSubtree();
        if (workload.Compare() != 4 || workload.CompareSnapshot() != 4 || workload.CompareMoves() <= 0)
            throw new InvalidOperationException("Benchmark workload produced an unexpected change set.");
        Console.WriteLine($"Count={count}: capture, diff, snapshot, moves and ignored getter passed.");
    }
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(ChangeGraphBenchmarks).Assembly).Run(args);
