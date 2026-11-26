using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>单来源的具名结果类型投影应与对象投影一样支持静态只读表达式。</summary>
    [Theory]
    [InlineData("Select")]
    [InlineData("ReplaceSelect")]
    public void CachedTypedProjection_WhenSingleSource_ShouldGenerateSql(string method)
    {
        var source = $$"""
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class ProjectionRow { public int Id { get; set; } }
            public sealed class RowSummary { public int Key { get; set; } }
            public static class Consumer
            {
                private static readonly Expression<Func<ProjectionRow, RowSummary>> Projection =
                    row => new RowSummary { Key = row.Id };
                public static SqlLambdaQuery Apply(SqlLambdaQuery query) =>
                    query.From<ProjectionRow>("r").{{method}}<ProjectionRow, RowSummary>(Projection, "r");
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var generated = RunGenerator(source, analyzerConfigOptionsProvider: strict);

        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedQueryBridge.Project", generated.GeneratedSource, StringComparison.Ordinal);
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new SqliteBuilder());
        var apply = LoadGeneratedAssembly(generated).GetType("Consumer")!.GetMethod("Apply")!;
        Assert.Equal("Select `r`.`Id` As `Key` \r\nFrom `ProjectionRow` As `r`",
            ((SqlLambdaQuery)apply.Invoke(null, new object[] { query })!).ToSql());
    }

    /// <summary>两个实体参数的表达式仍走明确诊断，不能误认作单来源的具名投影。</summary>
    [Theory]
    [InlineData("Select")]
    [InlineData("ReplaceSelect")]
    public void CachedTypedProjection_WhenMultipleSources_ShouldRetainFallbackDiagnostic(string method)
    {
        var source = $$"""
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class ProjectionRow { public int Id { get; set; } }
            public sealed class OtherRow { public int Id { get; set; } }
            public static class Consumer
            {
                private static readonly Expression<Func<ProjectionRow, OtherRow, object[]>> Projection =
                    (row, other) => new object[] { row.Id, other.Id };
                public static SqlLambdaQuery Apply(SqlLambdaQuery query) =>
                    query.From<ProjectionRow>("r").{{method}}<ProjectionRow, OtherRow>(Projection, "r", "o");
            }
            """;

        var generated = RunGenerator(source);

        AssertNoCompilationErrors(generated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
    }
}
