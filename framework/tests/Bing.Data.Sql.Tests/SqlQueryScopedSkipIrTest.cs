using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>作用域模板保留已有 Skip 参数的身份，后续分页和 Count 不得遗留旧值。</summary>
public sealed class SqlQueryScopedSkipIrTest
{
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("postgresql")]
    [InlineData("sqlite")]
    public void ScopedSkipTemplate_WhenPagedAgain_ShouldReplaceOffsetAndCountOnlySourceParameters(string provider)
    {
        ISqlBuilder builder = provider switch
        {
            "mysql" => new MySqlBuilder(),
            "postgresql" => new PostgreSqlBuilder(),
            "sqlite" => new SqliteBuilder(),
            _ => new TestSqlBuilder()
        };
        builder.Select("Name").From("samples").Where("Status", 7).OrderBy("Name").Skip(5);
        var source = SqlQueryPlan.Create(builder.ToTemplate(new SqlQueryScope("request")));
        var first = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));
        var second = SqlBuilderRuntimeBridge.CreatePagePlan(first, new Pager(2, 2));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(second);
        var open = provider == "sqlserver" ? "[" : provider == "postgresql" ? "\"" : "`";
        var close = provider == "sqlserver" ? "]" : open;
        string Q(string name) => open + name + close;
        var body = $"From {Q("samples")} \r\nWhere {Q("Status")}=@request_p_0";
        var pageSql = $"Select {Q("Name")} \r\n{body} \r\nOrder By {Q("Name")} \r\n" +
            (provider == "sqlserver" ? "Offset @request_p_1 Rows Fetch Next @request_p_2 Rows Only" :
                "Limit @request_p_2 OFFSET @request_p_1");

        Assert.Equal(pageSql, first.CommandText);
        Assert.Equal(pageSql, second.CommandText);
        Assert.Equal(new object[] { 7, 8, 4 }, Values(first));
        Assert.Equal(new object[] { 7, 2, 2 }, Values(second));
        Assert.Equal($"Select Count(*) \r\n{body}", count.CommandText);
        Assert.Equal(new object[] { 7 }, Values(count));
        Assert.Equal(new object[] { 7, 5 }, Values(source));
    }

    private static object[] Values(SqlQueryPlan plan) =>
        Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value).ToArray();
}
