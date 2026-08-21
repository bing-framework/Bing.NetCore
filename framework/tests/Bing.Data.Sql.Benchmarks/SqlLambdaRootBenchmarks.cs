using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Bing.Data.Enums;
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
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlLambdaRootBenchmarks
{
    private readonly Type[] _availableRootTypes =
    {
        typeof(Root01), typeof(Root02), typeof(Root03), typeof(Root04), typeof(Root05),
        typeof(Root06), typeof(Root07), typeof(Root08), typeof(Root09), typeof(Root10)
    };
    private BenchmarkBuilder _builder;
    private Type[] _rootTypes;

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
        _rootTypes = _availableRootTypes.Take(RootCount).ToArray();
        _builder = new BenchmarkBuilder();
        ((FromClause)_builder.FromClause).SetRoots(_rootTypes);
    }

    /// <summary>
    /// 测量类型化根来源重建并渲染 SQL 的成本。
    /// </summary>
    /// <returns>完整 From SQL。</returns>
    [Benchmark(Baseline = true)]
    public string SetRootsAndRender()
    {
        _builder.Clear();
        ((FromClause)_builder.FromClause).SetRoots(_rootTypes);
        return _builder.ToSql();
    }

    /// <summary>
    /// 测量已构造根来源的重复渲染成本。
    /// </summary>
    /// <returns>完整 From SQL。</returns>
    [Benchmark]
    public string RenderExistingRoots() => _builder.ToSql();

    private sealed class BenchmarkBuilder : SqlBuilderBase
    {
        public BenchmarkBuilder()
            : this(null)
        {
        }

        private BenchmarkBuilder(IParameterManager parameterManager)
            : base(BenchmarkSqlProvider.Instance, SqlBuilderServices.CreateDefault(), parameterManager)
        {
        }

        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new BenchmarkBuilder(parameterManager);
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

    private sealed class Root01 { }
    private sealed class Root02 { }
    private sealed class Root03 { }
    private sealed class Root04 { }
    private sealed class Root05 { }
    private sealed class Root06 { }
    private sealed class Root07 { }
    private sealed class Root08 { }
    private sealed class Root09 { }
    private sealed class Root10 { }
}
