using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Filters;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>测量查询模板创建、作用域渲染及统一查询 IR 派生成本。</summary>
[MemoryDiagnoser]
public class SqlQueryTemplateIrBenchmarks
{
    private SqliteBuilder _builder;
    private SqlQueryTemplate _template;
    private SqlQueryScope _scope;
    private SqlQueryPlan _templatePlan;
    private Pager _pager;
    private string _expectedSql;
    private bool _supportsTemplateIr;
    private SqliteBuilder _filteredBuilder;

    /// <summary>查询条件数量。</summary>
    [Params(1, 8)]
    public int Conditions { get; set; }

    /// <summary>准备可复用的 Builder、模板和 request scope，并校验完整 SQL 与参数。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = CreateBuilder();
        _expectedSql = CreateExpectedSql(Conditions);
        _scope = new SqlQueryScope("request");
        _pager = new Pager(2, 3, "Name");
        _filteredBuilder = new SqliteBuilder(new SqlBuilderServices(filters: new[] { new TenantFilter() }));
        _filteredBuilder.Select("Name").From<FilteredRow>("s");
        for (var index = 0; index < Conditions; index++)
            _filteredBuilder.Where($"Status{index}", index);
        var filteredSql = _expectedSql.Replace("From `samples`", "From `samples` As `s`", StringComparison.Ordinal) +
                          $" And `s`.`TenantId`=@_p_{Conditions}";
        var filteredParameters = CreateExpectedParameters(Conditions, scoped: false)
            .Concat(new[] { ($"@_p_{Conditions}", (object)7) }).ToArray();
        var filtered = FreezeFilteredSnapshot();
        AssertFragment(filtered.Sql, filtered.Parameters, filteredSql, filteredParameters);
        AssertPlan(FreezeFilteredPlan(), filteredSql, filteredParameters);

        _template = CreateTemplate();
        AssertFragment(_template.Sql, _template.Parameters, _expectedSql,
            CreateExpectedParameters(Conditions, scoped: false));

        var scoped = RenderScopedTemplate();
        AssertFragment(scoped.Sql, scoped.Parameters, CreateScopedSql(_expectedSql, Conditions),
            CreateExpectedParameters(Conditions, scoped: true));

        _templatePlan = CreatePlan();
        AssertPlan(_templatePlan, _expectedSql, CreateExpectedParameters(Conditions, scoped: false));
        _supportsTemplateIr = HasTemplateIr(_templatePlan);
        if (!_supportsTemplateIr)
            return;

        AssertPlan(DeriveCount(), CreateCountSql(Conditions),
            CreateExpectedParameters(Conditions, scoped: false));
        AssertPlan(DerivePage(), CreatePageSql(Conditions),
            CreatePageParameters(Conditions));
    }

    /// <summary>从已配置的 SQLite Builder 创建可复用模板。</summary>
    [Benchmark]
    public SqlQueryTemplate CreateTemplate() => _builder.ToTemplate();

    /// <summary>将共享模板渲染到预先创建的 request 参数作用域。</summary>
    [Benchmark]
    public SqlQueryFragment RenderScopedTemplate() => _template.Render(_scope);

    /// <summary>从模板创建脱离 Builder 的查询执行计划。</summary>
    [Benchmark]
    public SqlQueryPlan CreatePlan() => SqlQueryPlan.Create(_template);

    /// <summary>动态过滤在单次渲染中捕获值，测量完整执行快照分配。</summary>
    [Benchmark]
    public SqlBuilderExecutionSnapshot FreezeFilteredSnapshot() =>
        SqlBuilderRuntimeBridge.CreateExecutionSnapshot(_filteredBuilder);

    /// <summary>包含一次调用方草稿隔离的动态过滤计划。</summary>
    [Benchmark]
    public SqlQueryPlan FreezeFilteredPlan() => SqlQueryPlan.Create(_filteredBuilder);

    /// <summary>测量由查询模板 IR 直接派生 Count 的成本。</summary>
    [Benchmark]
    public SqlQueryPlan DeriveCount()
    {
        EnsureTemplateIrSupport();
        return SqlBuilderRuntimeBridge.CreateCountPlan(_templatePlan);
    }

    /// <summary>测量由查询模板 IR 直接派生 SQLite Page 的成本。</summary>
    [Benchmark]
    public SqlQueryPlan DerivePage()
    {
        EnsureTemplateIrSupport();
        return SqlBuilderRuntimeBridge.CreatePagePlan(_templatePlan, _pager);
    }

    /// <summary>运行固定分配采样；旧运行库不支持模板 IR 派生时会明确记录为 unsupported。</summary>
    internal static int RunAllocationProbe(string output)
    {
        const int warmupIterations = 2048;
        const int sampleCount = 5;
        const int measuredIterations = 2000;
        var results = new List<object>();

        foreach (var conditions in new[] { 1, 8 })
        {
            var benchmark = new SqlQueryTemplateIrBenchmarks { Conditions = conditions };
            benchmark.Setup();
            var scenarios = new (string Name, Func<object> Action)[]
            {
                (nameof(CreateTemplate), () => benchmark.CreateTemplate()),
                (nameof(RenderScopedTemplate), () => benchmark.RenderScopedTemplate()),
                (nameof(CreatePlan), () => benchmark.CreatePlan()),
                (nameof(FreezeFilteredSnapshot), () => benchmark.FreezeFilteredSnapshot()),
                (nameof(FreezeFilteredPlan), () => benchmark.FreezeFilteredPlan())
            };

            foreach (var scenario in scenarios)
                results.Add(Sample(conditions, scenario.Name, scenario.Action,
                    warmupIterations, sampleCount, measuredIterations));

            if (!benchmark._supportsTemplateIr)
            {
                results.Add(Unsupported(conditions, nameof(DeriveCount)));
                results.Add(Unsupported(conditions, nameof(DerivePage)));
                continue;
            }

            results.Add(Sample(conditions, nameof(DeriveCount), () => benchmark.DeriveCount(),
                warmupIterations, sampleCount, measuredIterations));
            results.Add(Sample(conditions, nameof(DerivePage), () => benchmark.DerivePage(),
                warmupIterations, sampleCount, measuredIterations));
        }

        var report = new
        {
            probe = "SqlQueryTemplateIrAllocation-v2",
            runtime = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            warmupIterations,
            sampleCount,
            measuredIterations,
            results
        };
        var fullOutputPath = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        File.WriteAllText(fullOutputPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        return 0;
    }

    private static object Sample(int conditions, string scenario, Func<object> action,
        int warmupIterations, int sampleCount, int measuredIterations)
    {
        object last = null;
        for (var index = 0; index < warmupIterations; index++)
            last = action();

        var samples = new double[sampleCount];
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < measuredIterations; index++)
                last = action();
            samples[sample] = (GC.GetAllocatedBytesForCurrentThread() - before) /
                              (double)measuredIterations;
        }

        GC.KeepAlive(last);
        var ordered = samples.OrderBy(value => value).ToArray();
        return new
        {
            conditions,
            scenario,
            status = "supported",
            allocatedBytesPerOperationSamples = samples,
            medianAllocatedBytesPerOperation = ordered[ordered.Length / 2]
        };
    }

    private static object Unsupported(int conditions, string scenario) => new
    {
        conditions,
        scenario,
        status = "unsupported",
        reason = "运行库未为 Builder 模板计划提供查询 IR；不使用文本回退冒充 IR 派生结果。"
    };

    private static bool HasTemplateIr(SqlQueryPlan plan)
    {
        // QueryIr 是实现细节，仅在 Setup 中用于区分新旧运行库，不进入测量热路径。
        var property = typeof(SqlQueryPlan).GetProperty("QueryIr",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return property?.GetValue(plan) != null;
    }

    private void EnsureTemplateIrSupport()
    {
        if (!_supportsTemplateIr)
            throw new NotSupportedException("当前运行库未提供模板 IR 派生能力。");
    }

    private SqliteBuilder CreateBuilder()
    {
        var builder = new SqliteBuilder();
        builder.Select("Name").From("samples");
        for (var index = 0; index < Conditions; index++)
            builder.Where($"Status{index}", index);
        return builder;
    }

    private static string CreateExpectedSql(int conditions) =>
        "Select `Name` \r\nFrom `samples` \r\nWhere " + string.Join(" And ",
            Enumerable.Range(0, conditions).Select(index => $"`Status{index}`=@_p_{index}"));

    private static string CreateScopedSql(string sql, int conditions)
    {
        for (var index = 0; index < conditions; index++)
            sql = sql.Replace($"@_p_{index}", $"@request_p_{index}", StringComparison.Ordinal);
        return sql;
    }

    private static string CreateCountSql(int conditions) =>
        "Select Count(*) \r\nFrom `samples` \r\nWhere " + string.Join(" And ",
            Enumerable.Range(0, conditions).Select(index => $"`Status{index}`=@_p_{index}"));

    private static string CreatePageSql(int conditions) =>
        CreateExpectedSql(conditions) + $" \r\nOrder By `Name` \r\nLimit @_p_{conditions + 1} OFFSET @_p_{conditions}";

    private static (string Name, object Value)[] CreateExpectedParameters(int conditions, bool scoped) =>
        Enumerable.Range(0, conditions)
            .Select(index => (scoped ? $"request_p_{index}" : $"@_p_{index}", (object)index))
            .ToArray();

    private static (string Name, object Value)[] CreatePageParameters(int conditions) =>
        CreateExpectedParameters(conditions, scoped: false)
            .Concat(new[]
            {
                ($"@_p_{conditions}", (object)3),
                ($"@_p_{conditions + 1}", (object)3)
            }).ToArray();

    private static void AssertFragment(string sql, IEnumerable<SqlParam> parameters,
        string expectedSql, (string Name, object Value)[] expectedParameters)
    {
        if (!string.Equals(sql, expectedSql, StringComparison.Ordinal))
            throw new InvalidOperationException($"查询模板 SQL 不一致。\r\n预期：{expectedSql}\r\n实际：{sql}");
        AssertParameters(parameters, expectedParameters);
    }

    private static void AssertPlan(SqlQueryPlan plan, string expectedSql,
        (string Name, object Value)[] expectedParameters)
    {
        if (!string.Equals(plan.CommandText, expectedSql, StringComparison.Ordinal))
            throw new InvalidOperationException($"查询计划 SQL 不一致。\r\n预期：{expectedSql}\r\n实际：{plan.CommandText}");
        if (plan.Parameters is not IEnumerable<SqlParam> parameters)
            throw new InvalidOperationException("查询计划参数不是 SqlParam 集合。");
        AssertParameters(parameters, expectedParameters);
    }

    private static void AssertParameters(IEnumerable<SqlParam> parameters,
        (string Name, object Value)[] expectedParameters)
    {
        var actual = parameters.ToArray();
        if (actual.Length != expectedParameters.Length)
            throw new InvalidOperationException("查询参数数量与预期不一致。");
        for (var index = 0; index < actual.Length; index++)
        {
            if (!string.Equals(actual[index].Name, expectedParameters[index].Name, StringComparison.Ordinal) ||
                !Equals(actual[index].Value, expectedParameters[index].Value))
                throw new InvalidOperationException(
                    $"查询参数 {index} 不一致：{actual[index].Name}={actual[index].Value}。");
        }
    }

    [Table("samples")]
    private sealed class FilteredRow
    {
        public int TenantId { get; set; }
    }

    private sealed class TenantFilter : ISqlFilter
    {
        public void Filter(SqlFilterContext context)
        {
            foreach (var source in context.Sources.Where(source => source.EntityType == typeof(FilteredRow)))
                context.AddPredicate(source, context.GetColumn(source, nameof(FilteredRow.TenantId)), 7);
        }
    }
}
