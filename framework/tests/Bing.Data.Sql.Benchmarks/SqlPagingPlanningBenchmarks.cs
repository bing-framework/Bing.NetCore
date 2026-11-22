using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>测量含旧分页参数的冻结查询派生成本。</summary>
[MemoryDiagnoser]
public class SqlPagingPlanningBenchmarks
{
    private SqlQueryPlan _simple;
    private SqlQueryPlan _grouped;
    private SqlQueryPlan _unpagedSimple;
    private SqlQueryPlan _unpagedGrouped;
    private SqlQueryPlan _unordered;
    private SqlQueryPlan _cte;
    private SqlQueryPlan _set;
    private SqlQueryPlan _skipOnly;
    private readonly Pager _pager = new(2, 3, "Id");
    private readonly Pager _newOrderPager = new(2, 3, "Name Desc");
    private readonly PageBuilderSource _builderSource = new();

    /// <summary>筛选参数的数量。</summary>
    [Params(1, 8)]
    public int Filters { get; set; }

    /// <summary>冻结普通和分组查询，并在测量前核对完整 Count/Page SQL 与参数。</summary>
    [GlobalSetup]
    public void Setup()
    {
        var simple = CreateBuilder();
        var grouped = CreateBuilder().GroupBy("Name");
        _simple = SqlQueryPlan.Create(simple).Freeze();
        _grouped = SqlQueryPlan.Create(grouped).Freeze();
        _unpagedSimple = SqlQueryPlan.Create(CreateUnpagedBuilder()).Freeze();
        _unpagedGrouped = SqlQueryPlan.Create(CreateUnpagedBuilder().GroupBy("Name")).Freeze();
        _unordered = SqlQueryPlan.Create(CreateUnorderedBuilder()).Freeze();
        _cte = CteFreeze();
        _set = SetFreeze();
        _skipOnly = SkipOnlyFreeze();

        var conditions = string.Join(" And ", Enumerable.Range(0, Filters)
            .Select(index => $"`Status{index}`=@_p_{index}"));
        var suffix = $"\r\nFrom `samples` \r\nWhere {conditions}";
        var filters = Enumerable.Range(0, Filters)
            .Select(index => ($"@_p_{index}", (object)index)).ToArray();
        var pageParameters = filters.Concat(new[]
        {
            ($"@_p_{Filters}", (object)3),
            ($"@_p_{Filters + 1}", (object)3)
        }).ToArray();
        var simpleCountSql = $"Select Count(*) {suffix}";
        var groupedCountSql = $"Select Count(*) \r\nFrom (Select `Name` {suffix} \r\nGroup By `Name`) As `t`";
        var simplePageSql = $"Select `Name` {suffix} \r\nOrder By `Id` \r\nLimit @_p_{Filters + 1} OFFSET @_p_{Filters}";
        var groupedPageSql = $"Select `Name` {suffix} \r\nGroup By `Name` \r\nOrder By `Id` \r\nLimit @_p_{Filters + 1} OFFSET @_p_{Filters}";

        AssertPlan(SqlBuilderRuntimeBridge.CreateCountPlan(_simple), simpleCountSql, filters);
        AssertPlan(SqlBuilderRuntimeBridge.CreateCountPlan(_grouped), groupedCountSql, filters);
        AssertPlan(SqlBuilderRuntimeBridge.CreatePagePlan(_simple, _pager, new SqliteBuilder()), simplePageSql,
            pageParameters);
        AssertPlan(SqlBuilderRuntimeBridge.CreatePagePlan(_grouped, _pager, new SqliteBuilder()), groupedPageSql,
            pageParameters);
        AssertPlan(SqlBuilderRuntimeBridge.CreatePageExecutionPlan(_simple, _pager, _builderSource), simplePageSql,
            pageParameters);
        AssertPlan(SqlBuilderRuntimeBridge.CreatePageExecutionPlan(_grouped, _pager, _builderSource), groupedPageSql,
            pageParameters);
        AssertPlan(UnpagedSimplePage(), simplePageSql, pageParameters);
        AssertPlan(UnpagedGroupedPage(), groupedPageSql, pageParameters);
        AssertPlan(UnorderedPage(), $"Select `Name` {suffix} \r\nOrder By `Name` Desc \r\nLimit @_p_{Filters + 1} OFFSET @_p_{Filters}", pageParameters);
        AssertPlan(AppendedOrderPage(), $"Select `Name` {suffix} \r\nOrder By `Id`,`Name` Desc \r\nLimit @_p_{Filters + 1} OFFSET @_p_{Filters}", pageParameters);
        if (_builderSource.CreatedBuilders != 0)
            throw new InvalidOperationException("结构化分页不能提前创建 Provider Builder。");

        AssertPlan(SkipOnlyPage(), simplePageSql, pageParameters);

        var ctePrefix = $"With Recursive `filtered` \r\nAs (Select `Name` {suffix})\r\n";
        AssertPlan(CteCount(), ctePrefix + "Select Count(*) \r\nFrom `filtered`", filters);
        AssertPlan(CtePage(), ctePrefix + $"Select `Name` \r\nFrom `filtered` \r\nOrder By `Name` Desc \r\nLimit @_p_{Filters + 1} OFFSET @_p_{Filters}", pageParameters);
        var branchConditions = string.Join(" And ", Enumerable.Range(0, Filters)
            .Select(index => $"`Status{index}`=@_p_{index + Filters}"));
        var setBody = $"Select `Name` {suffix} \r\nUnion All \r\nSelect `Name` \r\nFrom `samples` \r\nWhere {branchConditions}";
        var setParameters = filters.Concat(filters.Select(item =>
            ($"@_p_{int.Parse(item.Item1.Substring(4)) + Filters}", (object)((int)item.Item2 + 1000)))).ToArray();
        AssertPlan(SetCount(), $"Select Count(*) \r\nFrom ({setBody}) As `t`", setParameters);
        AssertPlan(SetPage(), setBody + $" \r\nOrder By `Name` Desc \r\nLimit @_p_{2 * Filters + 1} OFFSET @_p_{2 * Filters}",
            setParameters.Concat(new[] { ($"@_p_{2 * Filters}", (object)3), ($"@_p_{2 * Filters + 1}", (object)3) }).ToArray());
    }

    /// <summary>测量普通计数派生。</summary>
    [Benchmark]
    public SqlQueryPlan SimpleCount() => SqlBuilderRuntimeBridge.CreateCountPlan(_simple);

    /// <summary>测量分组查询的包装计数派生。</summary>
    [Benchmark]
    public SqlQueryPlan GroupedCount() => SqlBuilderRuntimeBridge.CreateCountPlan(_grouped);

    /// <summary>测量普通查询的分页派生。</summary>
    [Benchmark]
    public SqlQueryPlan SimplePage() => SqlBuilderRuntimeBridge.CreatePagePlan(_simple, _pager, new SqliteBuilder());

    /// <summary>测量分组查询的分页派生。</summary>
    [Benchmark]
    public SqlQueryPlan GroupedPage() => SqlBuilderRuntimeBridge.CreatePagePlan(_grouped, _pager, new SqliteBuilder());

    /// <summary>测量普通查询按需创建 Builder 的真实执行规划入口。</summary>
    [Benchmark]
    public SqlQueryPlan SimplePageExecution() => SqlBuilderRuntimeBridge.CreatePageExecutionPlan(_simple, _pager, _builderSource);

    /// <summary>测量分组查询按需创建 Builder 的真实执行规划入口。</summary>
    [Benchmark]
    public SqlQueryPlan GroupedPageExecution() => SqlBuilderRuntimeBridge.CreatePageExecutionPlan(_grouped, _pager, _builderSource);

    /// <summary>测量未分页的普通查询新增分页。</summary>
    [Benchmark]
    public SqlQueryPlan UnpagedSimplePage() => SqlBuilderRuntimeBridge.CreatePageExecutionPlan(_unpagedSimple, _pager, _builderSource);

    /// <summary>测量未分页的分组查询新增分页。</summary>
    [Benchmark]
    public SqlQueryPlan UnpagedGroupedPage() => SqlBuilderRuntimeBridge.CreatePageExecutionPlan(_unpagedGrouped, _pager, _builderSource);

    /// <summary>包含首次捕获的成本，防止派生优化掩盖冻结开销。</summary>
    [Benchmark]
    public SqlQueryPlan UnpagedFreeze() => SqlQueryPlan.Create(CreateUnpagedBuilder()).Freeze();

    /// <summary>测量已有 offset、尚未 Take 的冻结查询完成分页。</summary>
    [Benchmark]
    public SqlQueryPlan SkipOnlyPage() => SqlBuilderRuntimeBridge.CreatePageExecutionPlan(_skipOnly, _pager, _builderSource);

    /// <summary>测量 Skip-only 首次冻结与保留规划状态的分配。</summary>
    [Benchmark]
    public SqlQueryPlan SkipOnlyFreeze() => SqlQueryPlan.Create(CreateUnpagedBuilder().Skip(5)).Freeze();

    /// <summary>测量没有排序的冻结查询按请求追加排序与分页。</summary>
    [Benchmark]
    public SqlQueryPlan UnorderedPage() => SqlBuilderRuntimeBridge.CreatePageExecutionPlan(_unordered, _newOrderPager, _builderSource);

    /// <summary>测量已有页面追加其他排序列，并复用既有分页参数。</summary>
    [Benchmark]
    public SqlQueryPlan AppendedOrderPage() => SqlBuilderRuntimeBridge.CreatePageExecutionPlan(_simple, _newOrderPager, _builderSource);

    /// <summary>测量 CTE 主查询计数，包含其参数快照。</summary>
    [Benchmark]
    public SqlQueryPlan CteCount() => SqlBuilderRuntimeBridge.CreateCountPlan(_cte);

    /// <summary>测量 CTE 主查询新增排序和分页。</summary>
    [Benchmark]
    public SqlQueryPlan CtePage() => SqlBuilderRuntimeBridge.CreatePagePlan(_cte, _newOrderPager);

    /// <summary>计入 CTE 树首次冻结成本与保留对象的分配。</summary>
    [Benchmark]
    public SqlQueryPlan CteFreeze() => SqlQueryPlan.Create(new SqliteBuilder().Select("Name")
        .From("filtered").With("filtered", CreateUnorderedBuilder())).Freeze();

    /// <summary>集合查询 Count，包含参数快照。</summary>
    [Benchmark]
    public SqlQueryPlan SetCount() => SqlBuilderRuntimeBridge.CreateCountPlan(_set);

    /// <summary>集合查询按请求追加排序与分页。</summary>
    [Benchmark]
    public SqlQueryPlan SetPage() => SqlBuilderRuntimeBridge.CreatePagePlan(_set, _newOrderPager);

    /// <summary>单独测量首次创建和冻结集合查询的代价。</summary>
    [Benchmark]
    public SqlQueryPlan SetFreeze() => SqlQueryPlan.Create(CreateUnorderedBuilder().UnionAll(CreateUnorderedBuilder(1000))).Freeze();

    private static void AssertPlan(SqlQueryPlan plan, string expectedSql, (string Name, object Value)[] expectedParameters)
    {
        if (plan.CommandText != expectedSql)
            throw new InvalidOperationException($"分页规划基准 SQL 与预期不一致。\r\n预期：{expectedSql}\r\n实际：{plan.CommandText}");

        if (plan.Parameters is not SqlParam[] parameters || parameters.Length != expectedParameters.Length)
            throw new InvalidOperationException("分页规划基准的参数数量或类型与预期不一致。");

        for (var index = 0; index < parameters.Length; index++)
        {
            var expected = expectedParameters[index];
            if (parameters[index].Name != expected.Name || !Equals(parameters[index].Value, expected.Value))
                throw new InvalidOperationException(
                    $"分页规划基准参数 {index} 与预期不一致：{parameters[index].Name}={parameters[index].Value}。");
        }
    }

    private ISqlBuilder CreateBuilder()
        => CreateUnpagedBuilder().Skip(5).Take(2);

    private ISqlBuilder CreateUnpagedBuilder()
        => CreateUnorderedBuilder().OrderBy("Id");

    private ISqlBuilder CreateUnorderedBuilder(int valueOffset = 0)
    {
        ISqlBuilder builder = new SqliteBuilder().Select("Name").From("samples");
        for (var index = 0; index < Filters; index++)
            builder.Where($"Status{index}", index + valueOffset);
        return builder;
    }

    private sealed class PageBuilderSource : ISqlQueryBuilderSource
    {
        internal int CreatedBuilders { get; private set; }

        public ISqlBuilder CreateIndependentSqlBuilder()
        {
            CreatedBuilders++;
            return new SqliteBuilder();
        }
    }
}
