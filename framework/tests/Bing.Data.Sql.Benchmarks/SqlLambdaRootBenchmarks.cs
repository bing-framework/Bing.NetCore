using System;
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
/// 类型化 Lambda 根来源构造与渲染性能基线。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[MedianColumn]
[Config(typeof(SqlLambdaBenchmarkConfig))]
public class SqlLambdaRootBenchmarks
{
    private SqlLambdaQuery _query;
    private ISqlQueryPlanExecutor _executor;
    private SqlBuilderServices _services;

    /// <summary>
    /// 类型化根来源数量。
    /// </summary>
    [Params(1, 2, 5, 10, 20, 50)]
    public int RootCount { get; set; }

    /// <summary>
    /// IN 参数规模，用于观察参数快照和 SQL 渲染的分配变化。
    /// </summary>
    [Params(10, 100, 1000)]
    public int ParameterCount { get; set; }

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
    /// 测量类型化根来源重建并渲染 SQL 的成本。
    /// </summary>
    /// <returns>完整 From SQL。</returns>
    [Benchmark(Baseline = true)]
    public string SetRootsAndRender()
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

    /// <summary>
    /// 测量不同规模 IN 参数的构建和 SQL 渲染成本。
    /// </summary>
    [Benchmark]
    public string RenderInParameters()
    {
        var values = new object[ParameterCount];
        for (var index = 0; index < values.Length; index++)
            values[index] = index;
        var query = BuildQuery();
        query.Where<Root01, object>(root => root.Id, values, Operator.In);
        return query.ToSql();
    }

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
            case 20:
            case 50:
                query.From<Root01>("r1");
                for (var index = 2; index <= RootCount; index++)
                    query.FromTable($"Root{index}", $"r{index}");
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
