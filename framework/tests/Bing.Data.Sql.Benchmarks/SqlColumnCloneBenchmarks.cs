using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 测量列集合与 SQLite Builder 克隆的成本。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlColumnCloneBenchmarks
{
    private ColumnCollection _columns;
    private SqliteBuilder _builder;
    private string _expectedColumnSql;
    private string _expectedBuilderSql;

    /// <summary>
    /// 获取或设置投影列数量。
    /// </summary>
    [Params(1, 32, 128)]
    public int ColumnCount { get; set; }

    /// <summary>
    /// 初始化投影并校验克隆内容和源对象隔离。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _expectedColumnSql = CreateExpectedColumnSql(ColumnCount);
        _columns = CreateColumns(ColumnCount);
        var sourceColumnSql = RenderColumns(_columns);
        if (sourceColumnSql != _expectedColumnSql)
            throw new InvalidOperationException("列集合基准的完整投影 SQL 不符合预期。");

        var columnClone = _columns.Clone();
        if (RenderColumns(columnClone) != sourceColumnSql || columnClone.Count != ColumnCount)
            throw new InvalidOperationException("列集合克隆未保留完整投影 SQL。");
        if (columnClone[0].AggregateFunction != SqlAggregateFunction.Count ||
            columnClone[0].ColumnAlias != "TotalCount" || columnClone[0].TableAlias != "t")
            throw new InvalidOperationException("列集合克隆未保留聚合、列别名或表别名。");

        columnClone.RemoveColumns("Id", "t");
        columnClone.AddColumn(ColumnItem.CreateAggregate(SqlAggregateFunction.Count, "CloneOnly", "t", "CloneAlias"));
        if (_columns[0].Name != "Id" || _columns[0].ColumnAlias != "TotalCount" ||
            RenderColumns(_columns) != sourceColumnSql || RenderColumns(columnClone) == sourceColumnSql)
            throw new InvalidOperationException("列集合克隆修改影响了来源对象，或未隔离修改。");

        _expectedBuilderSql = CreateExpectedBuilderSql(ColumnCount);
        _builder = CreateBuilder(ColumnCount);
        var sourceBuilderSql = _builder.ToSql();
        if (sourceBuilderSql != _expectedBuilderSql)
            throw new InvalidOperationException("SQLite Builder 基准的完整 SQL 不符合预期。");

        var builderClone = (SqliteBuilder)_builder.Clone();
        if (builderClone.ToSql() != sourceBuilderSql)
            throw new InvalidOperationException("SQLite Builder 克隆未保留完整 SQL。");
        builderClone.Select("CloneOnly");
        if (_builder.ToSql() != sourceBuilderSql || builderClone.ToSql() == sourceBuilderSql)
            throw new InvalidOperationException("SQLite Builder 克隆修改影响了来源对象，或未隔离修改。");
    }

    /// <summary>
    /// 测量 ColumnCollection.Clone。
    /// </summary>
    [Benchmark]
    public ColumnCollection CloneColumnCollection() => _columns.Clone();

    /// <summary>
    /// 测量 SQLite Builder.Clone。
    /// </summary>
    [Benchmark]
    public ISqlBuilder CloneSqliteBuilder() => _builder.Clone();

    private static ColumnCollection CreateColumns(int count)
    {
        var columns = new ColumnCollection();
        columns.AddColumn(ColumnItem.CreateAggregate(SqlAggregateFunction.Count, "Id", "t", "TotalCount"));
        for (var index = 1; index < count; index++)
            columns.AddColumn(ColumnItem.CreateColumn($"Column{index:D3}", "t", $"Alias{index:D3}"));
        return columns;
    }

    private static string CreateExpectedColumnSql(int count)
    {
        var columns = new string[count];
        columns[0] = "Count(`t`.`Id`) As `TotalCount`";
        for (var index = 1; index < count; index++)
            columns[index] = $"`t`.`Column{index:D3}` As `Alias{index:D3}`";
        return string.Join(",", columns);
    }

    private static string RenderColumns(ColumnCollection columns) =>
        columns.ToSql(SqliteSqlProvider.Instance.Dialect, null);

    private static SqliteBuilder CreateBuilder(int count)
    {
        var builder = new SqliteBuilder();
        builder.CountColumn("t.Id", "TotalCount");
        if (count > 1)
        {
            var columns = string.Join(",", Enumerable.Range(1, count - 1)
                .Select(index => $"t.Column{index:D3} As Alias{index:D3}"));
            builder.Select(columns);
        }
        builder.From("Orders", "t");
        return builder;
    }

    private static string CreateExpectedBuilderSql(int count) =>
        $"Select {CreateExpectedColumnSql(count)} {Environment.NewLine}From `Orders` As `t`";
}

/// <summary>
/// 以固定工作量记录列集合与 Builder 克隆的托管分配。
/// </summary>
internal static class SqlColumnCloneAllocationProbe
{
    internal static int Run(string output)
    {
        const int warmups = 2048;
        const int samples = 5;
        const int iterations = 2000;
        var results = new List<object>();

        foreach (var count in new[] { 1, 32, 128 })
        {
            var benchmark = new SqlColumnCloneBenchmarks { ColumnCount = count };
            benchmark.Setup();
            var operations = new (string Name, Func<object> Action)[]
            {
                ("column-collection-clone", () => benchmark.CloneColumnCollection()),
                ("sqlite-builder-clone", () => benchmark.CloneSqliteBuilder())
            };

            foreach (var operation in operations)
            {
                for (var index = 0; index < warmups; index++)
                    _ = operation.Action() ?? throw new InvalidOperationException("Clone returned null.");

                for (var sample = 0; sample < samples; sample++)
                {
                    object last = null;
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    var timer = Stopwatch.StartNew();
                    for (var index = 0; index < iterations; index++)
                        last = operation.Action();
                    timer.Stop();
                    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    if (last == null)
                        throw new InvalidOperationException("Clone returned null.");
                    results.Add(new
                    {
                        columnCount = count,
                        operation = operation.Name,
                        sample,
                        operations = iterations,
                        nsPerOperation = timer.Elapsed.TotalNanoseconds / iterations,
                        bytesPerOperation = (double)allocated / iterations
                    });
                }
            }
        }

        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            workload = "SqlColumnCloneAllocationProbe-v1",
            framework = RuntimeInformation.FrameworkDescription,
            runtimeVersion = Environment.Version.ToString(),
            runtimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            warmups,
            samples,
            iterations,
            comparison = "Fixed same-process managed allocation probe; no timing or release conclusion",
            results
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return 0;
    }
}
