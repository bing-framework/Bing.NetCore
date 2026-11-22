using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>区分共享 CTE 定义的创建成本与重复渲染成本，不包含数据库 I/O。</summary>
[MemoryDiagnoser]
public class SqlCteDefinitionBenchmarks
{
    private SqlQueryFragment _fragment;
    private SqlCteDefinition _definition;
    private readonly SqlQueryScope _scope = new("shared");

    /// <summary>共享定义包含的参数数量。</summary>
    [Params(1, 8)]
    public int Parameters { get; set; }

    /// <summary>测量前核对完整 SQL、参数名称和值。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _fragment = new SqlQueryFragment("select " + string.Join(", ", Enumerable.Range(0, Parameters)
            .Select(i => $"@p{i} as Id{i}")), Enumerable.Range(0, Parameters).Select(i => new SqlParam($"@p{i}", i)));
        _definition = CreateDefinition();
        var rendered = RenderSharedDefinition();
        var expected = "items AS (select " + string.Join(", ", Enumerable.Range(0, Parameters)
            .Select(i => $"@shared_p{i} as Id{i}")) + ")";
        if (rendered.Sql != expected || !rendered.Parameters.Select(p => p.Value)
                .SequenceEqual(Enumerable.Range(0, Parameters).Cast<object>()) ||
            !rendered.Parameters.Select(p => p.Name).SequenceEqual(Enumerable.Range(0, Parameters).Select(i => $"shared_p{i}")))
            throw new InvalidOperationException("CTE 定义渲染的 SQL 或参数不一致。");
    }

    /// <summary>定义一次创建时的冻结成本。</summary>
    [Benchmark]
    public SqlCteDefinition CreateDefinition() => new("items", _fragment);

    /// <summary>同一定义在请求的独立参数作用域中重复渲染。</summary>
    [Benchmark]
    public SqlQueryFragment RenderSharedDefinition() => _definition.Render(_scope);
}
