using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 调试 SQL 参数替换性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlDebugSqlBenchmarks
{
    /// <summary>
    /// 待测 Builder。
    /// </summary>
    private MySqlBuilder _builder;

    /// <summary>
    /// 已生成的 SQL 文本。
    /// </summary>
    private string _sql;

    /// <summary>
    /// 参数数量。
    /// </summary>
    [Params(10, 100, 1000)]
    public int ParameterCount { get; set; }

    /// <summary>
    /// 初始化指定参数数量的稳定查询。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = new MySqlBuilder().Select("*").From("orders");
        for (var index = 0; index < ParameterCount; index++)
        {
            _builder.Where($"Value{index}", index);
            _builder.AddParam($"diagnostic_{index}", index);
        }
        _sql = _builder.ToSql();
    }

    /// <summary>
    /// 测量对同一 SQL 与参数快照生成调试文本的耗时。
    /// </summary>
    /// <returns>参数已替换后的调试 SQL。</returns>
    [Benchmark]
    public string RenderDebugSql() => _builder.ToDebugSql(_sql);
}