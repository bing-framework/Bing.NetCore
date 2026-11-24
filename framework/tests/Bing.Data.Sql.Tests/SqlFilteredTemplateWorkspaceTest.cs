using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>过滤工作区导出的模板保留同次渲染合并的逻辑参数身份。</summary>
public sealed class SqlFilteredTemplateWorkspaceTest
{
    [Fact]
    public void ScopedTemplate_WhenFilterWorkspaceMergesFragment_ShouldKeepLogicalParameterKey()
    {
        var key = new SqlParameterKey("request", "id");
        var fragment = new SqlQueryFragment("select @ID as Id", new[]
            { new SqlParam("id", 7, DbType.Int32) }).WithParameterKey("id", key);
        var builder = new SqlServerBuilder(new SqlBuilderServices(
            options: new SqlOptions
            {
                QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
            }, filters: new[] { new NoOpFilter() }));
        builder.With("items", fragment).Select("*").From("items");

        var template = builder.ToTemplate(new SqlQueryScope("root"));
        var shape = new SqlQueryShapeTemplate(template.CreateFragment());
        var next = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 19 });

        Assert.Equal("With [items] \r\nAs (select @root_id as Id)\r\nSelect * \r\nFrom [items]", template.Sql);
        Assert.Equal(template.Sql, next.Sql);
        Assert.Equal(key, Assert.Single(template.CreateFragment().Slots));
        Assert.Equal(7, Assert.Single(template.Parameters).Value);
        Assert.Equal(19, Assert.Single(next.Parameters).Value);
        Assert.Equal(key, Assert.Single(next.Slots));
        Assert.Empty(builder.GetParams());
    }

    private sealed class NoOpFilter : ISqlFilter
    {
        public void Filter(SqlFilterContext context) { }
    }
}
