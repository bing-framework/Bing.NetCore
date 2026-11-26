using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>完整的可选条件查询链应自动共享结构，而无需调用方声明模板。</summary>
    [Theory]
    [InlineData("ToSql()")]
    [InlineData("ToList<AutoRow>()")]
    [InlineData("ToListAsync<AutoRow>()")]
    public void Generate_AutomaticTemplate_CompleteChain_ShouldEmitBoundedTemplate(string terminal)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class AutoRow { public int Id { get; set; } public string Name { get; set; } }
            public static class Consumer {
                public static object Load(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int id) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<AutoRow>("r")
                        .Where<AutoRow, int>(r => r.Id, id, Bing.Operator.Greater)
                        .WhereIf<AutoRow>(enabled, r => r.Name, "A")
                        .Select<AutoRow>(r => new { r.Id, r.Name })
                        .OrderBy<AutoRow>(r => r.Id).TERMINAL;
            }
            """.Replace("TERMINAL", terminal);
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.Contains("SqlGeneratedQueryTemplate", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains(".Apply(source, Template_", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
    }

    /// <summary>可被外部复用的查询对象继续逐调用修改，不假设查询链独占其状态。</summary>
    [Fact]
    public void Generate_AutomaticTemplate_SharedQuery_ShouldKeepExistingBridge()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class AutoRow { public int Id { get; set; } }
            public static class Consumer {
                public static string Load(SqlLambdaQuery q, int id) =>
                    q.From<AutoRow>("r").Where<AutoRow, int>(r => r.Id, id).ToSql();
            }
            """;
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain("SqlGeneratedQueryTemplate", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedQueryBridge.Compare", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>分页配置等未识别操作保留既有生成链，不把部分链误标为完整模板。</summary>
    [Fact]
    public void Generate_AutomaticTemplate_UnknownOperation_ShouldKeepExistingBridge()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class AutoRow { public int Id { get; set; } }
            public static class Consumer {
                public static string Load(ISqlQuery root, int id) =>
                    root.From<AutoRow>("r").Where<AutoRow, int>(r => r.Id, id).Take(1).ToSql();
            }
            """;
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain("SqlGeneratedQueryTemplate", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedQueryBridge.Compare", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>闭包值无法进入静态模板时，必选 Where 继续使用逐调用桥接。</summary>
    [Fact]
    public void Generate_AutomaticTemplate_CapturedRequiredWhere_ShouldKeepExistingBridge()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class AutoRow { public int Id { get; set; } }
            public static class Consumer {
                public static string Load(ISqlQueryPlanExecutor executor, ISqlBuilder builder, int id) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<AutoRow>("r")
                        .Where<AutoRow>(r => r.Id == id).ToSql();
            }
            """;
        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain("SqlGeneratedQueryTemplate", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>集合值归一化后交给 object 参数桥接，不能再强转回调用点的具体数组类型。</summary>
    [Fact]
    public void Generate_AutomaticTemplate_CollectionValue_ShouldUseObjectSlot()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class AutoRow { public int Id { get; set; } public int[] Values { get; set; } }
            public static class Consumer {
                public static string Load(ISqlQuery root, bool enabled, int[] values) =>
                    root.From<AutoRow>("r")
                        .Where<AutoRow, int[]>(r => r.Values, values, Bing.Operator.In)
                        .WhereIf<AutoRow>(enabled, r => r.Id, 1).ToSql();
            }
            """;
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.Contains("SqlGeneratedQueryTemplate", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("((int[])arguments[", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>闭包取值继续使用原桥接，不能因嵌套桥接调用导致整个生成器退出。</summary>
    [Fact]
    public void Generate_AutomaticTemplate_CapturedValue_ShouldKeepExistingBridgeWithoutGeneratorFailure()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class AutoRow { public int Id { get; set; } }
            public static class Consumer {
                public static object Load(ISqlQuery root, bool enabled, int id) =>
                    root.From<AutoRow>("r").WhereIf<AutoRow>(enabled, r => r.Id == id).ToList<AutoRow>();
            }
            """;
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
        Assert.DoesNotContain("SqlGeneratedQueryTemplate", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedQueryBridge.OptionalCompare", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedQueryBridge.ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
    }
}
