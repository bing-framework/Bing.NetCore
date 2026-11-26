using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Configs;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>分别测量计划终结、结构克隆及名称解析，避免把数据库等待算成 CPU 成本。</summary>
[MemoryDiagnoser]
public class SqlPlanningHotPathBenchmarks
{
    private ServiceProvider _services;
    private SqliteConnection _connection;
    private SqliteSqlQuery _root;
    private ISqlBuilder _builder;
    private readonly EntityAliasRegister _emptyAliases = new();
    private readonly EntityAliasRegister _selfJoinAliases = new();

    /// <summary>准备同一条查询；不打开连接。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _services = new ServiceCollection().AddSqliteProvider().BuildServiceProvider();
        _connection = new SqliteConnection("Data Source=:memory:");
        _root = new SqliteSqlQuery(_services, new SqlOptions<SqliteSqlQuery>
        {
            DatabaseType = DatabaseType.Sqlite, Connection = _connection
        });
        _builder = new SqliteBuilder().Select("id,value").From("public.samples").Where("id", 7);
        _selfJoinAliases.Register(typeof(SqlPlanningHotPathBenchmarks), "s");
        _selfJoinAliases.Register(typeof(SqlPlanningHotPathBenchmarks), "p");
        _selfJoinAliases.RegisterAlias("raw_source");
        const string expected = "Select `id`,`value` \r\nFrom `public`.`samples` \r\nWhere `id`=@_p_0";
        if (GetPlan().CommandText != expected || CloneBuilder().ToSql() != expected ||
            ParseName().ToSql(SqliteSqlProvider.Instance.Dialect) != "`public`.`samples`")
            throw new InvalidOperationException("Planning probe SQL changed.");
    }

    /// <summary>包含独立查询描述及首次终结，输入 Builder 已准备好，不包含数据库执行。</summary>
    [Benchmark]
    public SqlQueryPlan GetPlan() => new SqlQuery((ISqlQueryPlanExecutor)_root, _builder).GetPlan();

    /// <summary>单独测量已准备好的 Builder 克隆。</summary>
    [Benchmark]
    public ISqlBuilder CloneBuilder() => _builder.Clone();

    /// <summary>测量普通字符串来源查询所用的空别名注册器克隆。</summary>
    [Benchmark]
    public IEntityAliasRegister CloneEmptyAliases() => _emptyAliases.Clone();

    /// <summary>测量包含自连接和原始来源别名的独立注册器克隆。</summary>
    [Benchmark]
    public IEntityAliasRegister CloneSelfJoinAliases() => _selfJoinAliases.Clone();

    /// <summary>测量容量负载中常用的无别名表名解析。</summary>
    [Benchmark]
    public SqlItem ParseName() => SqlItem.Parse("public.samples");

    /// <summary>单独测量未引用名称段解析，避免与 SqlItem 构造混在一起。</summary>
    [Benchmark]
    [Arguments("samples")]
    [Arguments("public.samples")]
    [Arguments("archive.public.samples")]
    public NameItem ParseNameSegments(string name) => new(name);

    /// <summary>释放仅用于构造查询描述的资源。</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _root?.Dispose();
        _connection?.Dispose();
        _services?.Dispose();
    }

    internal static int Run(string output)
    {
        const int warmups = 4096, iterations = 5000, sampleCount = 5;
        var benchmark = new SqlPlanningHotPathBenchmarks();
        benchmark.Setup();
        try
        {
            var results = new List<object>();
            foreach (var operation in new (string Name, Func<object> Run)[]
                     {
                         ("get-plan-first-terminal", () => benchmark.GetPlan()),
                         ("builder-clone", () => benchmark.CloneBuilder()),
                         ("alias-clone-empty", () => benchmark.CloneEmptyAliases()),
                         ("alias-clone-self-join", () => benchmark.CloneSelfJoinAliases()),
                         ("name-parse", () => benchmark.ParseName()),
                         ("name-segments-one", () => benchmark.ParseNameSegments("samples")),
                         ("name-segments-two", () => benchmark.ParseNameSegments("public.samples")),
                         ("name-segments-three", () => benchmark.ParseNameSegments("archive.public.samples"))
                     })
            {
                for (var i = 0; i < warmups; i++) GC.KeepAlive(operation.Run());
                for (var sample = 0; sample < sampleCount; sample++)
                {
                    object last = null;
                    var timer = new Stopwatch();
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    timer.Start();
                    for (var i = 0; i < iterations; i++) last = operation.Run();
                    timer.Stop();
                    var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
                    GC.KeepAlive(last);
                    results.Add(new { operation = operation.Name, sample,
                        bytesPerOperation = (double)bytes / iterations,
                        nsPerOperation = timer.Elapsed.TotalNanoseconds / iterations });
                }
            }
            using var stream = new FileStream(Path.GetFullPath(output), FileMode.CreateNew);
            JsonSerializer.Serialize(stream, new
            {
                schema = "SqlPlanningHotPath-v1", runtime = Environment.Version.ToString(),
                sqlAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(typeof(SqlItem).Assembly.Location))),
                warmups, iterations, sampleCount,
                scope = "Same-thread allocation; GetPlan includes new query descriptor; no IO; timing is diagnostic only",
                results
            }, new JsonSerializerOptions { WriteIndented = true });
            return 0;
        }
        finally { benchmark.Cleanup(); }
    }
}
