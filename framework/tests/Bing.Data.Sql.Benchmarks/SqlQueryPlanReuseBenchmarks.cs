using System;
using System.Linq;
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
        var expected = "Select `Name` \r\nFrom `samples` \r\nWhere " +
            string.Join(" And ", Enumerable.Range(0, Conditions).Select(i => $"`Status{i}`=@_p_{i}"));
        foreach (var plan in new[] { FirstTerminal(), RepeatedTerminal(), RepeatedTerminal() })
        {
            if (plan.CommandText != expected || !((SqlParam[])plan.Parameters).Select(p => p.Value)
                    .SequenceEqual(Enumerable.Range(0, Conditions).Cast<object>()))
                throw new InvalidOperationException("默认终结计划的 SQL 或参数不一致。");
        }
    }

    /// <summary>包括构造草稿、首次隔离与生成 IR 的成本。</summary>
    [Benchmark]
    public SqlQueryPlan FirstTerminal() => CreateQuery().GetPlan();

    /// <summary>从已经冻结的默认查询创建下一次独立执行计划。</summary>
    [Benchmark]
    public SqlQueryPlan RepeatedTerminal() => _query.GetPlan(forPaging: true);

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
        var builder = new SqliteBuilder().Select("Name").From("samples");
        for (var i = 0; i < Conditions; i++) builder.Where($"Status{i}", i);
        return new SqlQuery((ISqlQueryPlanExecutor)_root, builder);
    }

    /// <summary>释放未打开的本地资源。</summary>
    [GlobalCleanup]
    public void Cleanup() { _root.Dispose(); _connection.Dispose(); _services.Dispose(); }
}
