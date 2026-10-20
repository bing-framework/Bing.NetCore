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
    /// <summary>
    /// 当前基准使用的 Lambda 查询。
    /// </summary>
    private SqlLambdaQuery _query;

    /// <summary>
    /// 执行 Lambda 查询计划的代理执行器。
    /// </summary>
    private ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// 创建查询 Builder 所需的服务集合。
    /// </summary>
    private SqlBuilderServices _services;

    /// <summary>
    /// 获取或设置类型化根来源数量。
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
    /// <returns>执行快照中的 SQL 文本。</returns>
    [Benchmark]
    public string CreateExecutionSnapshot() => SqlBuilderRuntimeBridge.CreateExecutionSnapshot(_query.GetBuilder()).Sql;

    /// <summary>
    /// 按当前根来源数量构建 Lambda 查询。
    /// </summary>
    /// <returns>配置完成的 Lambda 查询。</returns>
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

    /// <summary>
    /// 创建不执行数据库操作的查询计划执行器。
    /// </summary>
    /// <returns>代理查询计划执行器。</returns>
    private static ISqlQueryPlanExecutor CreateExecutor() =>
        DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();

    /// <summary>
    /// 为基准查询计划提供默认返回值的代理执行器。
    /// </summary>
    private class NoOpExecutor : DispatchProxy
    {
        /// <inheritdoc />
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }

    /// <summary>
    /// 为 Lambda 基准构造 SQL 的测试 Builder。
    /// </summary>
    private sealed class BenchmarkBuilder : SqlBuilderBase
    {
        /// <summary>
        /// 初始化一个 <see cref="BenchmarkBuilder"/> 类型的实例。
        /// </summary>
        /// <param name="services">创建 Builder 所需的服务集合。</param>
        public BenchmarkBuilder(SqlBuilderServices services)
            : this(services, null)
        {
        }

        /// <summary>
        /// 初始化一个 <see cref="BenchmarkBuilder"/> 类型的实例。
        /// </summary>
        /// <param name="services">创建 Builder 所需的服务集合。</param>
        /// <param name="parameterManager">用于管理 SQL 参数的实例。</param>
        private BenchmarkBuilder(SqlBuilderServices services, IParameterManager parameterManager)
            : base(BenchmarkSqlProvider.Instance, services, parameterManager)
        {
        }

        /// <inheritdoc />
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new BenchmarkBuilder(Services, parameterManager);
    }

    /// <summary>
    /// 为 Lambda 基准配置 SQL Server 风格的 Provider。
    /// </summary>
    private sealed class BenchmarkSqlProvider : ISqlProvider
    {
        /// <summary>
        /// 获取共享的基准 Provider 实例。
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
    /// 输出 SQL Server 分页子句的基准实现。
    /// </summary>
    private sealed class BenchmarkPaginationRenderer : ISqlPaginationRenderer
    {
        /// <inheritdoc />
        public string Render(string offsetParameterName, string limitParameterName) =>
            $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
    }

    /// <summary>
    /// 使用 SQL Server 标识符规则的基准方言。
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
    /// 第一个类型化根来源模型。
    /// </summary>
    private sealed class Root01
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第二个类型化根来源模型。
    /// </summary>
    private sealed class Root02
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第三个类型化根来源模型。
    /// </summary>
    private sealed class Root03
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第四个类型化根来源模型。
    /// </summary>
    private sealed class Root04
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第五个类型化根来源模型。
    /// </summary>
    private sealed class Root05
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第六个类型化根来源模型。
    /// </summary>
    private sealed class Root06
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第七个类型化根来源模型。
    /// </summary>
    private sealed class Root07
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第八个类型化根来源模型。
    /// </summary>
    private sealed class Root08
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第九个类型化根来源模型。
    /// </summary>
    private sealed class Root09
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第十个类型化根来源模型。
    /// </summary>
    private sealed class Root10
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }
}

/// <summary>
/// 类型化 Lambda IN 参数规模基线，不与根来源数量形成交叉参数矩阵。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[MedianColumn]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlLambdaInBenchmarks
{
    /// <summary>
    /// 执行 Lambda 查询计划的代理执行器。
    /// </summary>
    private ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// 当前基准复用的 IN 参数值。
    /// </summary>
    private object[] _values;

    /// <summary>
    /// 获取或设置IN 参数数量。
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
    /// <returns>生成的 IN 参数值数组。</returns>
    [Benchmark]
    public object[] CreateInValues() => CreateValues();

    /// <summary>
    /// 测量使用预构造值绑定并渲染 IN 参数的成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
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
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark(Baseline = true)]
    public string BuildInValuesAndRender()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_executor, new Bing.Data.Sql.Builders.SqlServerBuilder())
            .From<ParameterRoot>("p")
            .Select<ParameterRoot>(root => new object[] { root.Id });
        query.Where<ParameterRoot, object>(root => root.Id, CreateValues(), Operator.In);
        return query.ToSql();
    }

    /// <summary>
    /// 按当前参数数量创建连续整数值。
    /// </summary>
    /// <returns>用于 IN 条件的参数值。</returns>
    private object[] CreateValues()
    {
        var values = new object[ParameterCount];
        for (var index = 0; index < values.Length; index++)
            values[index] = index;
        return values;
    }

    /// <summary>
    /// 为基准查询计划提供默认返回值的代理执行器。
    /// </summary>
    private class NoOpExecutor : DispatchProxy
    {
        /// <inheritdoc />
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }

    /// <summary>
    /// IN 条件使用的参数根来源模型。
    /// </summary>
    private sealed class ParameterRoot
    {
        /// <summary>
        /// 获取或设置根来源标识。
        /// </summary>
        public int Id { get; set; }
    }
}

/// <summary>
/// Raw Fluent 来源压力基线，独立测量 20 和 50 个原始来源。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[MedianColumn]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlRawFromBenchmarks
{
    /// <summary>
    /// 执行 Raw Fluent 查询计划的代理执行器。
    /// </summary>
    private ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// 当前基准复用的原始来源列表。
    /// </summary>
    private string _sources;

    /// <summary>
    /// 获取或设置原始来源数量。
    /// </summary>
    [Params(20, 50)]
    public int SourceCount { get; set; }

    /// <summary>
    /// 初始化 Raw Fluent 查询执行器。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _executor = DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();
        _sources = string.Join(", ", Enumerable.Range(1, SourceCount)
            .Select(index => $"[RawTable{index}] As [r{index}]"));
    }

    /// <summary>
    /// 测量预构造 Raw Fluent 多来源追加并渲染 SQL 的成本。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string BuildRawSourcesAndRender()
    {
        return SqlQueryRuntimeFactory.CreateQuery(_executor, new Bing.Data.Sql.Builders.SqlServerBuilder())
            .Select("Id")
            .AppendFrom(_sources)
            .ToSql();
    }

    /// <summary>
    /// 测量原始来源字符串生成成本，避免将其混入查询渲染基线。
    /// </summary>
    /// <returns>构造出的 Raw 来源字符串。</returns>
    [Benchmark]
    public string BuildRawSourceString() => string.Join(", ", Enumerable.Range(1, SourceCount)
        .Select(index => $"[RawTable{index}] As [r{index}]"));

    /// <summary>
    /// 为基准查询计划提供默认返回值的代理执行器。
    /// </summary>
    private class NoOpExecutor : DispatchProxy
    {
        /// <inheritdoc />
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}
