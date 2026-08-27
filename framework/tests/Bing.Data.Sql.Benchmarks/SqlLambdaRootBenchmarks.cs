using System;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 类型化 Lambda 根来源与原始表来源压力场景的构造和渲染性能基线。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[MedianColumn]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlLambdaRootBenchmarks
{
    private SqlLambdaQuery _query;
    private ISqlQueryPlanExecutor _executor;
    private SqlBuilderServices _services;

    /// <summary>
    /// 类型化根来源数量。
    /// </summary>
    [Params(1, 2, 5, 10)]
    public int RootCount { get; set; }

    /// <summary>
    /// 初始化指定元数的根来源基线。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _executor = CreateExecutor();
        _services = SqlBuilderServices.CreateDefault();
        _query = BuildQuery();
    }

    /// <summary>
    /// 测量根来源重建并渲染 SQL 的成本。
    /// </summary>
    /// <returns>完整 From SQL。</returns>
    [Benchmark(Baseline = true)]
    public string BuildRootsAndRender()
    {
        _query = BuildQuery();
        return _query.ToSql();
    }

    /// <summary>
    /// 测量已构造根来源的重复渲染成本。
    /// </summary>
    /// <returns>完整 From SQL。</returns>
    [Benchmark]
    public string RenderExistingRoots() => _query.ToSql();

    /// <summary>
    /// 测量创建 SQL 与参数一致执行快照的成本。
    /// </summary>
    [Benchmark]
    public string CreateExecutionSnapshot() => SqlBuilderRuntimeBridge.CreateExecutionSnapshot(_query.GetBuilder()).Sql;

    private SqlLambdaQuery BuildQuery()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new BenchmarkBuilder(_services));
        switch (RootCount)
        {
            case 1:
                query.From<Root01>("r1");
                break;
            case 2:
                query.From<Root01>("r1").From<Root02>("r2");
                break;
            case 5:
                query.From<Root01>("r1").From<Root02>("r2").From<Root03>("r3").From<Root04>("r4")
                    .From<Root05>("r5");
                break;
            case 10:
                query.From<Root01>("r1").From<Root02>("r2").From<Root03>("r3").From<Root04>("r4")
                    .From<Root05>("r5").From<Root06>("r6").From<Root07>("r7").From<Root08>("r8")
                    .From<Root09>("r9").From<Root10>("r10");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(RootCount));
        }
        query.Select<Root01>(root => new object[] { root.Id });
        return query;
    }

    private static ISqlQueryPlanExecutor CreateExecutor() =>
        DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();

    private class NoOpExecutor : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }

    private sealed class BenchmarkBuilder : SqlBuilderBase
    {
        public BenchmarkBuilder(SqlBuilderServices services)
            : this(services, null)
        {
        }

        private BenchmarkBuilder(SqlBuilderServices services, IParameterManager parameterManager)
            : base(BenchmarkSqlProvider.Instance, services, parameterManager)
        {
        }

        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new BenchmarkBuilder(Services, parameterManager);
    }

    private sealed class BenchmarkSqlProvider : ISqlProvider
    {
        public static BenchmarkSqlProvider Instance { get; } = new();
        public string Key => "benchmark.sqlserver";
        public DatabaseType DatabaseType => DatabaseType.SqlServer;
        public IDialect Dialect { get; } = new BenchmarkDialect();
        public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();
        public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;
        public ISqlPaginationRenderer PaginationRenderer { get; } = new BenchmarkPaginationRenderer();
        public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;
        public IParamLiteralsResolver ParamLiteralsResolver { get; } = new ParamLiteralsResolver();
    }

    private sealed class BenchmarkPaginationRenderer : ISqlPaginationRenderer
    {
        public string Render(string offsetParameterName, string limitParameterName) =>
            $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
    }

    private sealed class BenchmarkDialect : DialectBase
    {
        public override char OpeningIdentifier => '[';
        public override char ClosingIdentifier => ']';
        public override string GetPrefix() => "@";
    }

    private sealed class Root01 { public int Id { get; set; } }
    private sealed class Root02 { public int Id { get; set; } }
    private sealed class Root03 { public int Id { get; set; } }
    private sealed class Root04 { public int Id { get; set; } }
    private sealed class Root05 { public int Id { get; set; } }
    private sealed class Root06 { public int Id { get; set; } }
    private sealed class Root07 { public int Id { get; set; } }
    private sealed class Root08 { public int Id { get; set; } }
    private sealed class Root09 { public int Id { get; set; } }
    private sealed class Root10 { public int Id { get; set; } }
}

/// <summary>
/// 类型化 Lambda IN 参数规模基线，不与根来源数量形成交叉参数矩阵。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[MedianColumn]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlLambdaInBenchmarks
{
    private ISqlQueryPlanExecutor _executor;
    private object[] _values;

    /// <summary>
    /// IN 参数数量。
    /// </summary>
    [Params(0, 1, 10, 100, 500, 1000, 2100)]
    public int ParameterCount { get; set; }

    /// <summary>
    /// 初始化 Lambda 查询执行器。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _executor = DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();
        _values = CreateValues();
    }

    /// <summary>
    /// 测量输入值创建和 boxing 成本。
    /// </summary>
    [Benchmark]
    public object[] CreateInValues() => CreateValues();

    /// <summary>
    /// 测量使用预构造值绑定并渲染 IN 参数的成本。
    /// </summary>
    [Benchmark]
    public string BindExistingInValuesAndRender()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new Bing.Data.Sql.Builders.SqlServerBuilder())
            .From<ParameterRoot>("p")
            .Select<ParameterRoot>(root => new object[] { root.Id });
        query.Where<ParameterRoot, object>(root => root.Id, _values, Operator.In);
        return query.ToSql();
    }

    /// <summary>
    /// 测量创建值、构建查询、绑定和渲染 IN 参数的完整成本。
    /// </summary>
    [Benchmark(Baseline = true)]
    public string BuildInValuesAndRender()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new Bing.Data.Sql.Builders.SqlServerBuilder())
            .From<ParameterRoot>("p")
            .Select<ParameterRoot>(root => new object[] { root.Id });
        query.Where<ParameterRoot, object>(root => root.Id, CreateValues(), Operator.In);
        return query.ToSql();
    }

    private object[] CreateValues()
    {
        var values = new object[ParameterCount];
        for (var index = 0; index < values.Length; index++)
            values[index] = index;
        return values;
    }

    private class NoOpExecutor : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }

    private sealed class ParameterRoot { public int Id { get; set; } }
}

/// <summary>
/// Raw Fluent 来源压力基线，独立测量 20 和 50 个原始来源。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[MedianColumn]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlRawFromBenchmarks
{
    private ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// 原始来源数量。
    /// </summary>
    [Params(20, 50)]
    public int SourceCount { get; set; }

    /// <summary>
    /// 初始化 Raw Fluent 查询执行器。
    /// </summary>
    [GlobalSetup]
    public void Setup() => _executor = DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();

    /// <summary>
    /// 测量 Raw Fluent 追加多来源并渲染 SQL 的成本。
    /// </summary>
    [Benchmark]
    public string BuildRawSourcesAndRender()
    {
        var sources = string.Join(", ", Enumerable.Range(1, SourceCount)
            .Select(index => $"[RawTable{index}] As [r{index}]"));
        return SqlQueryRuntimeFactory.CreateQuery(_executor, new Bing.Data.Sql.Builders.SqlServerBuilder())
            .Select("Id")
            .AppendFrom(sources)
            .ToSql();
    }

    private class NoOpExecutor : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}
