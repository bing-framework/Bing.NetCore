using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using System.Reflection;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private const string ExcludeTemplateSource = """
        using System;
        using System.Collections.Generic;
        using System.Linq.Expressions;
        using Bing.Data.Sql;
        using Bing.Data.Sql.Builders;

        public sealed class ExcludeTemplateRow
        {
            public int Id { get; set; }
            public string Secret { get; set; } = "";
        }

        public static class SharedExcludeColumns
        {
            public static readonly Expression<Func<ExcludeTemplateRow, object>> Secret = row => row.Secret;
        }

        public static class ExcludeTemplateConsumer
        {
            public static List<ExcludeTemplateRow> RenderStatic(ISqlQueryPlanExecutor executor, ISqlBuilder builder,
                string alias, bool enabled, int minimum) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder)
                    .From<ExcludeTemplateRow>(alias)
                    .WhereIf<ExcludeTemplateRow, int>(enabled, minimum, (row, value) => row.Id >= value, alias)
                    .SelectAll()
                    .ExcludeSelect<ExcludeTemplateRow>(SharedExcludeColumns.Secret, alias)
                    .ToList<ExcludeTemplateRow>();

            public static List<ExcludeTemplateRow> RenderInline(ISqlQueryPlanExecutor executor, ISqlBuilder builder,
                string alias, bool enabled, int minimum) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder)
                    .From<ExcludeTemplateRow>(alias)
                    .WhereIf<ExcludeTemplateRow, int>(enabled, minimum, (row, value) => row.Id >= value, alias)
                    .SelectAll()
                    .ExcludeSelect<ExcludeTemplateRow>(row => row.Secret, alias)
                    .ToList<ExcludeTemplateRow>();
        }
        """;

    /// <summary>静态只读排除列模板应按查询形状复用 SQL 并绑定每次查询计划的参数。</summary>
    [Fact]
    public void GeneratedExcludeTemplate_WhenShapeChanges_ShouldReuseOnlyMatchingSql()
    {
        var generated = GenerateExcludeTemplates();
        var consumer = LoadGeneratedAssembly(generated).GetType("ExcludeTemplateConsumer")!;
        var services = SqlBuilderServices.CreateDefault();

        var firstBuilder = new SqliteBuilder(services);
        var first = Render("RenderStatic", firstBuilder, "r", true, 2);
        var sameShapeBuilder = new SqliteBuilder(services);
        var sameShape = Render("RenderStatic", sameShapeBuilder, "r", true, 3);
        var disabledBuilder = new SqliteBuilder(services);
        var disabled = Render("RenderStatic", disabledBuilder, "r", false, 4);
        var otherAliasBuilder = new SqliteBuilder(services);
        var otherAlias = Render("RenderStatic", otherAliasBuilder, "s", true, 5);

        Assert.Equal(
            "Select `r`.`Id` \r\nFrom `ExcludeTemplateRow` As `r` \r\nWhere `r`.`Id`>=@_p_0",
            first.CommandText);
        Assert.Same(first.CommandText, sameShape.CommandText);
        Assert.Equal(
            "Select `r`.`Id` \r\nFrom `ExcludeTemplateRow` As `r`",
            disabled.CommandText);
        Assert.Equal(
            "Select `s`.`Id` \r\nFrom `ExcludeTemplateRow` As `s` \r\nWhere `s`.`Id`>=@_p_0",
            otherAlias.CommandText);
        Assert.NotSame(first.CommandText, otherAlias.CommandText);

        Assert.Equal(2, Assert.Single(Assert.IsType<SqlParam[]>(first.Parameters)).Value);
        Assert.Equal(3, Assert.Single(Assert.IsType<SqlParam[]>(sameShape.Parameters)).Value);
        Assert.Empty(Assert.IsType<SqlParam[]>(disabled.Parameters));
        Assert.Equal(5, Assert.Single(Assert.IsType<SqlParam[]>(otherAlias.Parameters)).Value);

        SqlQueryPlan Render(string methodName, SqliteBuilder builder, string alias, bool enabled, int minimum)
        {
            var executor = DispatchProxy.Create<ISqlQueryPlanExecutor, CapturingPlanExecutor>();
            consumer.GetMethod(methodName)!.Invoke(null, new object[] { executor, builder, alias, enabled, minimum });
            return Assert.IsType<SqlQueryPlan>(((CapturingPlanExecutor)executor).Plan);
        }
    }

    /// <summary>内联排除列表达式应生成完整 SQL 并对同形状请求保留各自计划参数。</summary>
    [Fact]
    public void GeneratedExcludeTemplate_WhenColumnExpressionIsInline_ShouldBindCurrentBuilderValues()
    {
        var generated = GenerateExcludeTemplates();
        var consumer = LoadGeneratedAssembly(generated).GetType("ExcludeTemplateConsumer")!;
        var services = SqlBuilderServices.CreateDefault();
        var firstBuilder = new SqliteBuilder(services);
        var first = Render(firstBuilder, 6);
        var sameShapeBuilder = new SqliteBuilder(services);
        var sameShape = Render(sameShapeBuilder, 7);

        Assert.Equal(
            "Select `r`.`Id` \r\nFrom `ExcludeTemplateRow` As `r` \r\nWhere `r`.`Id`>=@_p_0",
            first.CommandText);
        Assert.Same(first.CommandText, sameShape.CommandText);
        Assert.Equal(6, Assert.Single(Assert.IsType<SqlParam[]>(first.Parameters)).Value);
        Assert.Equal(7, Assert.Single(Assert.IsType<SqlParam[]>(sameShape.Parameters)).Value);

        SqlQueryPlan Render(SqliteBuilder builder, int minimum)
        {
            var executor = DispatchProxy.Create<ISqlQueryPlanExecutor, CapturingPlanExecutor>();
            consumer.GetMethod("RenderInline")!.Invoke(null, new object[] { executor, builder, "r", true, minimum });
            return Assert.IsType<SqlQueryPlan>(((CapturingPlanExecutor)executor).Plan);
        }
    }

    /// <summary>并发访问同一个模板时，查询计划不能串用其他请求的参数。</summary>
    [Fact]
    public void GeneratedExcludeTemplate_WhenExecutedConcurrently_ShouldKeepPlansIndependent()
    {
        var generated = GenerateExcludeTemplates();
        var render = LoadGeneratedAssembly(generated).GetType("ExcludeTemplateConsumer")!.GetMethod("RenderStatic")!;
        var services = SqlBuilderServices.CreateDefault();
        var plans = new SqlQueryPlan[32];
        Parallel.For(0, plans.Length, value =>
        {
            var executor = DispatchProxy.Create<ISqlQueryPlanExecutor, CapturingPlanExecutor>();
            render.Invoke(null, new object[] { executor, new SqliteBuilder(services), "r", true, value });
            plans[value] = Assert.IsType<SqlQueryPlan>(((CapturingPlanExecutor)executor).Plan);
        });

        for (var value = 0; value < plans.Length; value++)
        {
            Assert.Equal("Select `r`.`Id` \r\nFrom `ExcludeTemplateRow` As `r` \r\nWhere `r`.`Id`>=@_p_0",
                plans[value].CommandText);
            Assert.Equal(value, Assert.Single(Assert.IsType<SqlParam[]>(plans[value].Parameters)).Value);
        }
    }

    /// <summary>重复排除与排空投影的行为应与原查询路径一致。</summary>
    [Fact]
    public void GeneratedExcludeTemplate_WhenColumnsRepeatOrAllExcluded_ShouldPreserveExistingSemantics()
    {
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            public sealed class ExcludeBoundaryRow
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public string Secret { get; set; }
            }
            public static class ExcludeBoundaryConsumer
            {
                public static string Repeated(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<ExcludeBoundaryRow>("r")
                        .WhereIf<ExcludeBoundaryRow, int>(true, 1, (row, value) => row.Id >= value, "r")
                        .SelectAll().ExcludeSelect<ExcludeBoundaryRow>(row => row.Secret)
                        .ExcludeSelect<ExcludeBoundaryRow>(row => row.Secret).ToSql();
                public static string Empty(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<ExcludeBoundaryRow>("r")
                        .WhereIf<ExcludeBoundaryRow, int>(true, 1, (row, value) => row.Id >= value, "r")
                        .SelectAll().ExcludeSelect<ExcludeBoundaryRow>(row => row.Id)
                        .ExcludeSelect<ExcludeBoundaryRow>(row => row.Name)
                        .ExcludeSelect<ExcludeBoundaryRow>(row => row.Secret).ToSql();
            }
            """;

        (string Repeated, Exception Empty) Execute(bool interceptors)
        {
            var result = RunGenerator(source, enableInterceptors: interceptors);
            AssertNoCompilationErrors(result);
            if (interceptors)
                Assert.Contains("SqlGeneratedQueryTemplate.Apply(source, Template_", result.GeneratedSource, StringComparison.Ordinal);
            var consumer = LoadGeneratedAssembly(result).GetType("ExcludeBoundaryConsumer")!;
            object[] Arguments() => new object[] { new Mock<ISqlQueryPlanExecutor>().Object, new SqliteBuilder() };
            var repeated = (string)consumer.GetMethod("Repeated")!.Invoke(null, Arguments())!;
            var error = Assert.Throws<TargetInvocationException>(() => consumer.GetMethod("Empty")!.Invoke(null, Arguments()));
            return (repeated, Assert.IsAssignableFrom<Exception>(error.InnerException));
        }

        var baseline = Execute(false);
        var generated = Execute(true);
        Assert.Equal("Select `r`.`Id`,`r`.`Name` \r\nFrom `ExcludeBoundaryRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", generated.Repeated);
        Assert.Equal(baseline.Repeated, generated.Repeated);
        Assert.Equal(baseline.Empty.GetType(), generated.Empty.GetType());
        Assert.Equal(baseline.Empty.Message, generated.Empty.Message);
    }

    /// <summary>包含未接入的调用时，整条链保留原有逐调用生成路径。</summary>
    [Fact]
    public void GeneratedExcludeTemplate_WhenChainContainsUnknownOperation_ShouldUseExistingPath()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class ExcludeFallbackRow { public int Id { get; set; } public string Secret { get; set; } }
            public static class ExcludeFallbackConsumer
            {
                public static string Render(ISqlQuery root, bool enabled) => root.From<ExcludeFallbackRow>("r")
                    .WhereIf<ExcludeFallbackRow, int>(enabled, 1, (row, value) => row.Id >= value, "r")
                    .SelectAll().ExcludeSelect<ExcludeFallbackRow>(row => row.Secret, "r").Distinct().ToSql();
            }
            """;
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain("SqlGeneratedQueryTemplate.Operation(", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedQueryBridge.Project(source, \"ExcludeSelect\"", result.GeneratedSource, StringComparison.Ordinal);
    }

    private static GeneratorResult GenerateExcludeTemplates()
    {
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(ExcludeTemplateSource, analyzerConfigOptionsProvider: strict);

        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.True(
            generated.GeneratedSource.Split(
                "SqlGeneratedQueryTemplate.Apply(source, Template_",
                StringSplitOptions.None).Length - 1 >= 2,
            "Both static and inline exclusion chains should call generated query templates.");
        Assert.Contains("ExcludeSelect", generated.GeneratedSource, StringComparison.Ordinal);

        return generated;
    }
}
