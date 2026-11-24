using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>直接验证来源过滤在连接拓扑中的放置位置，以及默认 Provider 的完整 SQL。</summary>
public sealed class SqlFilterPlacementPlannerTest
{
    /// <summary>Right Join 保留侧未被后续外连接置空时，可以在最终 Where 过滤。</summary>
    [Theory]
    [InlineData("None")]
    [InlineData("Left")]
    [InlineData("Inner")]
    public void Plan_WhenRightPreservedSourceRemainsPreserved_ShouldUseWhere(string followingJoin)
    {
        var builder = CreateBuilder();
        if (followingJoin == "Left")
            builder.LeftJoin<LastRow>("c").On<RightRow, LastRow>((b, c) => b.Id == c.Id);
        if (followingJoin == "Inner")
            builder.Join<LastRow>("c").On<RightRow, LastRow>((b, c) => b.Id == c.Id);
        var context = CreateContext(builder);

        var placement = Assert.Single(SqlFilterPlacementPlanner.Plan(context));

        Assert.Equal(context.Sources.Single(source => source.Alias == "b").SourceId,
            placement.Predicate.SourceId);
        Assert.Null(placement.JoinSourceId);
        Assert.Equal(7, placement.Predicate.Value);
    }

    /// <summary>后续 Right Join 使原保留来源可空时，应在后续 On 过滤，保留最后右表的未匹配行。</summary>
    [Fact]
    public void Plan_WhenLaterRightJoinMakesSourceNullable_ShouldUseLaterJoinOn()
    {
        var builder = CreateBuilder();
        builder.RightJoin<LastRow>("c").On<RightRow, LastRow>((b, c) => b.Id == c.Id);
        var context = CreateContext(builder);

        var placement = Assert.Single(SqlFilterPlacementPlanner.Plan(context));

        Assert.Equal(context.Sources.Single(source => source.Alias == "c").SourceId,
            placement.JoinSourceId);
    }

    /// <summary>后续 Full Join 需要来源预过滤，不能放在最终 Where。</summary>
    [Fact]
    public void Plan_WhenLaterFullJoinRequiresPrefiltering_ShouldPrefilterSource()
    {
        var builder = CreateBuilder();
        builder.FullJoin<LastRow>("c").On<RightRow, LastRow>((b, c) => b.Id == c.Id);

        var placement = Assert.Single(SqlFilterPlacementPlanner.Plan(CreateContext(builder)));

        Assert.True(placement.PrefilterSource);
        Assert.Equal("join_0", placement.Predicate.SourceId);
        Assert.Null(placement.JoinSourceId);
    }

    /// <summary>默认 Provider 应同时在 On 过滤非保留侧、在 Where 过滤保留侧，并隔离重复渲染的参数。</summary>
    [Theory]
    [InlineData("SqlServer", "Select [b].[Id] \r\nFrom [left_rows] As [a] \r\nRight Join [right_rows] As [b] On [a].[Id]=[b].[Id] And [a].[TenantId]=@_p_0 \r\nWhere [b].[TenantId]=@_p_1")]
    [InlineData("MySql", "Select `b`.`Id` \r\nFrom `left_rows` As `a` \r\nRight Join `right_rows` As `b` On `a`.`Id`=`b`.`Id` And `a`.`TenantId`=@_p_0 \r\nWhere `b`.`TenantId`=@_p_1")]
    [InlineData("PostgreSql", "Select \"b\".\"Id\" \r\nFrom \"left_rows\" As \"a\" \r\nRight Join \"right_rows\" As \"b\" On \"a\".\"Id\"=\"b\".\"Id\" And \"a\".\"TenantId\"=@_p_0 \r\nWhere \"b\".\"TenantId\"=@_p_1")]
    public void Render_WhenBothSidesHaveFilters_ShouldPreservePlacementAndDraft(string provider, string expectedSql)
    {
        var filter = new TenantFilter();
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { filter });
        SqlBuilderBase builder = provider switch
        {
            "SqlServer" => new SqlServerBuilder(services),
            "MySql" => new MySqlBuilder(services),
            "PostgreSql" => new PostgreSqlBuilder(services),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
        builder.Select("b.Id").From<LeftRow>("a")
            .RightJoin<RightRow>("b").On<LeftRow, RightRow>((a, b) => a.Id == b.Id);
        var join = builder.JoinClause;
        var where = builder.WhereClause;

        var first = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder, includeDebugSql: true);
        var second = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder, includeDebugSql: true);

        Assert.Equal(expectedSql, first.Sql);
        Assert.Equal(expectedSql, second.Sql);
        Assert.Equal(expectedSql.Replace("@_p_0", "7").Replace("@_p_1", "7"), first.DebugSql);
        Assert.Equal(first.DebugSql, second.DebugSql);
        Assert.Equal(new object[] { 7, 7 }, first.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 7, 7 }, second.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(2, filter.Calls);
        Assert.Same(join, builder.JoinClause);
        Assert.Same(where, builder.WhereClause);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>本次扩展过滤语义不改变 SQLite Provider 的 Right Join 能力门禁。</summary>
    [Fact]
    public void Render_WhenSqliteProfileDisallowsRightJoin_ShouldStillReject()
    {
        var builder = new SqliteBuilder(new SqlBuilderServices(filters: new ISqlFilter[] { new TenantFilter() }));
        builder.Select("b.Id").From<LeftRow>("a")
            .RightJoin<RightRow>("b").On<LeftRow, RightRow>((a, b) => a.Id == b.Id);

        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", exception.Message);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>冻结查询的 Count 与 Page 应复用同次过滤值和连接位置，不再请求全局过滤器。</summary>
    [Fact]
    public void GetPlan_WhenRightJoinIsFiltered_ShouldReusePlacementsForCountAndPage()
    {
        var filter = new TenantFilter();
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { filter }, options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
        });
        var builder = new SqlServerBuilder(services);
        builder.Select("b.Id").From<LeftRow>("a")
            .RightJoin<RightRow>("b").On<LeftRow, RightRow>((a, b) => a.Id == b.Id)
            .OrderBy("b.Id");
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);

        var plan = query.GetPlan(forPaging: true);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(2, 10, "b.Id"));

        const string sourceSql = "\r\nFrom [left_rows] As [a] \r\nRight Join [right_rows] As [b] On [a].[Id]=[b].[Id] And [a].[TenantId]=@_p_0 \r\nWhere [b].[TenantId]=@_p_1";
        Assert.Equal("Select Count(*) " + sourceSql, count.CommandText);
        Assert.Equal("Select [b].[Id] " + sourceSql + " \r\nOrder By [b].[Id] \r\nOffset @_p_2 Rows Fetch Next @_p_3 Rows Only",
            page.CommandText);
        Assert.Equal(new object[] { 7, 7 }, Assert.IsType<SqlParam[]>(count.Parameters).Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 7, 7, 10, 10 }, Assert.IsType<SqlParam[]>(page.Parameters).Select(parameter => parameter.Value));
        Assert.Equal(1, filter.Calls);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>创建包含单个右外连接的未过滤草稿。</summary>
    private static SqlServerBuilder CreateBuilder()
    {
        var builder = new SqlServerBuilder();
        builder.Select("b.Id").From<LeftRow>("a")
            .RightJoin<RightRow>("b").On<LeftRow, RightRow>((a, b) => a.Id == b.Id);
        return builder;
    }

    /// <summary>只为右侧来源提交谓词，直接调用规划器而不依赖渲染的间接覆盖。</summary>
    private static SqlFilterContext CreateContext(SqlServerBuilder builder)
    {
        var context = new SqlFilterContext(SqlServerDialect.Instance, builder, new SqlBuilderServices());
        var source = context.Sources.Single(item => item.EntityType == typeof(RightRow));
        context.AddPredicate(source, context.GetColumn(source, nameof(RightRow.TenantId)), 7);
        return context;
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

    [Table("last_rows")]
    private sealed class LastRow
    {
        public int Id { get; set; }
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
