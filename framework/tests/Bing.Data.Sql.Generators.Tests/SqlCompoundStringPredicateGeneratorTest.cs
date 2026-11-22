using Bing.Data.Sql;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>复合条件中的常量字符串匹配应与布尔及列比较共同生成。</summary>
    [Theory]
    [InlineData("Where<Row>(r => r.Enabled && r.Name.Contains(\"ab\"))", true)]
    [InlineData("Where<Row>(r => r.Name.StartsWith(\"a\") || r.Id > 5)", false)]
    [InlineData("WhereIf<Row>(enabled, r => r.Name.EndsWith(\"z\") && (r.Enabled || r.Id == 5))", true)]
    [InlineData("Where<Row>(r => r.Name.Contains(Token) || r.Name.StartsWith(\"\"))", true)]
    [InlineData("Where<Row, Row>((left, right) => left.Name.Contains(\"a\") || right.Name.EndsWith(\"b\"), \"l\", \"r\")", true)]
    public void Generate_WhenCompoundPredicateContainsStringMatches_ShouldUseGeneratedPredicate(string operation, bool strict)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row
            {
                public bool Enabled { get; set; }
                public int Id { get; set; }
                public string Name { get; set; }
            }
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
        Assert.Contains("SqlGeneratedQueryBridge.WherePredicate", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>关闭复合条件时不触碰 Builder。</summary>
    [Fact]
    public void Generate_WhenCompoundStringWhereIfIsDisabled_ShouldNotAccessBuilder()
    {
        const string source = "using Bing.Data.Sql; public sealed class Row { public string Name { get; set; } public int Id { get; set; } } " +
            "public static class C { public static SqlLambdaQuery Apply(SqlLambdaQuery q) => " +
            "q.WhereIf<Row>(false, r => r.Name.Contains(\"a\") || r.Id == 5); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        var assembly = LoadGeneratedAssembly(result);
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, new Mock<ISqlBuilder>().Object);

        Assert.Same(query, assembly.GetType("C")!.GetMethod("Apply")!.Invoke(null, new object[] { query }));
    }

    /// <summary>复合条件沿用单个字符串谓词的重载和运行时值限制。</summary>
    [Theory]
    [InlineData("r.Name.Contains(value)", true)]
    [InlineData("r.Name.Contains(value)", false)]
    [InlineData("r.Name.Contains('a')", true)]
    [InlineData("r.Name.StartsWith(\"a\", System.StringComparison.OrdinalIgnoreCase)", true)]
    [InlineData("r.Name.ToLower().Contains(\"a\")", true)]
    [InlineData("r.Custom.Contains(\"a\")", true)]
    public void Generate_WhenCompoundStringPredicateIsUnsupported_ShouldKeepDiagnostic(string predicate, bool strict)
    {
        var source = "using Bing.Data.Sql; public sealed class Custom { public bool Contains(string x) => true; } " +
            "public sealed class Row { public string Name { get; set; } public Custom Custom { get; set; } public bool Enabled { get; set; } } " +
            "public static class Consumer { public static SqlLambdaQuery Apply(SqlLambdaQuery q, string value) => " +
            "q.Where<Row>(r => r.Enabled && PREDICATE); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = strict ? "true" : "false" });

        var result = RunGenerator(source.Replace("PREDICATE", predicate), analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == (strict ? "BINGSQLG001" : "BINGSQLG002"));
        Assert.DoesNotContain("SqlGeneratedQueryBridge.WherePredicate", result.GeneratedSource, StringComparison.Ordinal);
    }
}
