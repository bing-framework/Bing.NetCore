using Bing.Data.Sql.Generators;
using Bing.Data.Sql;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>
    /// 测试目的：WhereIf 的条件和列比较由生成桥接静态绑定，避免执行时重新解析表达式树。
    /// </summary>
    [Fact]
    public void Generate_WhenWhereIfIsUsed_ShouldEmitOptionalCompareBridge()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;

            public sealed class Row
            {
                public int Id { get; set; }
                public int TenantId { get; set; }
            }

            public static class Consumer
            {
                public static List<Row> Load(ISqlQueryPlanExecutor executor, bool enabled, int tenantId) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .WhereIf<Row>(enabled, row => row.TenantId == tenantId)
                        .ToList<Row>();

                public static List<Row> LoadValue(ISqlQueryPlanExecutor executor, bool enabled, int tenantId) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .WhereIf<Row>(enabled, row => row.TenantId, tenantId)
                        .ToList<Row>();

            }
            """;

        var result = RunGenerator(source);

        Assert.True(result.EmitSucceeded,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.ToString())) +
            Environment.NewLine + result.GeneratedSource);
        Assert.Contains("SqlGeneratedQueryBridge.OptionalCompare", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("@condition", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("@value", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
    }

    /// <summary>
    /// 条件不成立时，生成调用不能读取可能为 null 的捕获对象字段。
    /// </summary>
    [Fact]
    public void Generate_WhenWhereIfIsFalse_ShouldNotReadCapturedField()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class OptionalRow { public int Id { get; set; } }
            public sealed class OptionalFilter { public int Id; }
            public static class OptionalConsumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, bool enabled, OptionalFilter filter) =>
                    query.WhereIf<OptionalRow>(enabled, row => row.Id == filter.Id);
            }
            """;
        var assembly = LoadGeneratedAssembly(RunGenerator(source));
        var apply = assembly.GetType("OptionalConsumer")!.GetMethod("Apply")!;
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, new Mock<ISqlBuilder>().Object);

        var result = apply.Invoke(null, new object[] { query, false, null });

        Assert.Same(query, result);
    }

    /// <summary>布尔属性条件应在普通和严格模式下使用生成路径，包括取反和动态开关。</summary>
    [Theory]
    [InlineData("Where<BooleanRow>(r => r.Enabled)", false)]
    [InlineData("Where<BooleanRow>(r => !(r.Enabled))", false)]
    [InlineData("WhereIf<BooleanRow>(enabled, r => r.Enabled)", false)]
    [InlineData("WhereIf<BooleanRow>(enabled, r => !r.Enabled)", false)]
    [InlineData("Where<BooleanRow>(r => r.Enabled)", true)]
    [InlineData("Where<BooleanRow>(r => !(r.Enabled))", true)]
    [InlineData("WhereIf<BooleanRow>(enabled, r => r.Enabled)", true)]
    [InlineData("WhereIf<BooleanRow>(enabled, r => !r.Enabled)", true)]
    [InlineData("Where<BooleanRow>(r => r.Enabled, \"b\")", true)]
    [InlineData("WhereIf<BooleanRow>(enabled, r => !r.Enabled, \"b\")", true)]
    [InlineData("Where<BooleanRow, BooleanRow>((left, right) => !right.Enabled, \"l\", \"r\")", true)]
    [InlineData("Where<BooleanRow, bool>(r => r.Enabled, false)", true)]
    [InlineData("WhereIf<BooleanRow>(enabled, r => r.Enabled, false)", true)]
    public void Generate_WhenBooleanPropertyIsPredicate_ShouldCompileWithoutFallback(string call, bool strict)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class BooleanRow { public bool Enabled { get; set; } }
            public static class BooleanConsumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, bool enabled) => query.CALL;
            }
            """.Replace("CALL", call);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = strict ? "true" : "false" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.DoesNotContain("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>关闭条件时生成代码应直接返回原查询，不读取查询 Builder。</summary>
    [Theory]
    [InlineData("r => r.Enabled")]
    [InlineData("r => !r.Enabled")]
    public void Generate_WhenBooleanWhereIfIsDisabled_ShouldReturnOriginalQuery(string predicate)
    {
        var source = "using Bing.Data.Sql; public sealed class BooleanRow { public bool Enabled { get; set; } } " +
            "public static class BooleanConsumer { public static SqlLambdaQuery Apply(SqlLambdaQuery q) => " +
            "q.WhereIf<BooleanRow>(false, PREDICATE); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source.Replace("PREDICATE", predicate), analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        var assembly = LoadGeneratedAssembly(result);
        var apply = assembly.GetType("BooleanConsumer")!.GetMethod("Apply")!;
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, new Mock<ISqlBuilder>().Object);

        Assert.Same(query, apply.Invoke(null, new object[] { query }));
    }

    /// <summary>布尔比较值只在对应调用点装箱一次，后续调用复用。</summary>
    [Theory]
    [InlineData("r => r.Enabled", true)]
    [InlineData("r => !r.Enabled", false)]
    public void Generate_WhenBooleanPredicateIsUsed_ShouldReuseBoxedValue(string predicate, bool expected)
    {
        var source = "using Bing.Data.Sql; public sealed class BooleanRow { public bool Enabled { get; set; } } " +
            "public static class BooleanConsumer { public static SqlLambdaQuery Apply(SqlLambdaQuery q) => " +
            "q.Where<BooleanRow>(PREDICATE); }";
        var assembly = LoadGeneratedAssembly(RunGenerator(source.Replace("PREDICATE", predicate)));
        var descriptor = Assert.Single(assembly.GetType("Bing.Data.Sql.Generated.QueryCalls")!.GetNestedTypes(
            System.Reflection.BindingFlags.NonPublic), type => type.Name.StartsWith("Descriptor_", StringComparison.Ordinal));
        var field = descriptor.GetField("Value", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

        var first = field.GetValue(null);
        var second = field.GetValue(null);

        Assert.True(field.IsInitOnly);
        Assert.Equal(expected, first);
        Assert.Same(first, second);
    }

    /// <summary>复合条件包含捕获值时仍应在严格模式中报告诊断。</summary>
    [Fact]
    public void Generate_WhenCompoundPredicateCapturesValue_ShouldReportStrictDiagnostic()
    {
        const string source = "using Bing.Data.Sql; public class Row { public bool Enabled { get; set; } public bool Visible { get; set; } } " +
            "public static class C { public static SqlLambdaQuery Apply(SqlLambdaQuery q, bool value) => " +
            "q.Where<Row>(r => r.Enabled && r.Visible == value); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
    }

    /// <summary>
    /// 编译期已知的比较值直接传给生成桥接，普通模式也不必读取表达式树。
    /// </summary>
    [Theory]
    [InlineData("Where<ConstantRow>(r => r.Id > 5)")]
    [InlineData("Where<ConstantRow>(r => r.Name == \"A\\\"B\")")]
    [InlineData("Where<ConstantRow>(r => r.Name == null)")]
    [InlineData("Where<ConstantRow>(r => r.Price >= 1.25m)")]
    [InlineData("Where<ConstantRow>(r => r.Status == SampleStatus.Active)")]
    [InlineData("Where<ConstantRow>(r => r.Optional == (long?)5)")]
    [InlineData("Where<ConstantRow>(r => r.Id == long.MinValue)")]
    [InlineData("Where<ConstantRow>(r => r.Score > double.PositiveInfinity)")]
    [InlineData("Where<ConstantRow>(r => r.Score == double.NaN)")]
    [InlineData("Where<ConstantRow>(r => r.Score < double.NegativeInfinity)")]
    [InlineData("Where<ConstantRow>(r => r.Ratio == float.NaN)")]
    [InlineData("Where<ConstantRow>(r => r.Ratio > float.PositiveInfinity)")]
    [InlineData("Where<ConstantRow>(r => r.Ratio < float.NegativeInfinity)")]
    [InlineData("WhereIf<ConstantRow>(true, r => r.Id >= 7)")]
    [InlineData("Having<ConstantRow>(r => SqlFn.Count(r.Id) > 1)")]
    public void Generate_WhenComparisonValueIsConstant_ShouldNotReadExpression(string call)
    {
        var source = """
            using Bing.Data.Sql;
            public enum SampleStatus { Inactive, Active }
            public sealed class ConstantRow
            {
                public long Id { get; set; }
                public string Name { get; set; }
                public decimal Price { get; set; }
                public SampleStatus Status { get; set; }
                public long? Optional { get; set; }
                public double Score { get; set; }
                public float Ratio { get; set; }
            }
            public static class ConstantConsumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query) => query.CALL;
            }
            """.Replace("CALL", call);

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.DoesNotContain("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>严格模式也应为非有限浮点常量输出可编译的表达式。</summary>
    [Theory]
    [InlineData("float", "float.NaN")]
    [InlineData("float", "float.PositiveInfinity")]
    [InlineData("float", "float.NegativeInfinity")]
    [InlineData("double", "double.NaN")]
    [InlineData("double", "double.PositiveInfinity")]
    [InlineData("double", "double.NegativeInfinity")]
    public void Generate_WhenStrictComparisonUsesNonFiniteConstant_ShouldCompile(string type, string value)
    {
        var source = "using Bing.Data.Sql; public class Row { public " + type + " Value { get; set; } } " +
            "public static class C { public static SqlLambdaQuery Apply(SqlLambdaQuery q) => " +
            "q.Where<Row>(r => r.Value == " + value + "); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.DoesNotContain("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>数值常量只装箱一次，可在调用点重复使用。</summary>
    [Fact]
    public void Generate_WhenComparisonUsesScalarConstant_ShouldReuseBoxedValue()
    {
        const string source = "using Bing.Data.Sql; public class Row { public long Id { get; set; } } " +
            "public static class C { public static SqlLambdaQuery Apply(SqlLambdaQuery q) => " +
            "q.Where<Row>(r => r.Id >= 7); }";
        var assembly = LoadGeneratedAssembly(RunGenerator(source));
        var fields = assembly.GetType("Bing.Data.Sql.Generated.QueryCalls")!.GetFields(
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var constant = Assert.Single(fields, field => field.Name.StartsWith("Constant_", StringComparison.Ordinal));

        var first = constant.GetValue(null);
        var second = constant.GetValue(null);

        Assert.True(constant.IsInitOnly);
        Assert.Equal(7L, first);
        Assert.Same(first, second);
    }
}
