using System;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Filters;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 公开非泛型 Lambda 查询的 Join、过滤、Clone 和失败路径性能基线。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[MedianColumn]
[Config(typeof(SqlLambdaBenchmarkConfig))]
public class SqlLambdaJoinBenchmarks
{
    private SqlLambdaQuery _query;
    private ISqlQueryPlanExecutor _executor;
    private SqlBuilderServices _services;

    /// <summary>
    /// Benchmark 共享的数据过滤状态，用于测量过滤开关变化下的动态渲染。
    /// </summary>
    private DataFilter _dataFilter;

    /// <summary>
    /// 连续 Join 的来源数量。
    /// </summary>
    [Params(1, 2, 5, 10, 20, 50)]
    public int JoinCount { get; set; }

    /// <summary>
    /// 初始化公开 Lambda 查询。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _executor = CreateExecutor();
        _dataFilter = new DataFilter();
        _services = new SqlBuilderServices(dataFilter: _dataFilter);
        _query = BuildQuery();
    }

    /// <summary>
    /// 测量连续二元 Join 的构建和 SQL 渲染成本。
    /// </summary>
    [Benchmark(Baseline = true)]
    public string BuildJoinAndRender()
    {
        _query = BuildQuery();
        return _query.ToSql();
    }

    /// <summary>
    /// 测量冻结结构的重复渲染成本。
    /// </summary>
    [Benchmark]
    public string RenderExistingJoin() => _query.ToSql();

    /// <summary>
    /// 测量 WhereIf 为 true 时的构建和渲染成本。
    /// </summary>
    [Benchmark]
    public string WhereIfTrue()
    {
        var query = BuildQuery();
        query.WhereIf(true, (Root01 root) => root.Id == JoinCount);
        return query.ToSql();
    }

    /// <summary>
    /// 测量 WhereIf 为 false 时不改变查询结构的成本。
    /// </summary>
    [Benchmark]
    public string WhereIfFalse()
    {
        var query = BuildQuery();
        query.WhereIf(false, (Root01 root) => root.Id == JoinCount);
        return query.ToSql();
    }

    /// <summary>
    /// 测量参数化动态过滤的渲染成本。
    /// </summary>
    [Benchmark]
    public string DynamicFilterRender()
    {
        var query = BuildQuery();
        using (_dataFilter.Disable<ISoftDelete>())
            return query.ToSql();
    }

    /// <summary>
    /// 测量公开查询描述的 Builder Clone 成本。
    /// </summary>
    [Benchmark]
    public string CloneQuery() => _query.Clone().ToSql();

    /// <summary>
    /// 测量创建 Join 查询执行快照的成本。
    /// </summary>
    [Benchmark]
    public string CreateExecutionSnapshot() => SqlBuilderRuntimeBridge.CreateExecutionSnapshot(_query.GetBuilder()).Sql;

    /// <summary>
    /// 测量重复实体 Join 的别名解析成本。
    /// </summary>
    [Benchmark]
    public string BuildRepeatedEntityJoin()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new BenchmarkBuilder(_services))
            .From<Root01>("parent")
            .Join<Root01, Root01>((left, right) => left.Id == right.ParentId, "child", "parent");
        return query.ToSql();
    }

    /// <summary>
    /// 测量 Join 失败后异常返回的成本。
    /// </summary>
    [Benchmark]
    public string JoinFailure()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new BenchmarkBuilder(_services))
            .From<Root01>("root");
        try
        {
            query.Join<Root01, Root02>((left, right) => left.Id == right.ParentId, "root", "root");
            return "unexpected";
        }
        catch (Exception exception)
        {
            return exception.GetType().Name;
        }
    }

    private SqlLambdaQuery BuildQuery()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new BenchmarkBuilder(_services))
            .From<Root01>("r1");
        switch (JoinCount)
        {
            case 1:
                break;
            case 2:
                AddJoinsThrough(query, 2);
                break;
            case 5:
                AddJoinsThrough(query, 5);
                break;
            case 10:
                AddJoinsThrough(query, 10);
                break;
            case 20:
            case 50:
                AddRawJoinsThrough(query, JoinCount);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(JoinCount));
        }
        query.Select<Root01>(root => new object[] { root.Id });
        return query;
    }

    private static void AddJoinsThrough(SqlLambdaQuery query, int count)
    {
        if (count >= 2)
            query.Join<Root01, Root02>((left, right) => left.Id == right.ParentId);
        if (count >= 3)
            query.Join<Root02, Root03>((left, right) => left.Id == right.ParentId);
        if (count >= 4)
            query.Join<Root03, Root04>((left, right) => left.Id == right.ParentId);
        if (count >= 5)
            query.Join<Root04, Root05>((left, right) => left.Id == right.ParentId);
        if (count >= 6)
            query.Join<Root05, Root06>((left, right) => left.Id == right.ParentId);
        if (count >= 7)
            query.Join<Root06, Root07>((left, right) => left.Id == right.ParentId);
        if (count >= 8)
            query.Join<Root07, Root08>((left, right) => left.Id == right.ParentId);
        if (count >= 9)
            query.Join<Root08, Root09>((left, right) => left.Id == right.ParentId);
        if (count >= 10)
            query.Join<Root09, Root10>((left, right) => left.Id == right.ParentId);
    }

    private static void AddRawJoinsThrough(SqlLambdaQuery query, int count)
    {
        for (var index = 2; index <= count; index++)
            query.GetBuilder().Join($"Root{index}", $"r{index}").AppendOn($"r{index}.Id=r{index - 1}.Id");
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

    /// <summary>
    /// 支持软删除过滤的 Benchmark 根实体。
    /// </summary>
    private class Root01 : ISoftDelete
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public bool IsDeleted { get; set; }
    }
    private class Root02 { public int Id { get; set; } public int ParentId { get; set; } }
    private class Root03 { public int Id { get; set; } public int ParentId { get; set; } }
    private class Root04 { public int Id { get; set; } public int ParentId { get; set; } }
    private class Root05 { public int Id { get; set; } public int ParentId { get; set; } }
    private class Root06 { public int Id { get; set; } public int ParentId { get; set; } }
    private class Root07 { public int Id { get; set; } public int ParentId { get; set; } }
    private class Root08 { public int Id { get; set; } public int ParentId { get; set; } }
    private class Root09 { public int Id { get; set; } public int ParentId { get; set; } }
    private class Root10 { public int Id { get; set; } public int ParentId { get; set; } }
}
