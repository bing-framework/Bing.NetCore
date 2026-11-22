using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>常量位于左侧时，生成调用应将列放在左侧并反转比较方向。</summary>
    [Theory]
    [InlineData("1 == r.Id", "Equal")]
    [InlineData("1 != r.Id", "NotEqual")]
    [InlineData("1 < r.Id", "Greater")]
    [InlineData("1 <= r.Id", "GreaterEqual")]
    [InlineData("1 > r.Id", "Less")]
    [InlineData("1 >= r.Id", "LessEqual")]
    [InlineData("null == r.Name", "Equal")]
    [InlineData("null != r.Name", "NotEqual")]
    [InlineData("\"Alice\" == r.Name", "Equal")]
    [InlineData("(long?)5 <= r.Optional", "GreaterEqual")]
    [InlineData("1.25m <= r.Price", "GreaterEqual")]
    [InlineData("Status.Open == r.Status", "Equal")]
    [InlineData("float.NaN != r.Ratio", "NotEqual")]
    public void Generate_WhenConstantPrecedesColumn_ShouldReverseComparison(string predicate, string comparison)
    {
        var source = """
            using Bing.Data.Sql;
            public enum Status { Open = 1 }
            public sealed class Row
            {
                public long Id { get; set; }
                public string Name { get; set; }
                public long? Optional { get; set; }
                public decimal Price { get; set; }
                public Status Status { get; set; }
                public float Ratio { get; set; }
            }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery q, bool enabled) =>
                    q.Where<Row>(r => PREDICATE).WhereIf<Row>(enabled, r => PREDICATE);
            }
            """.Replace("PREDICATE", predicate);

        foreach (var strict in new[] { false, true })
        {
            var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
                { ["build_property.BingSqlAotStrict"] = strict ? "true" : "false" });
            var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

            AssertNoCompilationErrors(result);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
            Assert.Contains("SqlGeneratedQueryBridge.Compare", result.GeneratedSource, StringComparison.Ordinal);
            Assert.Contains("SqlGeneratedQueryBridge.OptionalCompare", result.GeneratedSource, StringComparison.Ordinal);
            Assert.Equal(2, result.GeneratedSource.Split("global::Bing.Operator." + comparison + ", ", StringSplitOptions.None).Length - 1);
            Assert.DoesNotContain("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
        }
    }

    /// <summary>非常量左操作数仍沿用现有兼容回退和严格模式诊断。</summary>
    [Theory]
    [InlineData(false, "BINGSQLG002")]
    [InlineData(true, "BINGSQLG001")]
    public void Generate_WhenLeftOperandIsCaptured_ShouldKeepExistingDiagnostic(bool strict, string diagnosticId)
    {
        const string source = "using Bing.Data.Sql; public sealed class Row { public int Id { get; set; } } " +
            "public static class C { public static SqlLambdaQuery Apply(SqlLambdaQuery q, int limit) => " +
            "q.Where<Row>(r => limit < r.Id); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = strict ? "true" : "false" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId);
        Assert.DoesNotContain("SqlGeneratedQueryBridge.Compare(source", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>用户自定义比较运算符的语义不可通过交换操作数推断。</summary>
    [Fact]
    public void Generate_WhenOperatorIsUserDefined_ShouldKeepFallback()
    {
        const string source = """
            using Bing.Data.Sql;
            public struct Number
            {
                public static implicit operator Number(int value) => default;
                public static bool operator <(Number left, Number right) => true;
                public static bool operator >(Number left, Number right) => false;
            }
            public sealed class Row { public Number Value { get; set; } }
            public static class C
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery q) => q.Where<Row>(r => 1 < r.Value);
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        Assert.DoesNotContain("SqlGeneratedQueryBridge.Compare(source", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>列需要用户自定义转换时，不应将转换后的比较直接改写为列比较。</summary>
    [Theory]
    [InlineData("Where<Row>(r => 1 < r.Value)", false)]
    [InlineData("WhereIf<Row>(true, r => 1 < r.Value)", false)]
    [InlineData("Where<Row>(r => 1 < r.Value)", true)]
    [InlineData("WhereIf<Row>(true, r => 1 < r.Value)", true)]
    public void Generate_WhenColumnHasUserDefinedConversion_ShouldKeepFallback(string operation, bool strict)
    {
        var source = """
            using Bing.Data.Sql;
            public struct Number { public static implicit operator int(Number value) => 0; }
            public sealed class Row { public Number Value { get; set; } }
            public static class C
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery q) => q.OPERATION;
            }
            """.Replace("OPERATION", operation);

        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = strict ? "true" : "false" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == (strict ? "BINGSQLG001" : "BINGSQLG002"));
        Assert.DoesNotContain("SqlGeneratedQueryBridge.Compare(source", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlGeneratedQueryBridge.OptionalCompare(source", result.GeneratedSource, StringComparison.Ordinal);
    }
}
