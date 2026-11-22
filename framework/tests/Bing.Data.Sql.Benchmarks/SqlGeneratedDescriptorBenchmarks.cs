using System;
using BenchmarkDotNet.Attributes;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>隔离测量生成调用点的固定逻辑描述创建与复用。</summary>
/// <remarks>不包含表达式树、Builder、SQL 渲染和数据库执行。</remarks>
[MemoryDiagnoser]
public class SqlGeneratedDescriptorBenchmarks
{
    private static readonly Type[] Types = { typeof(DescriptorRow) };
    private static readonly string[] Names = { "row" };
    private static readonly SqlGeneratedColumn Column = new(0, "Id", null, typeof(int));

    /// <summary>模拟原先比较条件逐次创建固定描述。</summary>
    [Benchmark(Baseline = true)]
    public (Type[], string[], SqlGeneratedColumn) CreateDescriptors() =>
        (new[] { typeof(DescriptorRow) }, new[] { "row" }, new SqlGeneratedColumn(0, "Id", null, typeof(int)));

    /// <summary>模拟生成代码复用固定描述。</summary>
    [Benchmark]
    public (Type[], string[], SqlGeneratedColumn) ReuseDescriptors() => (Types, Names, Column);

    /// <summary>描述符测试用实体。</summary>
    public sealed class DescriptorRow
    {
        /// <summary>获取或设置实体标识。</summary>
        public int Id { get; set; }
    }
}
