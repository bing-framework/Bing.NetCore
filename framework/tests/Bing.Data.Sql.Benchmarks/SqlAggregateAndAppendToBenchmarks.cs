using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 统一聚合 SQL 渲染性能基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlAggregateRenderingBenchmarks
{
    private MySqlBuilder _countBuilder;
    private MySqlBuilder _aggregateBuilder;
    private MySqlBuilder _expressionAggregateBuilder;
    private MySqlBuilder _rawJsonPathBuilder;
    private MySqlBuilder _expressionArithmeticBuilder;
    private MySqlBuilder _expressionCaseBuilder;
    private MySqlBuilder _countDistinctColumnBuilder;
    private MySqlBuilder _aggregateExpressionBuilder;

    /// <summary>
    /// 初始化稳定的聚合查询状态。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _countBuilder = new MySqlBuilder();
        _countBuilder.Count(alias: "Total").From("orders");

        _aggregateBuilder = new MySqlBuilder();
        _aggregateBuilder.Count("o.Id", "Count")
            .Sum("o.Amount", "Sum")
            .Avg("o.Amount", "Average", distinct: true)
            .Max("o.Amount", "Maximum", distinct: true)
            .Min("o.Amount", "Minimum")
            .Count("o.UserId", "DistinctUsers", distinct: true)
            .Sum("o.Amount", "DistinctSum", distinct: true)
            .Avg("o.Amount", "DistinctAverage", distinct: true)
            .Max("o.Amount", "DistinctMaximum")
            .Min("o.Amount", "DistinctMinimum", distinct: true)
            .From("orders", "o")
            .GroupBy("o.CategoryId", "Count(o.Id)>0");

        _expressionAggregateBuilder = new MySqlBuilder();
        _expressionAggregateBuilder.AggregateExpression(SqlAggregateFunction.Count,
                "Case When [o].[Enabled]=1 Then [o].[Id] End", "EnabledCount", distinct: true)
            .AggregateExpression(SqlAggregateFunction.Sum, "[o].[Amount]*[o].[Quantity]", "GrossAmount")
            .From("orders", "o");

        _rawJsonPathBuilder = new MySqlBuilder();
        _rawJsonPathBuilder.AggregateRaw(SqlAggregateFunction.Count, "JsonExtract(o.Data, '$[0]')", "JsonCount")
            .From("orders", "o");

        _expressionArithmeticBuilder = new MySqlBuilder();
        _expressionArithmeticBuilder.AggregateExpression(SqlAggregateFunction.Sum, "[o].[Amount]*[o].[Quantity]",
            "GrossAmount").From("orders", "o");

        _expressionCaseBuilder = new MySqlBuilder();
        _expressionCaseBuilder.AggregateExpression(SqlAggregateFunction.Count,
            "Case When [o].[Enabled]=1 Then [o].[Id] End", "EnabledCount", distinct: true).From("orders", "o");

        _countDistinctColumnBuilder = new MySqlBuilder();
        _countDistinctColumnBuilder.Count("o.UserId", "DistinctUsers", distinct: true).From("orders", "o");

        _aggregateExpressionBuilder = new MySqlBuilder();
        _aggregateExpressionBuilder.AggregateExpression(SqlAggregateFunction.Count, "[o].[Id]", "Count")
            .AggregateExpression(SqlAggregateFunction.Sum, "[o].[Amount]", "Sum")
            .AggregateExpression(SqlAggregateFunction.Avg, "[o].[Amount]", "Average", distinct: true)
            .AggregateExpression(SqlAggregateFunction.Max, "[o].[Amount]", "Maximum", distinct: true)
            .AggregateExpression(SqlAggregateFunction.Min, "[o].[Amount]", "Minimum")
            .AggregateExpression(SqlAggregateFunction.Count, "[o].[UserId]", "DistinctUsers", distinct: true)
            .AggregateExpression(SqlAggregateFunction.Sum, "[o].[Amount]", "DistinctSum", distinct: true)
            .AggregateExpression(SqlAggregateFunction.Avg, "[o].[Amount]", "DistinctAverage", distinct: true)
            .AggregateExpression(SqlAggregateFunction.Max, "[o].[Amount]", "DistinctMaximum")
            .AggregateExpression(SqlAggregateFunction.Min, "[o].[Amount]", "DistinctMinimum", distinct: true)
            .From("orders", "o")
            .GroupBy("o.CategoryId", "Count(o.Id)>0");
    }

    /// <summary>
    /// 测量 Count(*) 渲染。
    /// </summary>
    [Benchmark]
    public string RenderCountWildcard() => _countBuilder.ToSql();

    /// <summary>
    /// 测量十个普通和 Distinct 聚合的 ToSql 渲染。
    /// </summary>
    [Benchmark(Baseline = true)]
    public string RenderTenAggregates() => _aggregateBuilder.ToSql();

    /// <summary>
    /// 测量十个普通和 Distinct 聚合的直接追加渲染。
    /// </summary>
    [Benchmark]
    public int AppendTenAggregates()
    {
        var result = new StringBuilder(512);
        _aggregateBuilder.AppendTo(result);
        return result.Length;
    }

    /// <summary>
    /// 测量 Expression Case 和算术聚合渲染。
    /// </summary>
    [Benchmark]
    public string RenderExpressionAggregates() => _expressionAggregateBuilder.ToSql();

    /// <summary>
    /// 测量完全原样 JSON Path Raw 聚合渲染。
    /// </summary>
    [Benchmark]
    public string AggregateRaw_JsonPath() => _rawJsonPathBuilder.ToSql();

    /// <summary>
    /// 测量带标识符方言转换的算术聚合表达式渲染。
    /// </summary>
    [Benchmark]
    public string AggregateExpression_Arithmetic() => _expressionArithmeticBuilder.ToSql();

    /// <summary>
    /// 测量带标识符方言转换的 Case 聚合表达式渲染。
    /// </summary>
    [Benchmark]
    public string AggregateExpression_Case() => _expressionCaseBuilder.ToSql();

    /// <summary>
    /// 测量 Count Distinct 聚合渲染。
    /// </summary>
    [Benchmark]
    public string Count_DistinctColumn() => _countDistinctColumnBuilder.ToSql();

    /// <summary>
    /// 测量十个聚合表达式的 ToSql 渲染。
    /// </summary>
    [Benchmark]
    public string TenAggregateExpressions() => _aggregateExpressionBuilder.ToSql();

    /// <summary>
    /// 测量十个聚合表达式的 Clone 开销。
    /// </summary>
    [Benchmark]
    public ISqlBuilder Clone_TenAggregateExpressions() => _aggregateExpressionBuilder.Clone();

    /// <summary>
    /// 测量单个标识符 Expression 聚合的构造和解析。
    /// </summary>
    [Benchmark]
    public ISqlBuilder AggregateExpression_SimpleIdentifier() => CreateExpressionBuilder("[o].[Amount]");

    /// <summary>
    /// 测量包含 JSON Path 字符串的 Expression 聚合解析。
    /// </summary>
    [Benchmark]
    public ISqlBuilder AggregateExpression_JsonPathString() =>
        CreateExpressionBuilder("JsonExtract([o].[Data], '$[0].name') + [o].[Amount]");

    /// <summary>
    /// 测量包含方括号文本字符串的 Expression 聚合解析。
    /// </summary>
    [Benchmark]
    public ISqlBuilder AggregateExpression_StringBracketText() =>
        CreateExpressionBuilder("Case When [o].[Code]='[legacy]' Then [o].[Amount] Else 0 End");

    /// <summary>
    /// 测量包含行注释的 Expression 聚合解析。
    /// </summary>
    [Benchmark]
    public ISqlBuilder AggregateExpression_LineComment() =>
        CreateExpressionBuilder("[o].[Amount] -- [comment]\n + [o].[Tax]");

    /// <summary>
    /// 测量包含块注释的 Expression 聚合解析。
    /// </summary>
    [Benchmark]
    public ISqlBuilder AggregateExpression_BlockComment() =>
        CreateExpressionBuilder("[o].[Amount] /* [comment] */ + [o].[Tax]");

    /// <summary>
    /// 测量包含十个方括号标识符的 Expression 聚合解析。
    /// </summary>
    [Benchmark]
    public ISqlBuilder AggregateExpression_TenIdentifiers() => CreateExpressionBuilder(
        "[o].[A]+[o].[B]+[o].[C]+[o].[D]+[o].[E]+[o].[F]+[o].[G]+[o].[H]+[o].[I]+[o].[J]");

    /// <summary>
    /// 测量十个独立 Expression 聚合的构造和解析。
    /// </summary>
    [Benchmark]
    public ISqlBuilder AggregateExpression_TenExpressions()
    {
        var builder = new MySqlBuilder();
        for (var index = 0; index < 10; index++)
            builder.AggregateExpression(SqlAggregateFunction.Sum, $"[o].[Amount{index}] * [o].[Quantity{index}]",
                $"Total{index}");
        return builder.From("orders", "o");
    }

    /// <summary>
    /// 测量带显式参数的 Expression 聚合构造和解析。
    /// </summary>
    [Benchmark]
    public ISqlBuilder AggregateExpression_WithParameters() =>
        CreateExpressionBuilder("Case When [o].[Amount]>@MinAmount Then [o].[Amount] Else 0 End")
            .AddParam("MinAmount", 100);

    /// <summary>
    /// 测量带空格和转义结束符的结构化聚合标识符路径解析。
    /// </summary>
    [Benchmark]
    public ISqlBuilder QuotedIdentifier_WithSpaces() => new MySqlBuilder()
        .Aggregate(SqlAggregateFunction.Sum, "[Sales Order].[Order]]Name]", "Total")
        .From("orders");

    /// <summary>
    /// 创建单个 Expression 聚合基准 Builder。
    /// </summary>
    /// <param name="expression">聚合表达式。</param>
    /// <returns>已配置的 MySQL Builder。</returns>
    private static MySqlBuilder CreateExpressionBuilder(string expression) => new MySqlBuilder()
        .AggregateExpression(SqlAggregateFunction.Sum, expression, "Total")
        .From("orders", "o");
}

/// <summary>
/// SQL Builder AppendTo 渲染性能基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlBuilderAppendToBenchmarks
{
    private MySqlBuilder _simpleBuilder;
    private MySqlBuilder _complexBuilder;

    /// <summary>
    /// 初始化简单和复杂查询。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _simpleBuilder = new MySqlBuilder();
        _simpleBuilder.Select("o.Id,o.Name").From("orders", "o").Where("o.Enabled", true);
        _complexBuilder = CreateComplexBuilder();
    }

    /// <summary>
    /// 测量简单查询 ToSql 渲染。
    /// </summary>
    [Benchmark]
    public string RenderSimpleQuery() => _simpleBuilder.ToSql();

    /// <summary>
    /// 测量简单查询直接追加渲染。
    /// </summary>
    [Benchmark]
    public int AppendSimpleQuery() => Append(_simpleBuilder, 256);

    /// <summary>
    /// 测量复杂查询 ToSql 渲染。
    /// </summary>
    [Benchmark(Baseline = true)]
    public string RenderComplexQuery() => _complexBuilder.ToSql();

    /// <summary>
    /// 测量复杂查询直接追加渲染。
    /// </summary>
    [Benchmark]
    public int AppendComplexQuery() => Append(_complexBuilder, 1024);

    /// <summary>
    /// 测量十次 ToSql 重复渲染。
    /// </summary>
    [Benchmark]
    public string RenderComplexQueryTenTimes()
    {
        string sql = null;
        for (var index = 0; index < 10; index++)
            sql = _complexBuilder.ToSql();
        return sql;
    }

    /// <summary>
    /// 测量十次直接追加重复渲染。
    /// </summary>
    [Benchmark]
    public int AppendComplexQueryTenTimes()
    {
        var result = new StringBuilder(1024);
        for (var index = 0; index < 10; index++)
        {
            result.Clear();
            _complexBuilder.AppendTo(result);
        }
        return result.Length;
    }

    /// <summary>
    /// 创建含五个 Join、参数、Raw、子查询、CTE 和 Union 的查询。
    /// </summary>
    private static MySqlBuilder CreateComplexBuilder()
    {
        var cte = new MySqlBuilder();
        cte.Select("i.OrderId").From("order_items", "i").Where("i.Enabled", true);

        var union = new MySqlBuilder();
        union.Select("a.Id,a.Name").From("archived_orders", "a").Where("a.Enabled", true);

        var builder = new MySqlBuilder();
        builder.With("active_items", cte)
            .Select("o.Id,o.Name,c.Name As CustomerName")
            .From("orders", "o")
            .LeftJoin("customers", "c").AppendOn("c.Id=o.CustomerId")
            .LeftJoin("order_statuses", "s").AppendOn("s.Id=o.StatusId")
            .LeftJoin("order_payments", "p").AppendOn("p.OrderId=o.Id")
            .LeftJoin("order_shipments", "h").AppendOn("h.OrderId=o.Id")
            .LeftJoin("active_items", "i").AppendOn("i.OrderId=o.Id")
            .AppendWhere("o.Enabled=1")
            .Where("o.TenantId", "benchmark")
            .Where("o.CreatedTime", new DateTime(2026, 1, 1))
            .OrderBy("o.Id")
            .Skip(10)
            .Take(20)
            .UnionAll(union);
        return builder;
    }

    /// <summary>
    /// 追加一次 SQL 并返回长度，以防止基准运行时消除调用。
    /// </summary>
    private static int Append(MySqlBuilder builder, int capacity)
    {
        var result = new StringBuilder(capacity);
        builder.AppendTo(result);
        return result.Length;
    }
}

/// <summary>
/// CTE 快照与上下文感知参数扫描性能基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlBuilderCteAndParameterTokenBenchmarks
{
    private MySqlBuilder _debugBuilder;
    private string _debugSql;

    /// <summary>
    /// 初始化参数标记扫描基准使用的稳定 SQL。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _debugBuilder = new MySqlBuilder();
        _debugBuilder.AppendSelect("*").AppendFrom("orders")
            .AppendWhere("Code=@Code And Note='@Code' /* @Code */ And `@Code`=@Code")
            .AddParam("Code", "benchmark");
        _debugSql = _debugBuilder.ToSql();
    }

    /// <summary>
    /// 测量注册 CTE 时独立快照输入 Builder 并渲染复合查询的成本。
    /// </summary>
    /// <returns>包含 CTE 的 SQL 文本。</returns>
    [Benchmark]
    public string ConfigureCteSnapshotAndRender()
    {
        var cte = new MySqlBuilder();
        cte.Select("o.Id,o.CustomerId").From("orders", "o")
            .LeftJoin("order_items", "i").AppendOn("i.OrderId=o.Id")
            .Where("o.Enabled", true)
            .Where("i.Enabled", true);

        return new MySqlBuilder().With("active_orders", cte)
            .Select("Id,CustomerId")
            .From("active_orders")
            .ToSql();
    }

    /// <summary>
    /// 测量调试 SQL 替换时跳过字符串、注释和引用标识符内参数文本的成本。
    /// </summary>
    /// <returns>调试 SQL 文本。</returns>
    [Benchmark]
    public string RenderDebugSqlWithProtectedParameterContexts() => _debugBuilder.ToDebugSql(_debugSql);
}