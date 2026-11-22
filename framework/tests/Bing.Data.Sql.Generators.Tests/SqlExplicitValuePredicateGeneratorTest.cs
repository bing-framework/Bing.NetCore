using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>数值提升在生成代码中执行，且同一输入在不同目标类型下分别绑定。</summary>
    [Theory]
    [InlineData("Where<Row, int>(number, (r, value) => r.Id == value)", ", (long)@value)")]
    [InlineData("Where<Row, int>(number, (r, value) => r.Id == ((value)) || r.Id >= value)", ", (long)@value)")]
    [InlineData("WhereIf<Row, int>(enabled, number, (r, value) => value < r.Id || r.Score == value || r.Rank == value)", "new object[]{(long)@value,(double)@value,@value}")]
    [InlineData("Where<Row, (int number, string name)>((number, name), (r, value) => r.Id >= value.number && r.Name == value.name)", "new object[]{(long)@value.Item1,@value.Item2}")]
    [InlineData("Where<Row, (int number, string name)>((number, name), (r, value) => r.Id == value.number || r.Score == value.number || r.Rank == value.number)", "new object[]{(long)@value.Item1,(double)@value.Item1,@value.Item1}")]
    [InlineData("Where<Row, int>(number, (r, value) => r.Approx == value || r.Amount == value)", "new object[]{(float)@value,(decimal)@value}")]
    public void Generate_WhenExplicitValuesNeedNumericPromotion_ShouldEmitTypedSlots(string call, string expectedValues)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row { public long Id { get; set; } public double Score { get; set; } public float Approx { get; set; } public decimal Amount { get; set; } public int Rank { get; set; } public string Name { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int number, string name, bool enabled) => query.CALL;
            }
            """.Replace("CALL", call);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains(expectedValues, result.GeneratedSource, StringComparison.Ordinal);
        var descriptor = Assert.Single(result.GeneratedSource.Split('\n'),
            line => line.Contains("SqlGeneratedPredicate Predicate =", StringComparison.Ordinal));
        Assert.DoesNotContain("@value", descriptor, StringComparison.Ordinal);
    }

    /// <summary>单个标量只转换到一种类型时无需创建请求值数组。</summary>
    [Theory]
    [InlineData("r.Id == value")]
    [InlineData("r.Id == value || r.Id >= value")]
    [InlineData("value < r.Id || r.Id == ((value))")]
    public void Generate_WhenScalarHasOnePromotionTarget_ShouldAvoidValueArray(string predicate)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row { public long Id { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int id) =>
                    query.Where<Row, int>(id, (r, value) => PREDICATE);
            }
            """.Replace("PREDICATE", predicate);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.Contains(", (long)@value)", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new object[]{", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>可选条件关闭时不能创建转换后的请求值数组。</summary>
    [Fact]
    public void Generate_WhenOptionalNumericPredicateIsDisabled_ShouldSkipValueArray()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class Row { public long Id { get; set; } public int Version { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, bool enabled, int id) =>
                    query.WhereIf<Row, int>(enabled, id, (r, value) => r.Id >= value && r.Version == value);
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        var interceptor = Assert.Single(result.GeneratedSource.Split('\n'), line =>
            line.Contains("@condition ?", StringComparison.Ordinal));
        Assert.Contains("new object[]{(long)@value,@value}", interceptor, StringComparison.Ordinal);
        Assert.EndsWith(" : source;", interceptor.TrimEnd(), StringComparison.Ordinal);
    }

    /// <summary>不需要数值提升的单个显式值沿用无数组绑定。</summary>
    [Fact]
    public void Generate_WhenExplicitScalarNeedsNoPromotion_ShouldAvoidValueArray()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class Row { public long Id { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, long id) =>
                    query.Where<Row, long>(id, (r, value) => r.Id == value || r.Id >= value);
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.Contains(", @value)", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new object[]{", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>元组中各个独立值应在生成路径按字段直接绑定，且条件描述不保存请求值。</summary>
    [Theory]
    [InlineData("Where<Row, (int id, string name)>((id, name), (r, value) => r.Id == value.id && r.Name == value.name, alias)")]
    [InlineData("WhereIf<Row, (int id, string name)>(enabled, (id, name), (r, value) => value.id < r.Id || r.Name.Contains(value.name), alias)")]
    [InlineData("Where<Row, (int, string)>((id, name), (r, value) => r.Id == value.Item1 || r.Id == value.Item1 && r.Name.StartsWith(value.Item2))")]
    [InlineData("Where<Row, (int[] ids, string name)>((ids, name), (r, value) => value.ids.Contains(r.Id) && r.Name.EndsWith(value.name))")]
    public void Generate_WhenExplicitTupleHasMultipleValues_ShouldBindDirectTupleFields(string call)
    {
        var source = """
            using System.Linq;
            using Bing.Data.Sql;
            public sealed class Row { public int Id { get; set; } public string Name { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int id, string name, int[] ids, bool enabled, string alias) => query.CALL;
            }
            """.Replace("CALL", call);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("@value.Item1", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("@value.Item2", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
        var descriptor = Assert.Single(result.GeneratedSource.Split('\n'),
            line => line.Contains("SqlGeneratedPredicate Predicate =", StringComparison.Ordinal));
        Assert.DoesNotContain("@value", descriptor, StringComparison.Ordinal);
    }

    /// <summary>命名和标准字段应指向同一槽位，较长元组也应能直接访问。</summary>
    [Fact]
    public void Generate_WhenTupleContainsEightFields_ShouldBindNamedAndStandardFields()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class Row { public int Id { get; set; } public int ParentId { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, (int a, int b, int c, int d, int e, int f, int g, int h) values) =>
                    query.Where<Row, (int a, int b, int c, int d, int e, int f, int g, int h)>(values,
                        (r, value) => r.Id == value.h || r.ParentId == value.Item8 || r.Id == value.a);
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("new object[]{@value.Item8,@value.Item1}", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>元组字段之外的动态运算和显式转换仍需严格诊断。</summary>
    [Theory]
    [InlineData("r.Id == (long)value.number")]
    [InlineData("r.Name == value.name.Trim()")]
    public void Generate_WhenTupleValueNeedsUnsupportedComputation_ShouldReportStrictDiagnostic(string body)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row { public long Id { get; set; } public string Name { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int number, string name) =>
                    query.Where<Row, (int number, string name)>((number, name), (r, value) => BODY);
            }
            """.Replace("BODY", body);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>显式值在复合条件中应通过参数槽绑定，而不是读取闭包。</summary>
    [Theory]
    [InlineData("Where<Row, int>(id, (r, value) => r.Id == value || r.ParentId == value, alias)")]
    [InlineData("WhereIf<Row, int>(enabled, id, (r, value) => r.Id >= value && r.ParentId != value, alias)")]
    [InlineData("Where<Row, int>(id, (r, value) => value < r.Id || r.ParentId == value, alias)")]
    [InlineData("Where<Row, int>(id, (r, value) => r.Id == value)")]
    [InlineData("WhereIf<Row, int>(enabled, id, (r, value) => r.Id >= value || r.ParentId < 5)")]
    [InlineData("Where<Row, int?>(optionalId, (r, value) => r.OptionalId == value || r.OptionalId != value)")]
    [InlineData("Where<Row, string>(name, (r, value) => r.Name == value || r.Name != value, alias)")]
    public void Generate_WhenCompoundPredicateUsesExplicitValue_ShouldUseStaticDescriptor(string call)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row { public int Id { get; set; } public int ParentId { get; set; } public int? OptionalId { get; set; } public string Name { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int id, int? optionalId, string name, bool enabled, string alias) => query.CALL;
            }
            """.Replace("CALL", call);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedQueryBridge.WherePredicate", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("static readonly global::Bing.Data.Sql.SqlGeneratedPredicate Predicate", result.GeneratedSource, StringComparison.Ordinal);
        var descriptor = Assert.Single(result.GeneratedSource.Split('\n'),
            line => line.Contains("SqlGeneratedPredicate Predicate =", StringComparison.Ordinal));
        Assert.DoesNotContain("@value", descriptor, StringComparison.Ordinal);
    }

    /// <summary>显式转换不属于直接值条件，严格模式应给出诊断。</summary>
    [Fact]
    public void Generate_WhenExplicitValueUsesExplicitConversion_ShouldReportStrictDiagnostic()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class Row { public long Id { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int id) =>
                    query.Where<Row, int>(id, (r, value) => r.Id == (long)value);
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>自定义隐式转换仍应保留严格诊断。</summary>
    [Theory]
    [InlineData("long", "Metric", "r.Id == value")]
    public void Generate_WhenExplicitValueUsesUnsupportedImplicitConversion_ShouldReportStrictDiagnostic(
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

    /// <summary>引用相等比较对 object 的隐式转换不会改变传给映射器的实际对象。</summary>
    [Fact]
    public void Generate_WhenExplicitValueHasReferenceConversion_ShouldUseStaticDescriptor()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class Payload { public string Label { get; set; } }
            public sealed class Row { public Payload Payload { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, Payload payload) =>
                    query.Where<Row, Payload>(payload, (r, value) => r.Payload == value || r.Payload == value);
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>显式值入口不允许通过另一个捕获变量绕过严格模式。</summary>
    [Fact]
    public void Generate_WhenExplicitPredicateAlsoCapturesValue_ShouldReportStrictDiagnostic()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class Row { public int Id { get; set; } public int ParentId { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int id, int captured) =>
                    query.Where<Row, int>(id, (r, value) => r.Id == value || r.ParentId == captured);
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>显式字符串值应在不同匹配方式和逻辑组合中复用同一个条件槽。</summary>
    [Theory]
    [InlineData("Where<Row, string>(term, (r, value) => r.Name.Contains(value), alias)")]
    [InlineData("WhereIf<Row, string>(enabled, term, (r, value) => r.Name.Contains(value) || r.Code.StartsWith(value), alias)")]
    [InlineData("Where<Row, string>(term, (r, value) => r.Code.EndsWith(value) && r.Name == value)")]
    [InlineData("WhereIf<Row, string>(enabled, term, (r, value) => !(r.Name.StartsWith(value) || r.Code.EndsWith(value)))")]
    [InlineData("Where<Row, string>(term, (r, value) => r.Name.Contains((value)) || r.Code.StartsWith(\"static\"), alias)")]
    public void Generate_WhenStringPredicateUsesExplicitValue_ShouldUseStaticDescriptor(string call)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row { public string Name { get; set; } public string Code { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, string term, bool enabled, string alias) => query.CALL;
            }
            """.Replace("CALL", call);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedQueryBridge.WherePredicate", result.GeneratedSource, StringComparison.Ordinal);
        var descriptor = Assert.Single(result.GeneratedSource.Split('\n'),
            line => line.Contains("SqlGeneratedPredicate Predicate =", StringComparison.Ordinal));
        Assert.DoesNotContain("@value", descriptor, StringComparison.Ordinal);
    }

    /// <summary>其他重载和额外捕获值不能被误识别为显式字符串槽。</summary>
    [Theory]
    [InlineData("r.Name.Contains(value, System.StringComparison.Ordinal)")]
    [InlineData("r.Name.Contains(captured)")]
    public void Generate_WhenStringPredicateUsesUnsupportedArgument_ShouldReportStrictDiagnostic(string body)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row { public string Name { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, string term, string captured) =>
                    query.Where<Row, string>(term, (r, value) => BODY);
            }
            """.Replace("BODY", body);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>显式集合的 Contains 应生成可复用条件，集合元素仍在请求中绑定。</summary>
    [Theory]
    [InlineData("Where<Row, int[]>(ids, (r, value) => value.Contains(r.Id), alias)")]
    [InlineData("Where<Row, int[]>(ids, (r, value) => Enumerable.Contains(value, r.Id))")]
    [InlineData("WhereIf<Row, int[]>(enabled, ids, (r, value) => r.Id > 1 && value.Contains(r.ParentId), alias)")]
    [InlineData("WhereIf<Row, int[]>(enabled, ids, (r, value) => !value.Contains(r.Id))")]
    [InlineData("Where<Row, List<int>>(items, (r, value) => value.Contains(r.Id), alias)")]
    [InlineData("Where<Row, IEnumerable<int>>(ids, (r, value) => value.Contains(r.Id), alias)")]
    [InlineData("Where<Row, int[]>(ids, (r, value) => value.Contains(r.Id) || value.Contains(r.ParentId), alias)")]
    public void Generate_WhenPredicateUsesExplicitCollection_ShouldUseParameterSlot(string call)
    {
        var source = """
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Data.Sql;
            public sealed class Row { public int Id { get; set; } public int ParentId { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int[] ids, List<int> items, bool enabled, string alias) => query.CALL;
            }
            """.Replace("CALL", call);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("global::Bing.Operator.In", result.GeneratedSource, StringComparison.Ordinal);
        var descriptor = Assert.Single(result.GeneratedSource.Split('\n'),
            line => line.Contains("SqlGeneratedPredicate Predicate =", StringComparison.Ordinal));
        Assert.DoesNotContain("@value", descriptor, StringComparison.Ordinal);
    }

    /// <summary>自定义比较器、额外捕获集合及列类型转换不应被误认为普通 IN。</summary>
    [Theory]
    [InlineData("Enumerable.Contains(value, r.Id, EqualityComparer<int>.Default)")]
    [InlineData("captured.Contains(r.Id)")]
    [InlineData("value.Contains((int)(long)r.Id)")]
    public void Generate_WhenCollectionPredicateIsUnsupported_ShouldReportStrictDiagnostic(string body)
    {
        var source = """
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Data.Sql;
            public sealed class Row { public int Id { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int[] ids, int[] captured) =>
                    query.Where<Row, int[]>(ids, (r, value) => BODY);
            }
            """.Replace("BODY", body);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
    }
}
