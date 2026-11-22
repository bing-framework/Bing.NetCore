using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Bing.Properties;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>冻结查询直接添加分页排序，保留既有追加和去重合同。</summary>
public sealed class SqlQueryPageOrderIrTest
{
    public static IEnumerable<object[]> PageCases()
    {
        foreach (var shape in new[] { "simple", "group", "distinct", "aggregate" })
        foreach (var state in new[] { "unordered", "ordered", "paged" })
        foreach (var executionEntry in new[] { false, true })
            yield return new object[] { shape, state, executionEntry };
    }

    /// <summary>四种投影和三种初始状态均无需复制或重渲染 Builder。</summary>
    [Theory]
    [MemberData(nameof(PageCases))]
    public void RequestedOrder_ShouldDerivePageWithoutBuilder(string shape, string state, bool executionEntry)
    {
        var calls = new Calls();
        var builder = new CountedBuilder(calls);
        if (shape == "aggregate") builder.CountAll("Total");
        else builder.Select("Name");
        builder.From("samples").Where("Status", 7);
        if (shape == "group") builder.GroupBy("Name");
        if (shape == "distinct") builder.Distinct();
        if (state != "unordered") builder.OrderBy("Name");
        if (state == "paged") builder.Skip(5).Take(2);
        var source = SqlQueryPlan.Create(builder).Freeze();
        var sourceSql = source.CommandText;
        var sourceValues = Values(source);
        calls.Clones = calls.Renders = 0;
        var pager = new Pager(3, 4, "Name Desc,Id Desc");

        var page = executionEntry
            ? SqlBuilderRuntimeBridge.CreatePageExecutionPlan(source, pager, new RejectBuilderSource())
            : SqlBuilderRuntimeBridge.CreatePagePlan(source, pager);
        var select = shape == "aggregate" ? "Select Count(*) As [Total]" :
            shape == "distinct" ? "Select Distinct [Name]" : "Select [Name]";
        const string body = "From [samples] \r\nWhere [Status]=@_p_0";
        var group = shape == "group" ? " \r\nGroup By [Name]" : "";
        var order = state == "unordered" ? "[Name] Desc,[Id] Desc" : "[Name],[Id] Desc";
        var sql = $"{select} \r\n{body}{group} \r\nOrder By {order} \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only";
        Assert.Equal(sql, page.CommandText);
        Assert.Equal(new object[] { 7, 8, 4 }, Values(page));
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2, "Id,Name"));
        Assert.Equal(sql, repeated.CommandText);
        Assert.Equal(new object[] { 7, 2, 2 }, Values(repeated));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);
        Assert.Equal(shape == "simple" ? $"Select Count(*) \r\n{body}" :
            $"Select Count(*) \r\nFrom ({select} \r\n{body}{group}) As [t]", count.CommandText);
        Assert.Equal(new object[] { 7 }, Values(count));
        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal(sourceValues, Values(source));
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
    }

    /// <summary>首次渲染和缓存入口都能为没有排序的默认计划补充分页排序。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultUnorderedPlan_ShouldAcceptOrderWithoutPlanningSource(bool cached)
    {
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object,
            new TestSqlBuilder().Select("Name").From("samples"));
        if (cached) query.ToSql();
        var source = query.GetPlan();
        Assert.Null(source.PlanningBuilder);

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3, "Name Desc"));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name] Desc \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 3, 3 }, Values(page));
        Assert.Equal("Select [Name] \r\nFrom [samples]", source.CommandText);
    }

    /// <summary>逻辑 Lambda 排序已绑定的映射和别名保持不变，字符串排序仍放在逻辑项之前。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LambdaOrder_ShouldKeepFrozenAliasMappingAndItemOrder(bool automatic, bool explicitAlias)
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
            new TestSqlBuilder(options: new SqlOptions { AutoGenerateTableAlias = automatic }))
            .From<Person>(explicitAlias ? "s" : null).Select<Person>(p => p.Name).OrderBy<Person>(p => p.Name);
        var source = PureIr(((ISqlQueryBuilderAccessor)query).GetSqlBuilder());
        var alias = explicitAlias ? "s" : automatic ? "p" : null;
        var prefix = $"[{alias ?? "People"}].";
        var from = "[People]" + (alias == null ? "" : $" As [{alias}]");
        Assert.Equal($"Select {prefix}[DisplayName] \r\nFrom {from} \r\nOrder By {prefix}[DisplayName]", source.CommandText);

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3, "Id Desc"));

        Assert.Equal($"Select {prefix}[DisplayName] \r\nFrom {from} \r\nOrder By [Id] Desc,{prefix}[DisplayName] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 3, 3 }, Values(page));
        Assert.Equal($"Select Count(*) \r\nFrom {from}", SqlBuilderRuntimeBridge.CreateCountPlan(page).CommandText);
    }

    /// <summary>限定前缀分别去重；无前缀请求匹配所有已有同名列，原方向优先。</summary>
    [Fact]
    public void QualifiedOrder_ShouldKeepExistingDeduplication()
    {
        var source = PureIr(new TestSqlBuilder().Select("a.Name").From("samples", "a")
            .OrderBy("a.Name Desc"));
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source,
            new Pager(2, 3, "Name,a.Name,b.Name, b.Name Desc,Id Asc"));
        Assert.Equal("Select [a].[Name] \r\nFrom [samples] As [a] \r\nOrder By [a].[Name] Desc,[b].[Name],[Id] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 3, 3 }, Values(page));
    }

    /// <summary>原始排序不解析为普通列，保留位置与表达式。</summary>
    [Fact]
    public void RawOrder_ShouldKeepFrozenExpression()
    {
        var builder = new TestSqlBuilder().Select("Name").From("samples");
        ((ISqlQueryClauseAccessor)builder).OrderByClause.AppendSql("case when [Name] is null then 1 else 0 end");
        var source = PureIr(builder);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3, "Name"));
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By case when [Name] is null then 1 else 0 end,[Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
    }

    /// <summary>只替换外层排序边界，子查询的排序、分页和值保持不变。</summary>
    [Fact]
    public void NewOrder_ShouldKeepNestedPage()
    {
        var nested = new TestSqlBuilder().Select("Name").From("archive").OrderBy("Name").Skip(1).Take(3);
        var source = PureIr(new TestSqlBuilder().Select("Name").From(nested, "a").OrderBy("Name").Skip(5).Take(2));
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Id Desc"));
        Assert.Equal("Select [Name] \r\nFrom (Select [Name] \r\nFrom [archive] \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only) As [a] \r\nOrder By [Name],[Id] Desc \r\nOffset @_p_2 Rows Fetch Next @_p_3 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 1, 3, 8, 4 }, Values(page));
        Assert.Equal(new object[] { 1, 3, 5, 2 }, Values(source));
    }

    /// <summary>调试源的并发派生不共享可变排序、参数或执行上下文。</summary>
    [Fact]
    public async Task ConcurrentOrders_ShouldKeepSourceAndEachPageIndependent()
    {
        var frozen = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From("samples")).Freeze();
        var source = SqlQueryPlan.Create(SqlQueryIr.Create(
            SqlBuilderRuntimeBridge.CreateExecutionSnapshot(frozen, true), CommandType.Text, null), "split");
        source.SetContext("query-110", "parent-110", "Count");
        await Task.WhenAll(Enumerable.Range(1, 16).Select(number => Task.Run(() =>
        {
            var order = number % 2 == 0 ? "Name" : "Id Desc";
            var sqlOrder = number % 2 == 0 ? "[Name]" : "[Id] Desc";
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(number, 3, order));
            Assert.Equal($"Select [Name] \r\nFrom [samples] \r\nOrder By {sqlOrder} \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
            Assert.Equal(new object[] { (number - 1) * 3, 3 }, Values(page));
            Assert.Equal("query-110", page.QueryContextId);
            Assert.Equal("parent-110", page.ParentQueryContextId);
            Assert.Equal("Data", page.Phase);
            Assert.Equal("split", page.SplitOn);
            Assert.Equal(number, page.QueryIr.CreateDefaultPagerSnapshot().Page);
        })));
        Assert.Equal("Select [Name] \r\nFrom [samples]", source.CommandText);
        Assert.Empty(Values(source));
    }

    /// <summary>没有有效排序时使用原分页诊断，失败前不转换分页值，失败后仍可重试。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , , ")]
    public void EmptyOrder_ShouldKeepDiagnosticAndSource(string order)
    {
        var dialect = new IntegerDialect();
        var source = PureIr(new TestSqlBuilder(parameterManager: new ParameterManager(dialect))
            .Select("Name").From("samples").Where("Status", 7));
        dialect.Calls = 0;

        var error = Assert.Throws<ArgumentException>(() =>
            SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3, order)));

        Assert.Equal(LibraryResource.OrderIsEmptyForPage, error.Message);
        Assert.Equal(0, dialect.Calls);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3, "Name"));
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 107, 103, 103 }, Values(page));
        Assert.Equal(2, dialect.Calls);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0", source.CommandText);
        Assert.Equal(new object[] { 107 }, Values(source));
    }

    /// <summary>新增排序不能绕过参数限额；普通筛选值不再转换，新分页值只转换一次。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NewOrder_ShouldKeepParameterLimitAndConversion(int maximum)
    {
        var dialect = new IntegerDialect();
        var manager = new AdvancedParameterLimitManager(new ParameterManager(dialect), maximum, "test");
        var source = PureIr(new TestSqlBuilder(parameterManager: manager)
            .Select("Name").From("samples").Where("Status", 7));
        dialect.Calls = 0;

        if (maximum < 3)
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Name")));
            Assert.Equal($"SQL Provider 'test' 的参数数量超出上限。当前参数数量: {maximum}；尝试添加后数量: {maximum + 1}；最大参数数量: {maximum}。", error.Message);
            Assert.Equal(0, dialect.Calls);
        }
        else
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Name"));
            Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
            Assert.Equal(new object[] { 107, 108, 104 }, Values(page));
            Assert.Equal(2, dialect.Calls);
        }
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0", source.CommandText);
        Assert.Equal(new object[] { 107 }, Values(source));
    }

    private static SqlQueryPlan PureIr(ISqlBuilder builder)
    {
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        return SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
    }
    private static object[] Values(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters).Select(p => p.Value).ToArray();
    [Table("People")]
    private sealed class Person { [Column("DisplayName")] public string Name { get; set; } = ""; }
    private sealed class Calls { internal int Clones; internal int Renders; }
    private sealed class CountedBuilder : TestSqlBuilder
    {
        private readonly Calls _calls;
        internal CountedBuilder(Calls calls, IParameterManager parameters = null) : base(parameterManager: parameters) => _calls = calls;
        public override ISqlBuilder Clone() { _calls.Clones++; return base.Clone(); }
        public override string ToSql() { _calls.Renders++; return base.ToSql(); }
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameters) => new CountedBuilder(_calls, parameters);
    }
    private sealed class RejectBuilderSource : ISqlQueryBuilderSource
    {
        public ISqlBuilder CreateIndependentSqlBuilder() => throw new InvalidOperationException("IR 分页不应创建 Builder。");
    }
    private sealed class IntegerDialect : DialectBase
    {
        internal int Calls;
        // 参数容器可使用另一套方言；标识符必须继续采用 Builder 的方括号方言。
        public override char OpeningIdentifier => '`';
        public override char ClosingIdentifier => '`';
        public override object GetParamValue(object value) { Calls++; return value is int i ? i + 100 : value; }
    }
}
