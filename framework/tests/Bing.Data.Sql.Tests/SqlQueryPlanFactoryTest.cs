using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>公开计划工厂在创建时冻结查询，后续执行和派生只使用独立快照。</summary>
public sealed class SqlQueryPlanFactoryTest
{
    /// <summary>两种 Builder 工厂都必须在调用时隔离 SQL、集合参数及公开参数副本。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_WhenSourceChanges_ShouldKeepSqlAndParameters(bool useSplitOn)
    {
        var payload = new byte[] { 1, 2 };
        var builder = new TestSqlBuilder().Select("Id").From("Users").Where("Payload", payload);
        var plan = useSplitOn ? SqlQueryPlan.Create(builder, "Key") : SqlQueryPlan.Create(builder);
        builder.Where("Status", "changed");
        payload[0] = 99;

        const string sql = "Select [Id] \r\nFrom [Users] \r\nWhere [Payload]=@_p_0";
        Assert.Equal(sql, plan.CommandText);
        Assert.False(plan.IsBuilderPlan);
        Assert.True(SqlBuilderRuntimeBridge.HasExecutionSnapshot(plan));
        Assert.Equal(useSplitOn ? "Key" : "Id", plan.SplitOn);
        Assert.Same(plan, plan.Freeze());
        var exposed = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.Single(exposed).Value));
        Assert.IsType<byte[]>(exposed[0].Value)[0] = 88;
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.Single(
            Assert.IsType<SqlParam[]>(plan.Parameters)).Value));
        Assert.Equal(sql, SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan).Sql);
    }

    /// <summary>Count 和并行页面使用创建时的来源，不能重新读取调用方草稿。</summary>
    [Fact]
    public async Task Create_WhenDerivedConcurrently_ShouldKeepCapturedSource()
    {
        var builder = new TestSqlBuilder().Select("Id").From("Users").Where("Status", 7).OrderBy("Id");
        var plan = SqlQueryPlan.Create(builder);
        builder.Where("Changed", 99);

        await Task.WhenAll(Enumerable.Range(1, 16).Select(number => Task.Run(() =>
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(number, 2, "Id"));
            Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Status]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
            Assert.Equal(new object[] { 7, (number - 1) * 2, 2 },
                Assert.IsType<SqlParam[]>(page.Parameters).Select(parameter => parameter.Value));
            var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
            Assert.Equal("Select Count(*) \r\nFrom [Users] \r\nWhere [Status]=@_p_0", count.CommandText);
            Assert.Equal(7, Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
        })));

        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Status]=@_p_0 \r\nOrder By [Id]", plan.CommandText);
        Assert.Equal(7, Assert.Single(Assert.IsType<SqlParam[]>(plan.Parameters)).Value);
    }

    /// <summary>分页对象的可变状态也应在工厂调用时捕获，而非执行时读取。</summary>
    [Fact]
    public void Create_WhenPagerChanges_ShouldKeepDefaultPagerSnapshot()
    {
        var pager = new Pager(2, 3, "Id");
        var builder = new TestSqlBuilder().Select("Id").From("Users").Page(pager);
        var plan = SqlQueryPlan.Create(builder);
        pager.Page = 9;
        pager.PageSize = 1;
        var first = SqlBuilderRuntimeBridge.GetPlanPager(plan, null);
        first.Page = 8;

        var captured = SqlBuilderRuntimeBridge.GetPlanPager(plan, null);
        Assert.Equal(2, captured.Page);
        Assert.Equal(3, captured.PageSize);
        Assert.Equal("Id", captured.Order);
        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nOrder By [Id] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", plan.CommandText);
        Assert.Equal(new object[] { 3, 3 }, Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value));
    }

    /// <summary>动态过滤在每次创建计划时取一次值，已创建计划的派生不重新执行过滤器。</summary>
    [Fact]
    public void Create_WhenFilterChanges_ShouldCaptureOneValuePerPlan()
    {
        var filter = new SequenceFilter();
        var builder = new TestSqlBuilder(new SqlBuilderServices(filters: new[] { filter }), TestDialect.Instance)
            .Select("Id").From<FilteredRow>("r");
        var first = SqlQueryPlan.Create(builder);
        var second = SqlQueryPlan.Create(builder);

        for (var index = 0; index < 2; index++)
        {
            var plan = index == 0 ? first : second;
            var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(2, 3, "Id"));
            Assert.Equal("Select [Id] \r\nFrom [rows] As [r] \r\nWhere [r].[TenantId]=@_p_0", plan.CommandText);
            Assert.Equal("Select Count(*) \r\nFrom [rows] As [r] \r\nWhere [r].[TenantId]=@_p_0", count.CommandText);
            Assert.Equal("Select [Id] \r\nFrom [rows] As [r] \r\nWhere [r].[TenantId]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
            Assert.Equal(index + 1, Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
            Assert.Equal(new object[] { index + 1, 3, 3 }, Assert.IsType<SqlParam[]>(page.Parameters).Select(parameter => parameter.Value));
            Assert.Same(plan, plan.Freeze());
        }
        Assert.Equal(2, filter.Calls);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>非法写入操作在克隆或渲染前拒绝，不能通过冻结抹掉其操作类型。</summary>
    [Theory]
    [InlineData(SqlOperationKind.InsertValues)]
    [InlineData(SqlOperationKind.InsertSelect)]
    [InlineData(SqlOperationKind.Update)]
    [InlineData(SqlOperationKind.Delete)]
    public void Create_WhenBuilderContainsWrite_ShouldRejectBeforeRendering(SqlOperationKind operation)
    {
        var builder = new Mock<ISqlBuilder>(MockBehavior.Strict);
        builder.SetupGet(value => value.OperationKind).Returns(operation);
        Assert.Throws<InvalidOperationException>(() => SqlQueryPlan.Create(builder.Object));
        builder.Verify(value => value.Clone(), Times.Never);
        builder.Verify(value => value.ToSql(), Times.Never);
    }

    /// <summary>默认描述也必须在冻结前校验，不能让第三方写入类型在转成 IR 后丢失。</summary>
    [Theory]
    [InlineData(SqlOperationKind.InsertValues)]
    [InlineData(SqlOperationKind.InsertSelect)]
    [InlineData(SqlOperationKind.Update)]
    [InlineData(SqlOperationKind.Delete)]
    public void GetPlan_WhenDraftContainsWrite_ShouldRejectBeforeCloning(SqlOperationKind operation)
    {
        var builder = new Mock<ISqlBuilder>(MockBehavior.Strict);
        builder.SetupGet(value => value.OperationKind).Returns(operation);
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder.Object);
        Assert.Throws<InvalidOperationException>(() => query.GetPlan());
        builder.Verify(value => value.Clone(), Times.Never);
        builder.Verify(value => value.ToSql(), Times.Never);
    }

    /// <summary>无效工厂参数不应触发 Builder 行为。</summary>
    [Fact]
    public void Create_WhenArgumentsInvalid_ShouldFailBeforeRendering()
    {
        var builder = new Mock<ISqlBuilder>(MockBehavior.Strict);
        Assert.Throws<ArgumentNullException>(() => SqlQueryPlan.Create((ISqlBuilder)null));
        Assert.Throws<ArgumentException>(() => SqlQueryPlan.Create(builder.Object, " "));
        builder.VerifyNoOtherCalls();
    }

    /// <summary>预取消只建立执行生命周期，不渲染 SQL；查询草稿仍隔离供后续正常调用。</summary>
    [Fact]
    public void GetPlan_WhenPreCancelled_ShouldAvoidRenderingAndKeepDraftIsolated()
    {
        var filter = new SequenceFilter();
        var builder = new TestSqlBuilder(new SqlBuilderServices(filters: new[] { filter }), TestDialect.Instance)
            .Select("Id").From<FilteredRow>("r");
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var cancelled = query.GetPlan(cancellation.Token);
        Assert.False(cancelled.IsBuilderPlan);
        Assert.Null(cancelled.CommandText);
        Assert.Null(cancelled.Parameters);
        Assert.Equal(0, filter.Calls);
        Assert.Equal(query.QueryContextId, cancelled.QueryContextId);
        cancelled.NotifyExecutionStarted();
        cancelled.NotifyExecutionFinished();
        builder.Where("Changed", 99);

        var plan = query.GetPlan();
        Assert.Equal("Select [Id] \r\nFrom [rows] As [r] \r\nWhere [r].[TenantId]=@_p_0", plan.CommandText);
        Assert.Equal(1, Assert.Single(Assert.IsType<SqlParam[]>(plan.Parameters)).Value);
        Assert.Equal(1, filter.Calls);
    }

    [Table("rows")]
    private sealed class FilteredRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class SequenceFilter : ISqlFilter
    {
        internal int Calls;
        public void Filter(SqlFilterContext context)
        {
            var source = context.Sources.Single(item => item.EntityType == typeof(FilteredRow));
            context.AddPredicate(source, context.GetColumn(source, nameof(FilteredRow.TenantId)), ++Calls);
        }
    }
}
