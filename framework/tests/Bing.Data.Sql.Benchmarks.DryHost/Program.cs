using BenchmarkDotNet.Running;

namespace Bing.Data.Sql.Benchmarks.DryHost;

/// <summary>
/// 启动 SQL 投影 dry-host 基准测试。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 运行投影基准的精简宿主。
    /// </summary>
    /// <param name="args">传递给基准或探测入口的命令行参数。</param>
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromTypes(new[] { typeof(SqlProjectionDryHostBenchmarks) }).Run(args);
}
