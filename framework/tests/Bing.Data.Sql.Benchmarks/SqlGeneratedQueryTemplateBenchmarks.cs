using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>比较静态桥接重放与热查询模板的 SQL 和绑定快照成本。</summary>
/// <remarks>每次调用都创建新查询并更新值；不包含数据库 I/O，服务与描述符在基准内共享。</remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlGeneratedQueryTemplateBenchmarks
{
    private const int AllocationProbeWarmup = 2048;
    private const int AllocationProbeIterations = 10000;
    private const int AllocationProbeRepetitions = 5;

    private static readonly Type[] Types = { typeof(Row) };
    private static readonly string[] Names = { "r" };
    private static readonly SqlGeneratedColumn Id = new(0, nameof(Row.Id), null, typeof(int));
    private static readonly SqlGeneratedColumn Name = new(0, nameof(Row.Name), null, typeof(string));
    private static readonly SqlGeneratedColumn[] SelectColumns = { Id, Name };
    private static readonly SqlGeneratedColumn[] OrderColumns = { Id };
    private static readonly SqlGeneratedColumn TotalCount = new(0, nameof(Row.Id), "Total", typeof(long), SqlAggregateFunction.Count);
    private static readonly SqlGeneratedColumn[] AggregateColumns = { TotalCount };
    private static readonly SqlGeneratedQueryTemplate Template = new(new[]
    {
        new SqlGeneratedQueryTemplate.Operation(
            static (query, arguments) => SqlGeneratedQueryBridge.Compare(query, false,
                Types, Names, null, Id, null, Operator.GreaterEqual, arguments[0]),
            static arguments => arguments[0], static _ => Operator.GreaterEqual, valueArgument: 0),
        new SqlGeneratedQueryTemplate.Operation(
            static (query, arguments) => SqlGeneratedQueryBridge.OptionalCompare(query, (bool)arguments[0], false,
                Types, Names, null, Id, null, Operator.In, arguments[1]),
            static arguments => arguments[1], static _ => Operator.In, valueArgument: 1, conditionArgument: 0),
        new SqlGeneratedQueryTemplate.Operation(
            static (query, _) => SqlGeneratedQueryBridge.Project(query, "Select", Types, Names, null, SelectColumns)),
        new SqlGeneratedQueryTemplate.Operation(
            static (query, _) => SqlGeneratedQueryBridge.Project(query, "OrderBy", Types, Names, null, OrderColumns))
    });
    private static readonly SqlGeneratedQueryTemplate AggregateTemplate = new(new[]
    {
        new SqlGeneratedQueryTemplate.Operation(
            static (query, arguments) => SqlGeneratedQueryBridge.OptionalCompare(query, (bool)arguments[0], false,
                Types, Names, null, Id, null, Operator.GreaterEqual, arguments[1]),
            static arguments => arguments[1], static _ => Operator.GreaterEqual, valueArgument: 1, conditionArgument: 0),
        new SqlGeneratedQueryTemplate.Operation(
            static (query, _) => SqlGeneratedQueryBridge.Project(query, "Select", Types, Names, null,
                AggregateColumns))
    });

    private SqlBuilderServices _services;
    private ISqlQueryPlanExecutor _executor;
    private SqlLambdaQuery _snapshotReuseQuery;
    private int _requestNumber;

    /// <summary>IN 集合大小；各参数值只改变数据，不改变查询形状。</summary>
    [Params(1, 8, 32)]
    public int CollectionLength { get; set; }

    /// <summary>创建共享服务并预热两条路径，校验 SQL 和请求值一致。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _services = SqlBuilderServices.CreateDefault();
        _executor = CreateExecutor();

        var request = NextRequest();
        var bridgeQuery = CreateBridgeQuery(request);
        var bridgeSql = bridgeQuery.ToSql();
        var bridgeSnapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(bridgeQuery.GetBuilder());

        var templateQuery = CreateTemplateQuery(request);
        _snapshotReuseQuery = templateQuery;
        var templateSql = templateQuery.ToSql();
        if (!templateQuery.GeneratedCore.TemplateState.TryPrepare(typeof(void), out var templateSnapshot, out _))
            throw new InvalidOperationException("查询模板没有生成绑定快照。");
        if (!_snapshotReuseQuery.GeneratedCore.TemplateState.TryPrepare(typeof(void), out var reusedTemplateSnapshot, out _) ||
            !ReferenceEquals(templateSnapshot, reusedTemplateSnapshot))
            throw new InvalidOperationException("同一查询没有复用已生成的绑定快照。");

        var expectedSql = CreateExpectedSql();
        if (bridgeSql != expectedSql || templateSql != expectedSql || bridgeSnapshot.Sql != expectedSql ||
            templateSnapshot.Sql != expectedSql)
            throw new InvalidOperationException("静态桥接与查询模板的 SQL 不一致。");
        ValidateParameters(bridgeSnapshot, request);
        ValidateParameters(templateSnapshot, request);

        var nextTemplateQuery = CreateTemplateQuery(NextRequest());
        if (!nextTemplateQuery.ToSql().Equals(templateSql, StringComparison.Ordinal) ||
            !nextTemplateQuery.GeneratedCore.TemplateState.TryPrepare(typeof(void), out var nextSnapshot, out _) ||
            !ReferenceEquals(templateSnapshot.Sql, nextSnapshot.Sql))
            throw new InvalidOperationException("相同模板形状没有复用 SQL 文本。");

        var aggregateQuery = CreateAggregateTemplateQuery(41);
        if (!aggregateQuery.GeneratedCore.TemplateState.TryPrepare(typeof(Summary), out var aggregateSnapshot, out _))
            throw new InvalidOperationException("聚合查询模板没有生成绑定快照。");
        const string expectedAggregateSql = "Select Count(`r`.`Id`) As `Total` \r\n" +
            "From `AutoTemplateRows` As `r` \r\nWhere `r`.`Id`>=@_p_0";
        if (aggregateSnapshot.Sql != expectedAggregateSql)
            throw new InvalidOperationException("聚合查询模板生成的 SQL 不一致。");
        ValidateAggregateParameter(aggregateSnapshot, 41);

        var nextAggregateQuery = CreateAggregateTemplateQuery(73);
        if (!nextAggregateQuery.GeneratedCore.TemplateState.TryPrepare(typeof(Summary), out var nextAggregateSnapshot, out _) ||
            !ReferenceEquals(aggregateSnapshot.Sql, nextAggregateSnapshot.Sql))
            throw new InvalidOperationException("聚合查询模板没有复用相同的 SQL 文本。");
        ValidateAggregateParameter(nextAggregateSnapshot, 73);
    }

    /// <summary>创建并渲染新查询，逐次重放静态查询桥接。</summary>
    [Benchmark(Baseline = true), BenchmarkCategory("ToSql")]
    public string StaticBridgeToSql() => CreateBridgeQuery(NextRequest()).ToSql();

    /// <summary>创建并渲染新查询，使用已热身的结构模板。</summary>
    [Benchmark, BenchmarkCategory("ToSql")]
    public string TemplateApplyToSql() => CreateTemplateQuery(NextRequest()).ToSql();

    /// <summary>重放静态查询桥接并生成包含当前参数值的执行快照。</summary>
    [Benchmark(Baseline = true), BenchmarkCategory("BoundSnapshot")]
    public SqlBuilderExecutionSnapshot StaticBridgeBoundSnapshot()
    {
        var query = CreateBridgeQuery(NextRequest());
        return SqlBuilderRuntimeBridge.CreateExecutionSnapshot(query.GetBuilder());
    }

    /// <summary>应用已热身的查询模板并创建当前请求的绑定快照。</summary>
    [Benchmark, BenchmarkCategory("BoundSnapshot")]
    public SqlBuilderExecutionSnapshot TemplateApplyBoundSnapshot()
    {
        var query = CreateTemplateQuery(NextRequest());
        if (!query.GeneratedCore.TemplateState.TryPrepare(typeof(void), out var snapshot, out _))
            throw new InvalidOperationException("查询模板没有生成绑定快照。");
        return snapshot;
    }

    /// <summary>在同一已预热查询上重复取得绑定快照，不创建新查询或参数。</summary>
    [Benchmark, BenchmarkCategory("SnapshotReuse")]
    public SqlBuilderExecutionSnapshot TemplateSameQueryBoundSnapshot()
    {
        if (!_snapshotReuseQuery.GeneratedCore.TemplateState.TryPrepare(typeof(void), out var snapshot, out _))
            throw new InvalidOperationException("查询模板没有生成绑定快照。");
        return snapshot;
    }

    /// <summary>创建热聚合查询模板并绑定本次标量条件。</summary>
    [Benchmark, BenchmarkCategory("AggregateBoundSnapshot")]
    public SqlBuilderExecutionSnapshot TemplateAggregateBoundSnapshot()
    {
        var query = CreateAggregateTemplateQuery(++_requestNumber);
        if (!query.GeneratedCore.TemplateState.TryPrepare(typeof(Summary), out var snapshot, out _))
            throw new InvalidOperationException("聚合查询模板没有生成绑定快照。");
        return snapshot;
    }

    /// <summary>采集各集合大小下查询模板的本机分配与 elapsed 观察数据。</summary>
    internal static int RunAllocationProbe(string output)
    {
        var results = new System.Collections.Generic.List<object>();
        foreach (var collectionLength in new[] { 1, 8, 32 })
        {
            var benchmark = new SqlGeneratedQueryTemplateBenchmarks { CollectionLength = collectionLength };
            benchmark.Setup();
            results.Add(new
            {
                collectionLength,
                operation = "ToSql",
                path = "StaticBridge",
                samples = Measure(benchmark.StaticBridgeToSql)
            });
            results.Add(new
            {
                collectionLength,
                operation = "ToSql",
                path = "QueryTemplate",
                samples = Measure(benchmark.TemplateApplyToSql)
            });
            results.Add(new
            {
                collectionLength,
                operation = "BoundSnapshot",
                path = "StaticBridge",
                samples = Measure(benchmark.StaticBridgeBoundSnapshot)
            });
            results.Add(new
            {
                collectionLength,
                operation = "BoundSnapshot",
                path = "QueryTemplate",
                samples = Measure(benchmark.TemplateApplyBoundSnapshot)
            });
            results.Add(new
            {
                collectionLength,
                operation = "SnapshotReuse",
                path = "QueryTemplateSameQuery",
                samples = Measure(benchmark.TemplateSameQueryBoundSnapshot)
            });
            if (collectionLength == 1)
                results.Add(new
                {
                    collectionLength,
                    operation = "AggregateBoundSnapshot",
                    path = "QueryTemplate",
                    samples = Measure(benchmark.TemplateAggregateBoundSnapshot)
                });
        }

        var report = new
        {
            probe = "GeneratedQueryTemplateAllocationProbe-v1",
            runtime = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            warmup = AllocationProbeWarmup,
            iterations = AllocationProbeIterations,
            repetitions = AllocationProbeRepetitions,
            elapsedMeasure = "Elapsed ns/op; local observation including probe loop overhead, not method CPU or production capacity.",
            results
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            new UTF8Encoding(false));
        Console.WriteLine("GENERATED_QUERY_TEMPLATE_ALLOCATION_PROBE_PASS");
        return 0;
    }

    private static AllocationProbeSamples Measure<T>(Func<T> operation) where T : class
    {
        for (var index = 0; index < AllocationProbeWarmup; index++) operation();
        var allocatedBytesPerOperation = new double[AllocationProbeRepetitions];
        var elapsedNanosecondsPerOperation = new double[AllocationProbeRepetitions];
        for (var sample = 0; sample < AllocationProbeRepetitions; sample++)
        {
            var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            T last = null;
            for (var index = 0; index < AllocationProbeIterations; index++) last = operation();
            var elapsedTicks = Stopwatch.GetTimestamp() - started;
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
            allocatedBytesPerOperation[sample] = allocatedBytes / (double)AllocationProbeIterations;
            elapsedNanosecondsPerOperation[sample] = elapsedTicks * (1_000_000_000d / Stopwatch.Frequency) /
                AllocationProbeIterations;
            GC.KeepAlive(last);
        }
        return new AllocationProbeSamples(allocatedBytesPerOperation, elapsedNanosecondsPerOperation);
    }

    private sealed record AllocationProbeSamples(double[] AllocatedBytesPerOperation, double[] ElapsedNanosecondsPerOperation);

    private SqlLambdaQuery CreateBridgeQuery(Request request)
    {
        var query = CreateQuery();
        SqlGeneratedQueryBridge.Compare(query, false, Types, Names, null, Id, null,
            Operator.GreaterEqual, request.Minimum);
        SqlGeneratedQueryBridge.OptionalCompare(query, request.FilterIds, false, Types, Names, null,
            Id, null, Operator.In, request.Ids);
        SqlGeneratedQueryBridge.Project(query, "Select", Types, Names, null, SelectColumns);
        SqlGeneratedQueryBridge.Project(query, "OrderBy", Types, Names, null, OrderColumns);
        return query;
    }

    private SqlLambdaQuery CreateTemplateQuery(Request request)
    {
        var query = CreateQuery();
        SqlGeneratedQueryTemplate.Apply(query, Template, 0, new object[] { request.Minimum });
        SqlGeneratedQueryTemplate.Apply(query, Template, 1, new object[] { request.FilterIds, request.Ids });
        SqlGeneratedQueryTemplate.Apply(query, Template, 2, Array.Empty<object>());
        SqlGeneratedQueryTemplate.Apply(query, Template, 3, Array.Empty<object>());
        return query;
    }

    private SqlLambdaQuery CreateAggregateTemplateQuery(int minimum)
    {
        var query = CreateQuery();
        SqlGeneratedQueryTemplate.Apply(query, AggregateTemplate, 0, new object[] { true, minimum });
        SqlGeneratedQueryTemplate.Apply(query, AggregateTemplate, 1, Array.Empty<object>());
        return query;
    }

    private SqlLambdaQuery CreateQuery() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new SqliteBuilder(_services)).From<Row>("r");

    private Request NextRequest()
    {
        var minimum = ++_requestNumber;
        var ids = new int[CollectionLength];
        for (var index = 0; index < ids.Length; index++)
            ids[index] = minimum + index + 1;
        return new Request(minimum, true, ids);
    }

    private string CreateExpectedSql() => "Select `r`.`Id`,`r`.`Name` \r\nFrom `AutoTemplateRows` As `r` \r\n" +
        "Where `r`.`Id`>=@_p_0 And `r`.`Id` In (" +
        string.Join(",", Enumerable.Range(1, CollectionLength).Select(index => "@_p_" + index)) +
        ") \r\nOrder By `r`.`Id`";

    private static void ValidateParameters(SqlBuilderExecutionSnapshot snapshot, Request request)
    {
        var parameters = snapshot.Parameters.ToArray();
        if (parameters.Length != request.Ids.Length + 1 || parameters[0].Name != "@_p_0" ||
            !Equals(parameters[0].Value, request.Minimum))
            throw new InvalidOperationException("绑定快照的标量参数不一致。");
        for (var index = 0; index < request.Ids.Length; index++)
            if (parameters[index + 1].Name != "@_p_" + (index + 1) ||
                !Equals(parameters[index + 1].Value, request.Ids[index]))
                throw new InvalidOperationException("绑定快照的集合参数不一致。");
    }

    private static void ValidateAggregateParameter(SqlBuilderExecutionSnapshot snapshot, int minimum)
    {
        var parameter = snapshot.Parameters.Single();
        if (parameter.Name != "@_p_0" || !Equals(parameter.Value, minimum))
            throw new InvalidOperationException("聚合绑定快照未使用本次标量值。");
    }

    private static ISqlQueryPlanExecutor CreateExecutor() =>
        DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();

    private readonly record struct Request(int Minimum, bool FilterIds, int[] Ids);

    /// <summary>静态桥接与模板场景共用的行模型。</summary>
    [System.ComponentModel.DataAnnotations.Schema.Table("AutoTemplateRows")]
    public sealed class Row
    {
        /// <summary>行标识。</summary>
        public int Id { get; set; }

        /// <summary>行名称。</summary>
        public string Name { get; set; }
    }

    /// <summary>接收聚合查询结果的 DTO。</summary>
    public sealed class Summary
    {
        /// <summary>满足条件的记录数。</summary>
        public long Total { get; set; }
    }

    private class NoOpExecutor : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}
