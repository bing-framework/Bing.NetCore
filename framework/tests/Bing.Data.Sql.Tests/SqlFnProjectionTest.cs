using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 三种函数语法共用来源、结果类型与失败原子性的运行时测试。
/// </summary>
public sealed class SqlFnProjectionTest
{
    /// <summary>
    /// 创建 SQL 函数投影测试查询。
    /// </summary>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery Create() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict).Object, new TestSqlBuilder());

    /// <summary>
    /// 验证不同聚合语法生成相同的分组 SQL。
    /// </summary>
    /// <param name="syntax">用于选择聚合表达式写法的场景序号。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Select_WhenAggregateSyntaxDiffers_ShouldRenderSameGroupedSql(int syntax)
    {
        var query = Create().From<Row>().Select<Row>(r => new { r.GroupId });
        if (syntax == 0) query.Select<Row>(r => new { Count = SqlFn.Count(r.Quantity), Total = SqlFn.Sum(r.Quantity) });
        if (syntax == 1) query.Select<Row>((r, f) => new { Count = f.Count(r.Quantity), Total = f.Sum(r.Quantity) });
        if (syntax == 2) query.Select<Row>((r, f) => new { Count = f.Count(x => x.Quantity), Total = f.Sum(x => x.Quantity) });
        query.GroupBy<Row>(g => g.GroupId)
            .Having<Row>(r => SqlFn.Count(r.Quantity) > 1L)
            .OrderBy<Row>(r => SqlFn.Sum(r.Quantity), true);
        const string expected = "Select [r].[GroupId] As [GroupId],Count([r].[Quantity]) As [Count],Sum([r].[Quantity]) As [Total] \r\nFrom [Rows] As [r] \r\nGroup By [r].[GroupId] Having Count([r].[Quantity])>@_p_0 \r\nOrder By Sum([r].[Quantity]) Desc";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
    }

    /// <summary>
    /// 验证静态聚合函数按实际参数绑定两个查询来源。
    /// </summary>
    [Fact]
    public void Select_WhenStaticFunctionsReferenceTwoSources_ShouldBindEachActualParameter()
    {
        var query = Create().From<Row>().Join<Row, Row>((first, second) => first.GroupId == second.Id)
            .Select<Row, Row, Totals>((first, second) => new Totals
            {
                First = SqlFn.Sum(first.Quantity),
                Second = SqlFn.Sum(second.Quantity)
            });
        const string expected = "Select Sum([first].[Quantity]) As [First],Sum([second].[Quantity]) As [Second] \r\nFrom [Rows] As [first] \r\nJoin [Rows] As [second] On [first].[GroupId]=[second].[Id]";
        Assert.Equal(expected, query.ToSql());
    }

    /// <summary>
    /// 验证混合函数形式的投影替换与去重。
    /// </summary>
    [Fact]
    public void ReplaceSelect_WhenCombiningFunctionForms_ShouldReplaceAndDeduplicate()
    {
        var query = Create().From<Row>().Select<Row>(r => new { Count = SqlFn.CountAll() })
            .Select<Row>((r, f) => new { Count = f.CountAll() })
            .ReplaceSelect<Row>((r, f) => new { Total = f.Sum(r.Quantity) })
            .Select<Row>(r => new { Total = SqlFn.Sum(r.Quantity) });
        Assert.Equal("Select Sum([r].[Quantity]) As [Total] \r\nFrom [Rows] As [r]", query.ToSql());
    }

    /// <summary>
    /// 验证聚合别名与去重选项生成完整 SQL。
    /// </summary>
    [Fact]
    public void Select_WhenAggregatesUseAliasesAndDistinct_ShouldRenderFullContract()
    {
        var query = Create().From<Row>().Select<Row>(r => new
        {
            C = SqlFn.Count(r.Id, "C", true),
            A = SqlFn.Avg(r.Price),
            Lo = SqlFn.Min(r.Quantity),
            Hi = SqlFn.Max(r.Price),
            N = SqlFn.CountAll(),
            S = SqlFn.Sum(r.Quantity, true)
        });
        Assert.Equal("Select Count(Distinct [r].[Id]) As [C],Avg([r].[Price]) As [A],Min([r].[Quantity]) As [Lo],Max([r].[Price]) As [Hi],Count(*) As [N],Sum(Distinct [r].[Quantity]) As [S] \r\nFrom [Rows] As [r]", query.ToSql());
    }

    /// <summary>
    /// 验证函数投影在 DTO、数组和强类型形式中保留操作意图。
    /// </summary>
    [Fact]
    public void Select_WhenDtoArrayAndTypedProjectionUseFunctions_ShouldPreserveOperationIntent()
    {
        var query = Create().From<Row>()
            .Select<Row, Summary>(r => new Summary { Count = SqlFn.Count(r.Id), Total = SqlFn.Sum(r.Quantity) });
        Assert.Equal("Select Count([r].[Id]) As [Count],Sum([r].[Quantity]) As [Total] \r\nFrom [Rows] As [r]", query.ToSql());
        query.ReplaceSelect<Row>(r => new object[] { SqlFn.Count(r.Id, "Count"), SqlFn.Sum(r.Quantity, "Total") });
        Assert.Equal("Select Count([r].[Id]) As [Count],Sum([r].[Quantity]) As [Total] \r\nFrom [Rows] As [r]", query.ToSql());
    }

    /// <summary>
    /// 验证非法子句中的聚合函数被拒绝且查询状态不变。
    /// </summary>
    [Fact]
    public void Function_WhenUsedInInvalidClause_ShouldRejectWithoutChangingState()
    {
        var query = Create().From<Row>().Select<Row>(r => new { r.Id });
        const string expected = "Select [r].[Id] As [Id] \r\nFrom [Rows] As [r]";
        Assert.Throws<NotSupportedException>(() => query.Where<Row>(r => SqlFn.Count(r.Id) > 0L));
        Assert.Throws<NotSupportedException>(() => query.Join<Row, Row>((r, child) => SqlFn.Count(r.Id) == SqlFn.Count(child.Id)));
        Assert.Throws<NotSupportedException>(() => query.GroupBy<Row>(r => SqlFn.Count(r.Id)));
        Assert.Equal(expected, query.ToSql());
        var automatic = Create().From<Row>().SelectAll();
        Assert.Throws<NotSupportedException>(() => automatic.ExcludeSelect<Row>(r => SqlFn.Count(r.Id)));
        Assert.Equal("Select [Rows].[Id],[Rows].[GroupId],[Rows].[Price],[Rows].[Quantity] \r\nFrom [Rows]", automatic.ToSql());
    }

    /// <summary>
    /// 验证聚合参数不是外层成员时拒绝操作并回滚状态。
    /// </summary>
    [Fact]
    public void Function_WhenArgumentIsNotAnOuterMember_ShouldRejectAndRollback()
    {
        var query = Create().From<Row>().Select<Row>(r => new { r.Id });
        var closed = new Row();
        Assert.Throws<NotSupportedException>(() => query.Select<Row>(r => SqlFn.Sum(closed.Quantity)));
        Assert.Throws<NotSupportedException>(() => query.Select<Row>(r => SqlFn.Count(42)));
        Assert.Throws<NotSupportedException>(() => query.Select<Row>(r => SqlFn.Sum(r.Quantity + 1)));
        Assert.Throws<NotSupportedException>(() => query.Select<Row>(r => SqlFn.Count(SqlFn.Sum(r.Quantity))));
        Assert.Throws<NotSupportedException>(() => query.Select<Row>(r => PretendSqlFn.Count(r.Quantity)));
        Assert.Equal("Select [r].[Id] As [Id] \r\nFrom [Rows] As [r]", query.ToSql());
    }

    /// <summary>
    /// 验证非法或冲突的聚合别名被原子拒绝。
    /// </summary>
    [Fact]
    public void Function_WhenAliasIsInvalidOrConflicts_ShouldRejectAtomically()
    {
        var query = Create().From<Row>().Select<Row>(r => new { Count = SqlFn.Count(r.Id) });
        Assert.Throws<ArgumentException>(() => query.Select<Row>(r => new { Count = SqlFn.Sum(r.Quantity) }));
        Assert.Throws<ArgumentException>(() => query.ReplaceSelect<Row>(r => new { Count = SqlFn.Count(r.Id, "different") }));
        Assert.Throws<ArgumentException>(() => query.Having<Row>(r => SqlFn.Count(r.Id, "Count") > 0L));
        Assert.Throws<ArgumentException>(() => query.OrderBy<Row>(r => SqlFn.Sum(r.Quantity, "Total")));
        Assert.Equal("Select Count([r].[Id]) As [Count] \r\nFrom [Rows] As [r]", query.ToSql());
    }

    /// <summary>
    /// 验证聚合目标成员被忽略时在调用执行器前失败。
    /// </summary>
    /// <param name="generated">是否通过生成查询桥接路径执行。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToList_WhenAggregateTargetIsIgnored_ShouldRejectBeforeExecutor(bool generated)
    {
        var query = Create().From<Row>("r");
        if (generated)
            SqlGeneratedQueryBridge.Project(query, "Select", new[] { typeof(Row) }, new[] { "r" }, null,
                new[] { new SqlGeneratedColumn(0, "Id", "Count", typeof(long), SqlAggregateFunction.Count) });
        else
            query.Select<Row>(r => new { Count = SqlFn.Count(r.Id) });
        Assert.Throws<InvalidOperationException>(() => query.ToList<IgnoredSummary>());
        Assert.Equal("Select Count([r].[Id]) As [Count] \r\nFrom [Rows] As [r]", query.ToSql());
    }

    /// <summary>
    /// 验证生成桥接拒绝非法聚合子句且不修改查询。
    /// </summary>
    [Fact]
    public void GeneratedFunction_WhenInvalidClauseIsRequested_ShouldRejectBeforeChangingQuery()
    {
        var query = Create().From<Row>("r").Select<Row>(r => new { r.Id });
        var count = new SqlGeneratedColumn(0, "Id", null, typeof(long), SqlAggregateFunction.Count);
        Assert.Throws<NotSupportedException>(() =>
            SqlGeneratedQueryBridge.Compare(query, false, new[] { typeof(Row) }, new[] { "r" }, null, count, null, Operator.Greater, 0L));
        Assert.Equal("Select [r].[Id] As [Id] \r\nFrom [Rows] As [r]", query.ToSql());
        query.SelectAll();
        Assert.Throws<NotSupportedException>(() =>
            SqlGeneratedQueryBridge.Project(query, "ExcludeSelect", new[] { typeof(Row) }, new[] { "r" }, null, new[] { count }));
        Assert.Equal("Select [r].[Id],[r].[GroupId],[r].[Price],[r].[Quantity] \r\nFrom [Rows] As [r]", query.ToSql());
    }

    /// <summary>
    /// 提供SQL 函数投影测试使用的查询数据模型。
    /// </summary>
    [Table("Rows")]
    private sealed class Row
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key] public int Id { get; set; }
        /// <summary>
        /// 获取或设置分组标识。
        /// </summary>
        public int GroupId { get; set; }
        /// <summary>
        /// 获取或设置订单数量。
        /// </summary>
        public int? Quantity { get; set; }
        /// <summary>
        /// 获取或设置函数计算使用的价格值。
        /// </summary>
        public decimal Price { get; set; }
    }

    /// <summary>
    /// 承载SQL 函数投影查询结果的投影数据模型。
    /// </summary>
    private sealed class Summary {
        /// <summary>
        /// 获取或设置函数投影中的计数结果。
        /// </summary>
        public long Count { get; set; }
        /// <summary>
        /// 获取或设置函数投影中的总值。
        /// </summary>
        public int? Total { get; set; } }
    /// <summary>
    /// 承载SQL 函数投影查询结果的投影数据模型。
    /// </summary>
    private sealed class IgnoredSummary {
        /// <summary>
        /// 获取或设置按忽略列规则投影的聚合计数。
        /// </summary>
        [NotMapped] public long Count { get; set; } }
    /// <summary>
    /// 承载SQL 函数投影查询结果的投影数据模型。
    /// </summary>
    private sealed class Totals {
        /// <summary>
        /// 获取或设置第一项函数计算结果。
        /// </summary>
        public int? First { get; set; }
        /// <summary>
        /// 获取或设置第二项函数计算结果。
        /// </summary>
        public int? Second { get; set; } }
    /// <summary>
    /// 提供仅用于 SQL 投影解析且禁止实际执行的函数。
    /// </summary>
    private static class PretendSqlFn {
        /// <summary>
        /// 拒绝执行仅用于识别用户函数的测试标记。
        /// </summary>
        /// <param name="value">需要格式化或作为 SQL 参数的测试值。</param>
        /// <returns>此测试标记不返回正常结果，直接调用时始终抛出异常。</returns>
        public static long Count(object value) => throw new InvalidOperationException("不应执行用户函数。"); }
}
