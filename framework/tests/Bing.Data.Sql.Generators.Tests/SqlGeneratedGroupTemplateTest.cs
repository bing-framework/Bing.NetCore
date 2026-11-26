using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>分组查询的模板应按本次别名、排序方向和可选条件隔离结构。</summary>
    [Fact]
    public void GeneratedGroupTemplate_WhenShapeChanges_ShouldReuseOnlyMatchingSql()
    {
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            public sealed class TemplateGroupRow { public int Id { get; set; } public int GroupId { get; set; } }
            public static class Consumer
            {
                public static string Render(ISqlQueryPlanExecutor executor, ISqlBuilder builder,
                    string alias, bool enabled, int minimum, bool descending) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<TemplateGroupRow>(alias)
                        .WhereIf<TemplateGroupRow, int>(enabled, minimum, (row, value) => row.Id >= value, alias)
                        .Select<TemplateGroupRow>(row => row.GroupId, alias)
                        .GroupBy<TemplateGroupRow>(row => row.GroupId, alias)
                        .OrderBy<TemplateGroupRow>(row => row.GroupId, descending, alias).ToSql();
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(source, analyzerConfigOptionsProvider: strict);
        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedQueryTemplate.Apply(source, Template_", generated.GeneratedSource, StringComparison.Ordinal);

        var render = LoadGeneratedAssembly(generated).GetType("Consumer")!.GetMethod("Render")!;
        var executor = new Mock<ISqlQueryPlanExecutor>().Object;
        var services = SqlBuilderServices.CreateDefault();
        string Build(string alias, bool enabled, int minimum, bool descending) => (string)render.Invoke(null,
            new object[] { executor, new SqliteBuilder(services), alias, enabled, minimum, descending })!;
        var first = Build("r", true, 2, false);
        var sameShape = Build("r", true, 3, false);
        var descending = Build("r", true, 2, true);
        var otherAlias = Build("s", true, 2, false);
        var disabled = Build("r", false, 2, false);

        Assert.Equal("Select `r`.`GroupId` \r\nFrom `TemplateGroupRow` As `r` \r\nWhere `r`.`Id`>=@_p_0 \r\nGroup By `r`.`GroupId` \r\nOrder By `r`.`GroupId`", first);
        Assert.Same(first, sameShape);
        Assert.Equal("Select `r`.`GroupId` \r\nFrom `TemplateGroupRow` As `r` \r\nWhere `r`.`Id`>=@_p_0 \r\nGroup By `r`.`GroupId` \r\nOrder By `r`.`GroupId` Desc", descending);
        Assert.Equal("Select `s`.`GroupId` \r\nFrom `TemplateGroupRow` As `s` \r\nWhere `s`.`Id`>=@_p_0 \r\nGroup By `s`.`GroupId` \r\nOrder By `s`.`GroupId`", otherAlias);
        Assert.Equal("Select `r`.`GroupId` \r\nFrom `TemplateGroupRow` As `r` \r\nGroup By `r`.`GroupId` \r\nOrder By `r`.`GroupId`", disabled);
    }
}
