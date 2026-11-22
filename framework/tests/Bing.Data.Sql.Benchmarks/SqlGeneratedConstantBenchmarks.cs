using System;
using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>比较常量桥接读取与生成代码直接传参的局部成本。</summary>
/// <remarks>表达式树在准备阶段创建，不测量完整查询、SQL 渲染或数据库执行。</remarks>
[MemoryDiagnoser]
public class SqlGeneratedConstantBenchmarks
{
    private static readonly object CachedValue = 7;
    private Expression<Func<ConstantRow, bool>> _predicate;

    /// <summary>准备旧路径所读取的常量表达式并校验两种输入一致。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _predicate = row => row.Id >= 7;
        if (!Equals(ReadExpression(), DirectConstant()) || !Equals(ReadExpression(), CachedConstant()))
            throw new InvalidOperationException("比较常量不一致。");
    }

    /// <summary>模拟原先的普通生成模式从表达式树读取常量。</summary>
    [Benchmark(Baseline = true)]
    public object ReadExpression() => SqlGeneratedQueryBridge.ComparisonValue(_predicate);

    /// <summary>模拟直接传入值类型常量时逐次装箱。</summary>
    [Benchmark]
    public object DirectConstant() => 7;

    /// <summary>模拟当前生成代码复用只读装箱常量。</summary>
    [Benchmark]
    public object CachedConstant() => CachedValue;

    /// <summary>基准使用的单列表。</summary>
    public sealed class ConstantRow
    {
        /// <summary>获取或设置比较列。</summary>
        public int Id { get; set; }
    }
}
