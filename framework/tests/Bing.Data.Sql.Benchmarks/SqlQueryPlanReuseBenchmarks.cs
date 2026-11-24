using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>比较默认 Fluent 查询的首次终结与重复终结，不执行数据库命令。</summary>
[MemoryDiagnoser]
public class SqlQueryPlanReuseBenchmarks
{
    private ServiceProvider _services;
    private SqliteConnection _connection;
    private SqliteSqlQuery _root;
    private SqlQuery _query;
    private ISqlBuilder _publicBuilder;
    private SqlQueryPlan _frozenPlan;
    private string _expectedPlanSql;

    /// <summary>冻结查询的条件数。</summary>
    [Params(1, 8)]
    public int Conditions { get; set; }

    /// <summary>预热重复入口并断言两种入口的完整 SQL 与参数。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _services = new ServiceCollection().AddSqliteProvider().BuildServiceProvider();
        _connection = new SqliteConnection("Data Source=:memory:");
        _root = new SqliteSqlQuery(_services, new SqlOptions<SqliteSqlQuery>
        {
            DatabaseType = DatabaseType.Sqlite, Connection = _connection
        });
        _query = CreateQuery();
        _expectedPlanSql = "Select `Name` \r\nFrom `samples` \r\nWhere " +
            string.Join(" And ", Enumerable.Range(0, Conditions).Select(i => $"`Status{i}`=@_p_{i}"));
        foreach (var plan in new[] { FirstTerminal(), RepeatedTerminal(), RepeatedTerminal() })
        {
            if (plan.CommandText != _expectedPlanSql || !((SqlParam[])plan.Parameters).Select(p => p.Value)
                    .SequenceEqual(Enumerable.Range(0, Conditions).Cast<object>()))
                throw new InvalidOperationException("默认终结计划的 SQL 或参数不一致。");
        }

        _publicBuilder = CreateBuilder();
        _frozenPlan = SqlQueryPlan.Create(_publicBuilder).Freeze();
        ValidateFrozenPlan(_frozenPlan, _expectedPlanSql, Conditions);
        if (!ReferenceEquals(_frozenPlan, _frozenPlan.Freeze()))
            throw new InvalidOperationException("冻结计划的 Freeze 必须返回同一实例。");
    }

    /// <summary>包括构造草稿、首次隔离与生成 IR 的成本。</summary>
    [Benchmark]
    public SqlQueryPlan FirstTerminal() => CreateQuery().GetPlan();

    /// <summary>从已经冻结的默认查询创建下一次独立执行计划。</summary>
    [Benchmark]
    public SqlQueryPlan RepeatedTerminal() => _query.GetPlan(forPaging: true);

    /// <summary>测量公开 Builder 工厂创建后再冻结的路径。</summary>
    [Benchmark]
    public SqlQueryPlan PublicFactoryFreeze() => SqlQueryPlan.Create(_publicBuilder).Freeze();

    /// <summary>测量已经冻结的计划重复调用 Freeze 的路径。</summary>
    [Benchmark]
    public SqlQueryPlan FrozenPlanFreeze() => _frozenPlan.Freeze();

    /// <summary>首次冻结后由调用方保留查询，覆盖结构来源替换路径；分配量不等于驻留堆大小。</summary>
    [Benchmark]
    public object FirstTerminalRetainingQuery()
    {
        var query = CreateQuery();
        query.GetPlan();
        return query;
    }

    private SqlQuery CreateQuery()
    {
        return new SqlQuery((ISqlQueryPlanExecutor)_root, CreateBuilder());
    }

    private ISqlBuilder CreateBuilder()
    {
        var builder = new SqliteBuilder().Select("Name").From("samples");
        for (var i = 0; i < Conditions; i++) builder.Where($"Status{i}", i);
        return builder;
    }

    private static void ValidateFrozenPlan(SqlQueryPlan plan, string expectedSql, int conditions)
    {
        if (plan.IsBuilderPlan || plan.CommandText != expectedSql ||
            !((SqlParam[])plan.Parameters).Select(parameter => parameter.Value)
                .SequenceEqual(Enumerable.Range(0, conditions).Cast<object>()))
            throw new InvalidOperationException("冻结计划的完整 SQL 或参数不一致。");
    }

    /// <summary>运行固定分配采样并写入调用方指定的 UTF-8 JSON 文件。</summary>
    internal static int RunAllocationProbe(string output)
    {
        const int warmupIterations = 2048;
        const int sampleCount = 5;
        const int measuredIterations = 5000;
        var results = new List<object>();

        foreach (var conditions in new[] { 1, 8 })
        {
            var benchmark = new SqlQueryPlanReuseBenchmarks { Conditions = conditions };
            benchmark.Setup();
            try
            {
                foreach (var scenario in new[] { "PublicFactoryFreeze", "FrozenPlanFreeze" })
                {
                    Func<SqlQueryPlan> action = scenario == "PublicFactoryFreeze"
                        ? benchmark.PublicFactoryFreeze
                        : benchmark.FrozenPlanFreeze;
                    SqlQueryPlan last = null;
                    for (var index = 0; index < warmupIterations; index++)
                        last = action();

                    var samples = new double[sampleCount];
                    for (var sample = 0; sample < sampleCount; sample++)
                    {
                        var before = GC.GetAllocatedBytesForCurrentThread();
                        for (var index = 0; index < measuredIterations; index++)
                            last = action();
                        samples[sample] =
                            (GC.GetAllocatedBytesForCurrentThread() - before) / (double)measuredIterations;
                    }

                    GC.KeepAlive(last);
                    var ordered = samples.OrderBy(value => value).ToArray();
                    results.Add(new
                    {
                        conditions,
                        scenario,
                        allocatedBytesPerOperationSamples = samples,
                        medianAllocatedBytesPerOperation = ordered[ordered.Length / 2]
                    });
                }
            }
            finally
            {
                benchmark.Cleanup();
            }
        }

        var report = new
        {
            probe = "SqlQueryPlanFreezeAllocation-v1",
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

    /// <summary>释放未打开的本地资源。</summary>
    [GlobalCleanup]
    public void Cleanup() { _root.Dispose(); _connection.Dispose(); _services.Dispose(); }
}
