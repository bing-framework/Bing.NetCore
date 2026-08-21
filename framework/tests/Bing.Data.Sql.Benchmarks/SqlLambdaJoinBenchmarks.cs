using System;
using System.Linq.Expressions;
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
/// 类型化 Lambda 连续 Join 构造、渲染和复制性能基线。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[MedianColumn]
[Config(typeof(SqlLambdaBenchmarkConfig))]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlLambdaJoinBenchmarks
{
    private BenchmarkBuilder _builder;

    /// <summary>
    /// 连续类型化 Join 的来源数量。
    /// </summary>
    [Params(1, 2, 5, 10)]
    public int JoinCount { get; set; }

    /// <summary>
    /// 初始化指定来源数量的连续 Join 查询。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = new BenchmarkBuilder();
        BuildJoinQuery();
    }

    /// <summary>
    /// 测量连续类型化 Join 的构建和 SQL 渲染成本。
    /// </summary>
    [Benchmark(Baseline = true)]
    public string BuildJoinAndRender()
    {
        _builder.Clear();
        BuildJoinQuery();
        return _builder.ToSql();
    }

    /// <summary>
    /// 测量已构造连续 Join 的重复渲染成本。
    /// </summary>
    [Benchmark]
    public string RenderExistingJoin() => _builder.ToSql();

    /// <summary>
    /// 测量带参数条件的连续 Join 构建和渲染成本。
    /// </summary>
    [Benchmark]
    public string BuildParameterizedJoin()
    {
        _builder.Clear();
        BuildJoinQuery();
        _builder.Where("Root01.Id", JoinCount);
        return _builder.ToSql();
    }

    /// <summary>
    /// 测量重复实体来源 Join 的构建和渲染成本。
    /// </summary>
    [Benchmark]
    public string BuildRepeatedEntityJoin()
    {
        _builder.Clear();
        var from = (FromClause)_builder.FromClause;
        from.SetRoots(new[] { typeof(Root01) });
        _builder.Select("Root01.Id");
        var joins = (JoinClause)_builder.JoinClause;
        joins.Join<Root01>(from, (Expression<Func<Root01, Root01, bool>>)((left, right) => left.Id == right.Id), "parent");
        return _builder.ToSql();
    }

    /// <summary>
    /// 测量连续 Join DTO 投影的构建和渲染成本。
    /// </summary>
    [Benchmark]
    public string BuildDtoProjectionJoin()
    {
        _builder.Clear();
        BuildJoinQuery();
        _builder.ClearSelect().Select("Root01.Id As FirstId,Root10.Id As LastId");
        return _builder.ToSql();
    }

    /// <summary>
    /// 测量连续 Join 查询 Clone 的成本。
    /// </summary>
    [Benchmark]
    public string CloneJoinQuery() => _builder.Clone().ToSql();

    private void BuildJoinQuery()
    {
        var from = (FromClause)_builder.FromClause;
        from.SetRoots(new[] { typeof(Root01) });
        _builder.Select("Root01.Id");
        switch (JoinCount)
        {
            case 1:
                return;
            case 2:
                BuildTwoJoin(from);
                return;
            case 5:
                BuildFiveJoin(from);
                return;
            case 10:
                BuildTenJoin(from);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(JoinCount));
        }
    }

    private void BuildTwoJoin(FromClause from)
    {
        var joins = (JoinClause)_builder.JoinClause;
        joins.Join<Root02>(from, (Expression<Func<Root01, Root02, bool>>)((first, second) => first.Id == second.ParentId));
    }

    private void BuildFiveJoin(FromClause from)
    {
        var joins = (JoinClause)_builder.JoinClause;
        joins.Join<Root02>(from, (Expression<Func<Root01, Root02, bool>>)((first, second) => first.Id == second.ParentId));
        joins.Join<Root03>(from, (Expression<Func<Root01, Root02, Root03, bool>>)((first, second, third) => second.Id == third.ParentId));
        joins.Join<Root04>(from, (Expression<Func<Root01, Root02, Root03, Root04, bool>>)((first, second, third, fourth) => third.Id == fourth.ParentId));
        joins.Join<Root05>(from, (Expression<Func<Root01, Root02, Root03, Root04, Root05, bool>>)((first, second, third, fourth, fifth) => fourth.Id == fifth.ParentId));
    }

    private void BuildTenJoin(FromClause from)
    {
        var joins = (JoinClause)_builder.JoinClause;
        joins.Join<Root02>(from, (Expression<Func<Root01, Root02, bool>>)((first, second) => first.Id == second.ParentId));
        joins.Join<Root03>(from, (Expression<Func<Root01, Root02, Root03, bool>>)((first, second, third) => second.Id == third.ParentId));
        joins.Join<Root04>(from, (Expression<Func<Root01, Root02, Root03, Root04, bool>>)((first, second, third, fourth) => third.Id == fourth.ParentId));
        joins.Join<Root05>(from, (Expression<Func<Root01, Root02, Root03, Root04, Root05, bool>>)((first, second, third, fourth, fifth) => fourth.Id == fifth.ParentId));
        joins.Join<Root06>(from, (Expression<Func<Root01, Root02, Root03, Root04, Root05, Root06, bool>>)((first, second, third, fourth, fifth, sixth) => fifth.Id == sixth.ParentId));
        joins.Join<Root07>(from, (Expression<Func<Root01, Root02, Root03, Root04, Root05, Root06, Root07, bool>>)((first, second, third, fourth, fifth, sixth, seventh) => sixth.Id == seventh.ParentId));
        joins.Join<Root08>(from, (Expression<Func<Root01, Root02, Root03, Root04, Root05, Root06, Root07, Root08, bool>>)((first, second, third, fourth, fifth, sixth, seventh, eighth) => seventh.Id == eighth.ParentId));
        joins.Join<Root09>(from, (Expression<Func<Root01, Root02, Root03, Root04, Root05, Root06, Root07, Root08, Root09, bool>>)((first, second, third, fourth, fifth, sixth, seventh, eighth, ninth) => eighth.Id == ninth.ParentId));
        joins.Join<Root10>(from, (Expression<Func<Root01, Root02, Root03, Root04, Root05, Root06, Root07, Root08, Root09, Root10, bool>>)((first, second, third, fourth, fifth, sixth, seventh, eighth, ninth, tenth) => ninth.Id == tenth.ParentId));
    }

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

    private sealed class Root01 { public int Id { get; set; } public int ParentId { get; set; } }
    private sealed class Root02 { public int Id { get; set; } public int ParentId { get; set; } }
    private sealed class Root03 { public int Id { get; set; } public int ParentId { get; set; } }
    private sealed class Root04 { public int Id { get; set; } public int ParentId { get; set; } }
    private sealed class Root05 { public int Id { get; set; } public int ParentId { get; set; } }
    private sealed class Root06 { public int Id { get; set; } public int ParentId { get; set; } }
    private sealed class Root07 { public int Id { get; set; } public int ParentId { get; set; } }
    private sealed class Root08 { public int Id { get; set; } public int ParentId { get; set; } }
    private sealed class Root09 { public int Id { get; set; } public int ParentId { get; set; } }
    private sealed class Root10 { public int Id { get; set; } public int ParentId { get; set; } }
}
