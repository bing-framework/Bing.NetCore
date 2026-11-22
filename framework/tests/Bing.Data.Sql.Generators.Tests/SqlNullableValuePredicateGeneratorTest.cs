using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>内置可空提升应由生成的强类型转换完成，单一目标不创建值数组。</summary>
    [Theory]
    [InlineData("long?", "int", "r.Id == value", "long?")]
    [InlineData("long?", "int", "r.Id == ((value)) || r.Id >= value", "long?")]
    [InlineData("long?", "int?", "r.Id == value", "long?")]
    [InlineData("long", "int?", "value < r.Id || r.Id == value", "long?")]
    [InlineData("int?", "int", "r.Id == value", "int?")]
    [InlineData("double?", "float?", "r.Id == value", "double?")]
    [InlineData("decimal?", "int?", "r.Id == value", "decimal?")]
    public void Generate_WhenExplicitValueNeedsNullablePromotion_ShouldEmitScalarConversion(
        string columnType, string valueType, string predicate, string targetType)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row { public COLUMN Id { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, VALUE input) =>
                    query.Where<Row, VALUE>(input, (r, value) => PREDICATE);
            }
            """.Replace("COLUMN", columnType).Replace("VALUE", valueType).Replace("PREDICATE", predicate);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.Contains(", (" + targetType + ")@value)", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new object[]{", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
        var descriptor = Assert.Single(result.GeneratedSource.Split('\n'), line =>
            line.Contains("SqlGeneratedPredicate Predicate =", StringComparison.Ordinal));
        Assert.DoesNotContain("@value", descriptor, StringComparison.Ordinal);
    }

    /// <summary>同一可空输入按不同目标类型分别复用值槽，条件关闭时跳过转换和数组创建。</summary>
    [Fact]
    public void Generate_WhenNullableScalarHasMultipleTargets_ShouldGuardTypedSlots()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class Row { public long Id { get; set; } public int Version { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, bool enabled, int? input) =>
                    query.WhereIf<Row, int?>(enabled, input, (r, value) =>
                        r.Id == value || r.Version == value || r.Id != ((value)));
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        var interceptor = Assert.Single(result.GeneratedSource.Split('\n'), line =>
            line.Contains("@condition ?", StringComparison.Ordinal));
        Assert.Contains("new object[]{(long?)@value,@value}", interceptor, StringComparison.Ordinal);
        Assert.EndsWith(" : source;", interceptor.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains("global::Bing.Operator.NotEqual, 0)", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>元组字段各自按目标可空类型转换，重复字段和类型不重复占槽。</summary>
    [Fact]
    public void Generate_WhenTupleFieldsNeedNullablePromotion_ShouldReuseTypedSlots()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class Row { public long? Id { get; set; } public decimal? Amount { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int? id, short? amount) =>
                    query.Where<Row, (int? id, short? amount)>((id, amount), (r, value) =>
                        r.Id == value.id && (r.Amount >= value.amount || r.Id != value.id));
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.Contains("new object[]{(long?)@value.Item1,(decimal?)@value.Item2}", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("global::Bing.Operator.NotEqual, 0)", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>可空支持不放开显式窄化、自定义转换和动态取值。</summary>
    [Theory]
    [InlineData("int?", "long?", "r.Id == (int?)value")]
    [InlineData("long?", "int?", "r.Id == (long?)value")]
    [InlineData("long?", "Metric?", "r.Id == value")]
    [InlineData("long?", "int?", "r.Id == value.GetValueOrDefault()")]
    public void Generate_WhenNullableValueNeedsUnsupportedConversion_ShouldKeepStrictDiagnostic(
        string columnType, string valueType, string predicate)
    {
        var source = """
            using Bing.Data.Sql;
            public readonly struct Metric { public static implicit operator long(Metric value) => 1L; }
            public sealed class Row { public COLUMN Id { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, VALUE input) =>
                    query.Where<Row, VALUE>(input, (r, value) => PREDICATE);
            }
            """.Replace("COLUMN", columnType).Replace("VALUE", valueType).Replace("PREDICATE", predicate);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
    }
}
