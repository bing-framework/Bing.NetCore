using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>测量需要保留第三方 SQL 扩展规划来源时，首次分页的分配成本。</summary>
[MemoryDiagnoser]
public class SqlFallbackPlanningBenchmarks
{
    private SqlQueryPlan _source;
    private readonly Pager _pager = new(2, 3, "Name Desc");

    /// <summary>普通条件及参数数量。</summary>
    [Params(1, 8)]
    public int Conditions { get; set; }

    /// <summary>核对回退 SQL 和参数，保证前后版本比较相同查询。</summary>
    [GlobalSetup]
    public void Setup()
    {
        var builder = new TailBuilder().Select("Name").From("samples");
        for (var index = 0; index < Conditions; index++)
            builder.Where($"Status{index}", index);
        _source = SqlQueryPlan.Create(builder).Freeze();
        var plan = Page();
        var expected = "Select `Name` \r\nFrom `samples` \r\nWhere " +
            string.Join(" And ", Enumerable.Range(0, Conditions).Select(index => $"`Status{index}`=@_p_{index}")) +
            $" \r\nOrder By `Name` Desc \r\nLimit @_p_{Conditions + 1} OFFSET @_p_{Conditions} /* extension */";
        if (plan.CommandText != expected)
            throw new InvalidOperationException("回退分页的完整 SQL 不一致：" + plan.CommandText);
        var parameters = (SqlParam[])plan.Parameters;
        if (parameters.Length != Conditions + 2 ||
            !parameters.Select(parameter => parameter.Value).SequenceEqual(
                Enumerable.Range(0, Conditions).Cast<object>().Concat(new object[] { 3, 3 })))
            throw new InvalidOperationException("回退分页的参数不一致。");
    }

    /// <summary>从不可变查询描述派生首个页面，包含必要的兼容规划来源。</summary>
    [Benchmark]
    public SqlQueryPlan Page() => SqlBuilderRuntimeBridge.CreatePagePlan(_source, _pager);

    private sealed class TailBuilder : SqliteBuilder
    {
        internal TailBuilder(IParameterManager parameters = null) : base(parameterManager: parameters) { }
        public override string ToSql() => base.ToSql() + " /* extension */";
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) => new TailBuilder(parameterManager);
    }
}
