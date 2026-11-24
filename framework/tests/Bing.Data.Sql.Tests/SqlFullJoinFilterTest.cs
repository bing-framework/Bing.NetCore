using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>Full Join 来源预过滤与冻结计划合同。</summary>
public sealed class SqlFullJoinFilterTest
{
    /// <summary>双侧先过滤再连接，重复渲染不改变原草稿。</summary>
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSql")]
    public void Render_ShouldPrefilterBothSourcesWithoutChangingDraft(string provider)
    {
        var filter = new TenantFilter();
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { filter });
        SqlBuilderBase builder = provider == "SqlServer" ? new SqlServerBuilder(services) : new PostgreSqlBuilder(services);
        builder.Select("a.Id,b.Id").From<LeftRow>("a")
            .FullJoin<RightRow>("b").On<LeftRow, RightRow>((a, b) => a.Id == b.Id);
        var from = builder.FromClause;
        var join = builder.JoinClause;
        const string sql = "Select [a].[Id],[b].[Id] \r\nFrom (Select * From [left_rows] As [a] Where [a].[TenantId]=@_p_0) As [a] \r\nFull Join (Select * From [right_rows] As [b] Where [b].[TenantId]=@_p_1) As [b] On [a].[Id]=[b].[Id]";
        var expected = provider == "SqlServer" ? sql : sql.Replace("[", "\"").Replace("]", "\"");
        var first = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder, includeDebugSql: true);
        var second = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder, includeDebugSql: true);
        Assert.Equal(expected, first.Sql);
        Assert.Equal(expected, second.Sql);
        Assert.Equal(expected.Replace("@_p_0", "7").Replace("@_p_1", "7"), first.DebugSql);
        Assert.Equal(new object[] { 7, 7 }, first.Parameters.Select(x => x.Value));
        Assert.Same(from, builder.FromClause);
        Assert.Same(join, builder.JoinClause);
        Assert.Empty(builder.GetParams());
        Assert.Equal(2, filter.Calls);
    }
    /// <summary>冻结 Count/Page 复用来源过滤和同一次参数。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetPlan_ShouldReusePrefilteredSourcesForCountAndPage(bool customTail)
    {
        var filter = new TenantFilter();
        var services = new SqlBuilderServices(filters: new ISqlFilter[] {filter}, options: new SqlOptions {
            QueryCapabilities = new SqlQueryCapabilities {Pagination = SqlQueryCapabilityState.Supported}});
        var builder = customTail ? new TailBuilder(services) : new SqlServerBuilder(services);
        var suffix = customTail ? " /* tail */" : "";
        builder.Select("a.Id").From<LeftRow>("a").FullJoin<RightRow>("b")
            .On<LeftRow, RightRow>((a, b) => a.Id == b.Id).OrderBy("a.Id");
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
        var plan = query.GetPlan(forPaging: true);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(2, 10, "a.Id"));
        const string tail = "\r\nFrom (Select * From [left_rows] As [a] Where [a].[TenantId]=@_p_0) As [a] \r\nFull Join (Select * From [right_rows] As [b] Where [b].[TenantId]=@_p_1) As [b] On [a].[Id]=[b].[Id]";
        Assert.Equal("Select Count(*) " + tail + suffix, count.CommandText);
        Assert.Equal("Select [a].[Id] " + tail + " \r\nOrder By [a].[Id] \r\nOffset @_p_2 Rows Fetch Next @_p_3 Rows Only" + suffix, page.CommandText);
        Assert.Equal(new object[] { 7, 7 }, Assert.IsType<SqlParam[]>(count.Parameters).Select(x => x.Value));
        Assert.Equal(new object[] { 7, 7, 10, 10 }, Assert.IsType<SqlParam[]>(page.Parameters).Select(x => x.Value));
        Assert.Equal(1, filter.Calls);
        Assert.Empty(builder.GetParams());
    }
    /// <summary>独立执行副本和再次 Clone 应保留来源条件，不重复调用过滤器。</summary>
    [Fact]
    public void Clone_WhenExecutionSourcesArePrefiltered_ShouldRetainSqlAndParameterBindings()
    {
        var filter = new TenantFilter();
        var builder = new SqlServerBuilder(new SqlBuilderServices(filters: new ISqlFilter[] { filter }));
        builder.Select("a.Id,b.Id").From<LeftRow>("a").FullJoin<RightRow>("b")
            .On<LeftRow, RightRow>((a, b) => a.Id == b.Id);
        var execution = builder.CreateExecutionBuilderSnapshot();
        var clone = execution.Clone();
        const string expected = "Select [a].[Id],[b].[Id] \r\nFrom (Select * From [left_rows] As [a] Where [a].[TenantId]=@_p_0) As [a] \r\nFull Join (Select * From [right_rows] As [b] Where [b].[TenantId]=@_p_1) As [b] On [a].[Id]=[b].[Id]";
        Assert.Equal(expected, execution.ToSql());
        Assert.Equal(expected, clone.ToSql());
        Assert.Equal(new object[] { 7, 7 }, clone.GetParams().Values);
        Assert.Empty(builder.GetParams());
        Assert.Equal(1, filter.Calls);
    }

    /// <summary>来源预过滤不绕过 Provider 原有的 Full Join 能力门禁。</summary>
    [Theory]
    [InlineData("MySql", "bing.mysql")]
    [InlineData("Sqlite", "bing.sqlite")]
    public void Render_WhenProviderDisallowsFullJoin_ShouldStillReject(string provider, string key)
    {
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { new TenantFilter() });
        SqlBuilderBase builder = provider == "MySql" ? new MySqlBuilder(services) : new SqliteBuilder(services);
        builder.Select("a.Id").From<LeftRow>("a").FullJoin<RightRow>("b")
            .On<LeftRow, RightRow>((a, b) => a.Id == b.Id);
        var error = Assert.Throws<NotSupportedException>(() => builder.ToSql());
        Assert.Equal($"Provider {key} 的当前查询能力配置不支持 Full Join。", error.Message);
        Assert.Empty(builder.GetParams());
    }

    private sealed class TailBuilder : SqlServerBuilder
    {
        internal TailBuilder(SqlBuilderServices services, IParameterManager parameters = null) : base(services, parameters) { }
        protected override bool SupportsMutationCheckpoint => true;
        public override string ToSql() => base.ToSql() + " /* tail */";
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameters) => new TailBuilder(Services, parameters);
    }

    [Table("left_rows")]
    private sealed class LeftRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    [Table("right_rows")]
    private sealed class RightRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class TenantFilter : ISqlFilter
    {
        internal int Calls { get; private set; }

        public void Filter(SqlFilterContext context)
        {
            Calls++;
            foreach (var source in context.Sources)
                context.AddPredicate(source, context.GetColumn(source, nameof(LeftRow.TenantId)), 7);
        }
    }
}
