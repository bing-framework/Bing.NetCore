using Bing.Data.Sql;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>字符串谓词中的编译期常量应生成现有参数化比较，支持条件开关和别名。</summary>
    [Theory]
    [InlineData("Where<Row>(r => r.Name.Contains(\"at\"))", "Compare", "Contains", false)]
    [InlineData("Where<Row>(r => r.Name.StartsWith(\"up\"), \"r\")", "Compare", "Starts", true)]
    [InlineData("Where<Row>(r => (r.Name.EndsWith(\"ed\")))", "Compare", "Ends", true)]
    [InlineData("WhereIf<Row>(enabled, r => r.Name.Contains(\"at\"))", "OptionalCompare", "Contains", true)]
    [InlineData("WhereIf<Row>(enabled, r => r.Name.StartsWith(\"up\"), \"r\")", "OptionalCompare", "Starts", true)]
    [InlineData("WhereIf<Row>(enabled, r => r.Name.EndsWith(\"ed\"))", "OptionalCompare", "Ends", false)]
    [InlineData("Where<Row, Row>((l, r) => r.Name.Contains(\"at\"), \"l\", \"r\")", "Compare", "Contains", true)]
    [InlineData("Where<Row>(r => r.Name.Contains(Token))", "Compare", "Contains", true)]
    [InlineData("WhereIf<Row>(enabled, r => r.Name.Contains(null))", "OptionalCompare", "Contains", true)]
    public void Generate_WhenStringPredicateHasConstant_ShouldUseGeneratedComparison(
        string operation, string bridge, string comparison, bool strict)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row { public string Name { get; set; } }
            public static class Consumer
            {
                public const string Token = "a\"b\\c";
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, bool enabled) => query.OPERATION;
            }
            """.Replace("OPERATION", operation);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = strict ? "true" : "false" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedQueryBridge." + bridge + "(source", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("global::Bing.Operator." + comparison, result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>关闭可选字符串谓词时应返回原查询，也不访问 Builder。</summary>
    [Theory]
    [InlineData("Contains")]
    [InlineData("StartsWith")]
    [InlineData("EndsWith")]
    public void Generate_WhenStringWhereIfIsDisabled_ShouldReturnOriginalQuery(string method)
    {
        var source = "using Bing.Data.Sql; public sealed class Row { public string Name { get; set; } } " +
            "public static class Consumer { public static SqlLambdaQuery Apply(SqlLambdaQuery q) => " +
            "q.WhereIf<Row>(false, r => r.Name.METHOD(\"a\")); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source.Replace("METHOD", method), analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        var assembly = LoadGeneratedAssembly(result);
        var apply = assembly.GetType("Consumer")!.GetMethod("Apply")!;
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, new Mock<ISqlBuilder>().Object);

        Assert.Same(query, apply.Invoke(null, new object[] { query }));
    }

    /// <summary>动态值和其他方法重载保留诊断，避免生成错误的 LIKE 语义。</summary>
    [Theory]
    [InlineData("r.Name.Contains(value)", false)]
    [InlineData("r.Name.Contains(value)", true)]
    [InlineData("r.Name.Contains('a')", false)]
    [InlineData("r.Name.Contains('a')", true)]
    [InlineData("r.Name.StartsWith(\"a\", System.StringComparison.OrdinalIgnoreCase)", true)]
    [InlineData("r.Name.ToLower().Contains(\"a\")", true)]
    [InlineData("r.Custom.Contains(\"a\")", true)]
    public void Generate_WhenStringPredicateIsOutsideSupportedShape_ShouldKeepDiagnostic(string predicate, bool strict)
    {
        var source = "using Bing.Data.Sql; public sealed class Custom { public bool Contains(string x) => true; } " +
            "public sealed class Row { public string Name { get; set; } public Custom Custom { get; set; } } " +
            "public static class Consumer { public static SqlLambdaQuery Apply(SqlLambdaQuery q, string value) => " +
            "q.Where<Row>(r => PREDICATE); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = strict ? "true" : "false" });

        var result = RunGenerator(source.Replace("PREDICATE", predicate), analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == (strict ? "BINGSQLG001" : "BINGSQLG002"));
        Assert.DoesNotContain("SqlGeneratedQueryBridge.Compare(source", result.GeneratedSource, StringComparison.Ordinal);
    }
}
