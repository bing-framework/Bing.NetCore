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
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlLambdaJoinBenchmarks
{
    /// <summary>
    /// 当前 Lambda 查询。
    /// </summary>
    private SqlLambdaQuery _query;

    /// <summary>
    /// 查询计划执行器。
    /// </summary>
    private ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// Builder 共享服务。
    /// </summary>
    private SqlBuilderServices _services;

    /// <summary>
    /// Benchmark 共享的数据过滤状态，用于测量过滤开关变化下的动态渲染。
    /// </summary>
    private DataFilter _dataFilter;

    /// <summary>
    /// 获取或设置连续 Join 的来源数量。
    /// </summary>
    /// <remarks>
    /// 1 表示一个根来源，后续值各增加对应数量的 Join。
    /// </remarks>
    [Params(1, 2, 5, 10)]
    public int SourceCount { get; set; }

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
    /// <returns>渲染后的 SQL。</returns>
    [Benchmark(Baseline = true)]
    public string BuildJoinAndRender()
    {
        _query = BuildQuery();
        return _query.ToSql();
    }

    /// <summary>
    /// 测量冻结结构的重复渲染成本。
    /// </summary>
    /// <returns>渲染后的 SQL。</returns>
    [Benchmark]
    public string RenderExistingJoin() => _query.ToSql();

    /// <summary>
    /// 测量 WhereIf 为 true 时的构建和渲染成本。
    /// </summary>
    /// <returns>渲染后的 SQL。</returns>
    [Benchmark]
    public string WhereIfTrue()
    {
        var query = BuildQuery();
        query.WhereIf(true, (Root01 root) => root.Id == SourceCount);
        return query.ToSql();
    }

    /// <summary>
    /// 测量 WhereIf 为 false 时不改变查询结构的成本。
    /// </summary>
    /// <returns>渲染后的 SQL。</returns>
    [Benchmark]
    public string WhereIfFalse()
    {
        var query = BuildQuery();
        query.WhereIf(false, (Root01 root) => root.Id == SourceCount);
        return query.ToSql();
    }

    /// <summary>
    /// 测量参数化动态过滤的渲染成本。
    /// </summary>
    /// <returns>渲染后的 SQL。</returns>
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
    /// <returns>渲染后的 SQL。</returns>
    [Benchmark]
    public string CloneQuery() => _query.Clone().ToSql();

    /// <summary>
    /// 测量创建 Join 查询执行快照的成本。
    /// </summary>
    /// <returns>执行快照中的 SQL。</returns>
    [Benchmark]
    public string CreateExecutionSnapshot() => SqlBuilderRuntimeBridge.CreateExecutionSnapshot(_query.GetBuilder()).Sql;

    /// <summary>
    /// 构建当前来源数量对应的 Lambda 查询。
    /// </summary>
    /// <returns>构建的 Lambda 查询。</returns>
    private SqlLambdaQuery BuildQuery()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new BenchmarkBuilder(_services))
            .From<Root01>("r1");
        switch (SourceCount)
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
            default:
                throw new ArgumentOutOfRangeException(nameof(SourceCount));
        }
        query.Select<Root01>(root => new object[] { root.Id });
        return query;
    }

    /// <summary>
    /// 向查询追加指定数量的连续 Join。
    /// </summary>
    /// <param name="query">待追加 Join 的查询。</param>
    /// <param name="count">需要追加的来源数量。</param>
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

    /// <summary>
    /// 创建空操作查询计划执行器。
    /// </summary>
    /// <returns>空操作查询计划执行器。</returns>
    private static ISqlQueryPlanExecutor CreateExecutor() =>
        DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();

    /// <summary>
    /// Benchmark 使用的空操作代理。
    /// </summary>
    private class NoOpExecutor : DispatchProxy
    {
        /// <inheritdoc />
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }

    /// <summary>
    /// Benchmark 使用的 SQL Builder。
    /// </summary>
    private sealed class BenchmarkBuilder : SqlBuilderBase
    {
        /// <summary>
        /// 初始化一个 <see cref="BenchmarkBuilder"/> 类型的实例。
        /// </summary>
        /// <param name="services">Builder 共享服务。</param>
        public BenchmarkBuilder(SqlBuilderServices services)
            : this(services, null)
        {
        }

        /// <summary>
        /// 初始化一个 <see cref="BenchmarkBuilder"/> 类型的实例。
        /// </summary>
        /// <param name="services">Builder 共享服务。</param>
        /// <param name="parameterManager">参数管理器。</param>
        private BenchmarkBuilder(SqlBuilderServices services, IParameterManager parameterManager)
            : base(BenchmarkSqlProvider.Instance, services, parameterManager)
        {
        }

        /// <inheritdoc />
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new BenchmarkBuilder(Services, parameterManager);
    }

    /// <summary>
    /// Benchmark 使用的 SQL Provider。
    /// </summary>
    private sealed class BenchmarkSqlProvider : ISqlProvider
    {
        /// <summary>
        /// 获取 Provider 单例。
        /// </summary>
        public static BenchmarkSqlProvider Instance { get; } = new();

        /// <inheritdoc />
        public string Key => "benchmark.sqlserver";

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect { get; } = new BenchmarkDialect();

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new BenchmarkPaginationRenderer();

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver { get; } = new ParamLiteralsResolver();
    }

    /// <summary>
    /// Benchmark 使用的分页渲染器。
    /// </summary>
    private sealed class BenchmarkPaginationRenderer : ISqlPaginationRenderer
    {
        /// <inheritdoc />
        public string Render(string offsetParameterName, string limitParameterName) =>
            $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
    }

    /// <summary>
    /// Benchmark 使用的 SQL 方言。
    /// </summary>
    private sealed class BenchmarkDialect : DialectBase
    {
        /// <inheritdoc />
        public override char OpeningIdentifier => '[';

        /// <inheritdoc />
        public override char ClosingIdentifier => ']';

        /// <inheritdoc />
        public override string GetPrefix() => "@";
    }

    /// <summary>
    /// 支持软删除过滤的 Benchmark 根实体。
    /// </summary>
    private class Root01 : ISoftDelete
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }

        /// <inheritdoc />
        public bool IsDeleted { get; set; }
    }

    /// <summary>
    /// Benchmark 使用的 Join 来源实体。
    /// </summary>
    private class Root02
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }

    /// <summary>
    /// Benchmark 使用的 Join 来源实体。
    /// </summary>
    private class Root03
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }

    /// <summary>
    /// Benchmark 使用的 Join 来源实体。
    /// </summary>
    private class Root04
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }

    /// <summary>
    /// Benchmark 使用的 Join 来源实体。
    /// </summary>
    private class Root05
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }

    /// <summary>
    /// Benchmark 使用的 Join 来源实体。
    /// </summary>
    private class Root06
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }

    /// <summary>
    /// Benchmark 使用的 Join 来源实体。
    /// </summary>
    private class Root07
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }

    /// <summary>
    /// Benchmark 使用的 Join 来源实体。
    /// </summary>
    private class Root08
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }

    /// <summary>
    /// Benchmark 使用的 Join 来源实体。
    /// </summary>
    private class Root09
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }

    /// <summary>
    /// Benchmark 使用的 Join 来源实体。
    /// </summary>
    private class Root10
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }
}

/// <summary>
/// 固定输入的 Join 边界路径基线，避免与来源数量参数形成无关交叉矩阵。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[MedianColumn]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlLambdaFixedJoinBenchmarks
{
    /// <summary>
    /// 查询计划执行器。
    /// </summary>
    private ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// Builder 共享服务。
    /// </summary>
    private SqlBuilderServices _services;

    /// <summary>
    /// 初始化固定 Join 场景。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _executor = DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();
        _services = new SqlBuilderServices();
    }

    /// <summary>
    /// 测量重复实体 Join 的别名解析成本。
    /// </summary>
    /// <returns>渲染后的 SQL。</returns>
    [Benchmark]
    public string BuildRepeatedEntityJoin()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor,
                new Bing.Data.Sql.Builders.SqlServerBuilder(_services))
            .From<Root01>("parent")
            .Join<Root01, Root01>((left, right) => left.Id == right.ParentId,
                new SqlJoinOptions { RightAlias = "child", LeftAlias = "parent" });
        return query.ToSql();
    }

    /// <summary>
    /// 测量 Join 失败后异常返回的成本。
    /// </summary>
    /// <returns>成功或异常类型的标识。</returns>
    [Benchmark]
    public string JoinFailure()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor,
                new Bing.Data.Sql.Builders.SqlServerBuilder(_services))
            .From<Root01>("root");
        try
        {
            query.Join<Root01, Root02>((left, right) => left.Id == right.ParentId,
                new SqlJoinOptions { RightAlias = "root", LeftAlias = "root" });
            return "unexpected";
        }
        catch (Exception exception)
        {
            return exception.GetType().Name;
        }
    }

    /// <summary>
    /// Benchmark 使用的空操作代理。
    /// </summary>
    private class NoOpExecutor : DispatchProxy
    {
        /// <inheritdoc />
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }

    /// <summary>
    /// Benchmark 使用的父实体。
    /// </summary>
    private class Root01
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }

    /// <summary>
    /// Benchmark 使用的子实体。
    /// </summary>
    private class Root02
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置父实体标识。
        /// </summary>
        public int ParentId { get; set; }
    }
}
