using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Dapper.Tests.Builders;

/// <summary>Oracle 的冻结排序派生保持 Offset/Fetch 与字符串参数合同。</summary>
public sealed class OraclePageOrderIrTest
{
    [Theory]
    [InlineData("unordered")]
    [InlineData("ordered")]
    [InlineData("paged")]
    public void RequestedOrder_FromPureIr_ShouldKeepOracleSqlAndValues(string state)
    {
        var builder = new OracleBuilder(new SqlBuilderServices(options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
        })).Select("Name").From("samples").Where("Status", "active");
        if (state != "unordered") builder.OrderBy("Name");
        if (state == "paged") builder.Skip(5).Take(2);
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        const string body = "Select \"Name\" \r\nFrom \"samples\" \r\nWhere \"Status\"=:p_0";
        var sourceSql = body + (state == "unordered" ? "" : " \r\nOrder By \"Name\"") +
            (state == "paged" ? " \r\nOffset :p_1 Rows Fetch Next :p_2 Rows Only" : "");

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Name Desc,Id Desc"));
        var expected = body + " \r\nOrder By \"Name\"" + (state == "unordered" ? " Desc" : "") +
            ",\"Id\" Desc \r\nOffset :p_1 Rows Fetch Next :p_2 Rows Only";
        Assert.Equal(expected, page.CommandText);
        AssertParameters(page, "active", "8", "4");
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2, "Id,Name"));
        Assert.Equal(expected, repeated.CommandText);
        AssertParameters(repeated, "active", "2", "2");
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);
        Assert.Equal("Select Count(*) \r\nFrom \"samples\" \r\nWhere \"Status\"=:p_0", count.CommandText);
        AssertParameters(count, "active");
        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, state == "paged" ? new object[] { "active", "5", "2" } : new object[] { "active" });
    }

    /// <summary>首次添加排序仍执行 Oracle 分页能力校验。</summary>
    [Fact]
    public void NewOrder_WhenPaginationIsUnsupported_ShouldRejectAndKeepSource()
    {
        var source = SqlQueryPlan.Create(new OracleBuilder(new SqlBuilderServices(options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Unsupported }
        })).Select("Name").From("samples").Where("Status", "active")).Freeze();

        Assert.Throws<NotSupportedException>(() =>
            SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3, "Name Desc")));

        Assert.Equal("Select \"Name\" \r\nFrom \"samples\" \r\nWhere \"Status\"=:p_0", source.CommandText);
        AssertParameters(source, "active");
    }

    private static void AssertParameters(SqlQueryPlan plan, params object[] values)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(Enumerable.Range(0, values.Length).Select(index => $":p_{index}"), parameters.Select(p => p.Name));
        Assert.Equal(values, parameters.Select(p => p.Value));
    }
}
