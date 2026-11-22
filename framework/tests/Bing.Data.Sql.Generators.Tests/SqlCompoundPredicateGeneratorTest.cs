using Bing.Data.Sql.Generators;
using Bing.Data.Sql;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>取反表达式应生成可重用的条件描述，并保留嵌套逻辑结构。</summary>
    [Theory]
    [InlineData("Where<Row>(r => !(r.Id > 5 || r.Name.StartsWith(\"A\")))")]
    [InlineData("Where<Row>(r => !(r.Enabled && r.Id == 5))")]
    [InlineData("Where<Row>(r => r.Enabled && !(r.Id == 5 || r.Name == null))")]
    [InlineData("Where<Row>(r => !(r.Id == 5))")]
    [InlineData("Where<Row>(r => !r.Name.Contains(\"A\"))")]
    [InlineData("Where<Row>(r => !!(r.Id == 5))")]
    [InlineData("WhereIf<Row>(enabled, r => !(r.Id == 5 || r.Name.EndsWith(\"B\")))")]
    [InlineData("Where<Row, Row>((left, right) => !(left.Id == right.Id || right.Enabled), \"l\", \"r\")")]
    public void Generate_WhenPredicateIsNegated_ShouldCompileInStrictMode(string call)
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
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, bool enabled) => query.CALL;
            }
            """.Replace("CALL", call);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedPredicate.Not(", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>关闭取反条件时不应读取查询 Builder。</summary>
    [Fact]
    public void Generate_WhenNegatedWhereIfIsDisabled_ShouldNotAccessBuilder()
    {
        const string source = "using Bing.Data.Sql; public sealed class Row { public int Id { get; set; } } " +
            "public static class C { public static SqlLambdaQuery Apply(SqlLambdaQuery q) => " +
            "q.WhereIf<Row>(false, r => !(r.Id == 5 || r.Id == 6)); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        var assembly = LoadGeneratedAssembly(result);
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, new Mock<ISqlBuilder>().Object);

        Assert.Same(query, assembly.GetType("C")!.GetMethod("Apply")!.Invoke(null, new object[] { query }));
    }

    /// <summary>自定义取反运算不能被误译为 SQL 的内置 NOT。</summary>
    [Fact]
    public void Generate_WhenNegationUsesCustomOperator_ShouldReportStrictDiagnostic()
    {
        const string source = """
            using Bing.Data.Sql;
            public struct Flag { public static bool operator !(Flag value) => true; }
            public class Row { public Flag Value { get; set; } }
            public static class C
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery q) => q.Where<Row>(r => !r.Value);
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain("SqlGeneratedPredicate.Not(", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>取反不改变捕获值在严格模式下需要显式传参的限制。</summary>
    [Fact]
    public void Generate_WhenNegatedPredicateCapturesValue_ShouldReportStrictDiagnostic()
    {
        const string source = "using Bing.Data.Sql; public class Row { public int Id { get; set; } public bool Enabled { get; set; } } " +
            "public static class C { public static SqlLambdaQuery Apply(SqlLambdaQuery q, int id) => " +
            "q.Where<Row>(r => !(r.Id == id || r.Enabled)); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain("SqlGeneratedPredicate.Not(", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>常量比较、列比较和布尔属性的组合应在严格 AOT 下生成条件结构。</summary>
    [Theory]
    [InlineData("Where<Row>(r => r.Enabled && r.Visible)")]
    [InlineData("Where<Row>(r => r.Enabled && (!r.Visible || r.Id > 5))")]
    [InlineData("Where<Row>(r => (r.Id >= 1 && r.Id < 10) || r.ParentId == r.Id)")]
    [InlineData("WhereIf<Row>(enabled, r => r.Enabled || r.Id == 5)")]
    [InlineData("Where<Row>(r => r.Name == null || r.Amount >= 1.25m)")]
    [InlineData("Where<Row>(r => 5 < r.Id && r.Name == \"five\")")]
    [InlineData("Where<Row, Row>((left, right) => left.Id == right.ParentId && right.Enabled, \"l\", \"r\")")]
    public void Generate_WhenCompoundPredicateIsSupported_ShouldCompileInStrictMode(string call)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row
            {
                public bool Enabled { get; set; }
                public bool Visible { get; set; }
                public int Id { get; set; }
                public int ParentId { get; set; }
                public string Name { get; set; }
                public decimal Amount { get; set; }
            }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, bool enabled) => query.CALL;
            }
            """.Replace("CALL", call);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedQueryBridge.WherePredicate", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>关闭复合条件时不读取 Builder。</summary>
    [Fact]
    public void Generate_WhenCompoundWhereIfIsDisabled_ShouldNotAccessBuilder()
    {
        const string source = "using Bing.Data.Sql; public sealed class Row { public bool Enabled { get; set; } public int Id { get; set; } } " +
            "public static class C { public static SqlLambdaQuery Apply(SqlLambdaQuery q) => " +
            "q.WhereIf<Row>(false, r => r.Enabled || r.Id == 5); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        var assembly = LoadGeneratedAssembly(result);
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, new Mock<ISqlBuilder>().Object);

        Assert.Same(query, assembly.GetType("C")!.GetMethod("Apply")!.Invoke(null, new object[] { query }));
    }

    /// <summary>编译期不能安全识别的重载运算符不能按普通 SQL 比较生成。</summary>
    [Fact]
    public void Generate_WhenCompoundComparisonUsesCustomOperator_ShouldReportStrictDiagnostic()
    {
        const string source = """
            using Bing.Data.Sql;
            public struct Value
            {
                public static bool operator >(Value x, int y) => true;
                public static bool operator <(Value x, int y) => false;
            }
            public class Row { public Value Number { get; set; } public bool Enabled { get; set; } }
            public static class C
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery q) => q.Where<Row>(r => r.Enabled && r.Number > 5);
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
    }

    /// <summary>用户自定义列转换不能被当作 SQL 内置转换忽略。</summary>
    [Fact]
    public void Generate_WhenCompoundComparisonUsesCustomConversion_ShouldReportStrictDiagnostic()
    {
        const string source = """
            using Bing.Data.Sql;
            public struct Value { public static implicit operator int(Value value) => 0; }
            public class Row { public Value Number { get; set; } public bool Enabled { get; set; } }
            public static class C
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery q) => q.Where<Row>(r => r.Enabled && r.Number > 5);
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
    }

    /// <summary>兼容模式下不支持的捕获条件继续使用原有运行时路径。</summary>
    [Fact]
    public void Generate_WhenCompoundPredicateCapturesValue_ShouldFallbackInCompatibleMode()
    {
        const string source = "using Bing.Data.Sql; public class Row { public bool Enabled { get; set; } public int Id { get; set; } } " +
            "public static class C { public static SqlLambdaQuery Apply(SqlLambdaQuery q, int id) => " +
            "q.Where<Row>(r => r.Enabled && r.Id == id); }";

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        Assert.DoesNotContain("SqlGeneratedQueryBridge.WherePredicate", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>调用点条件描述初始化一次并在后续请求中复用。</summary>
    [Fact]
    public void Generate_WhenCompoundPredicateIsUsed_ShouldReuseDescriptor()
    {
        const string source = "using Bing.Data.Sql; public class Row { public bool Enabled { get; set; } public int Id { get; set; } } " +
            "public static class C { public static SqlLambdaQuery Apply(SqlLambdaQuery q) => " +
            "q.Where<Row>(r => r.Enabled && r.Id >= 5); }";
        var assembly = LoadGeneratedAssembly(RunGenerator(source));
        var descriptor = Assert.Single(assembly.GetType("Bing.Data.Sql.Generated.QueryCalls")!.GetNestedTypes(
            System.Reflection.BindingFlags.NonPublic), type => type.Name.StartsWith("Descriptor_", StringComparison.Ordinal));
        var field = descriptor.GetField("Predicate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

        Assert.True(field.IsInitOnly);
        Assert.Same(field.GetValue(null), field.GetValue(null));
    }
}
