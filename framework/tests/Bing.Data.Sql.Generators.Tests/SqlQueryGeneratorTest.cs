using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Bing.Data.Sql;
using Bing.Data.Sql.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

/// <summary>
/// SqlQuery 调用点生成器编译契约测试。
/// </summary>
public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>
    /// 验证直接结果原型终结点生成投影桥接和绑定读取器调用。
    /// </summary>
    [Fact]
    public void Generate_WhenPrototypeTerminalsAreUsed_ShouldEmitPrototypeExecutionCalls()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Bing.Data.Sql;

            public sealed class Row
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }

            public sealed class Summary
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }

            public static class Consumer
            {
                public static List<Summary> Load(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .ToList(new Summary());

                public static Task<List<Summary>> LoadAsync(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .ToListAsync(new Summary());

                public static Summary LoadSingle(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .To(new Summary());

                public static Task<Summary> LoadSingleAsync(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .ToAsync(new Summary());
            }
            """;

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Contains("SqlGeneratedExecution.ToBoundListPrototype<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToBoundListPrototypeAsync<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToBoundSinglePrototype<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToBoundSinglePrototypeAsync<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("new string[]{\"Id\",\"Name\"}", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证只读 DTO 可以按构造函数参数绑定生成静态读取器。
    /// </summary>
    [Fact]
    public void Generate_WhenResultHasReadOnlyConstructor_ShouldEmitConstructorReader()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;

            public sealed class Row
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }

            public sealed class ReadOnlySummary
            {
                public ReadOnlySummary(int id, string name)
                {
                    Id = id;
                    Name = name;
                }

                public int Id { get; }
                public string Name { get; }
            }

            public static class Consumer
            {
                public static List<ReadOnlySummary> Load(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .ToList<ReadOnlySummary>();

                public static List<ReadOnlySummary> LoadPrototype(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .ToList(new ReadOnlySummary(0, string.Empty));
            }
            """;

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Contains("new global::ReadOnlySummary(", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("ReadOnlySummary", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("new string[]{\"Id\",\"Name\"}", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
    }

    /// <summary>
    /// 验证基数终结操作保留各自的空结果和多行结果语义，并进入对应静态执行入口。
    /// </summary>
    [Fact]
    public void Generate_WhenCardinalityTerminalsAreUsed_ShouldEmitDedicatedExecutionCalls()
    {
        const string source = """
            using System.Threading.Tasks;
            using Bing.Data.Sql;

            public sealed class Row
            {
                public int Id { get; set; }
            }

            public static class Consumer
            {
                public static Row First(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .First<Row>();

                public static Row FirstOrDefault(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .FirstOrDefault<Row>();

                public static Row Single(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .Single<Row>();

                public static Task<Row> FirstAsync(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .FirstAsync<Row>();

                public static Task<Row> FirstOrDefaultAsync(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .FirstOrDefaultAsync<Row>();

                public static Task<Row> SingleAsync(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .SingleAsync<Row>();

            }
            """;

        var result = RunGenerator(source);

        Assert.Contains("SqlGeneratedExecution.First<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.FirstOrDefault<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.Single<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.FirstAsync<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.FirstOrDefaultAsync<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.SingleAsync<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
    }

    /// <summary>
    /// 验证 Lambda 查询的 ToEntity 终结点生成 SingleOrDefault 执行入口。
    /// </summary>
    [Fact]
    public void Generate_WhenToEntityTerminalsAreUsed_ShouldEmitSingleOrDefaultExecutionCalls()
    {
        const string source = """
            using System.Threading.Tasks;
            using Bing.Data.Sql;

            public sealed class Row
            {
                public int Id { get; set; }
            }

            public static class Consumer
            {
                public static Row Load(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .ToEntity<Row>();

                public static Task<Row> LoadAsync(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .ToEntityAsync<Row>();
            }
            """;

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Contains("SqlGeneratedExecution.SingleOrDefault<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.SingleOrDefaultAsync<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证标量和分页终结点生成同一静态计划入口，并保留异步取消参数。
    /// </summary>
    [Fact]
    public void Generate_WhenScalarAndPageTerminalsAreUsed_ShouldEmitStaticPlanCalls()
    {
        const string source = """
            using System.Threading.Tasks;
            using Bing.Data.Sql;

            public sealed class Row
            {
                public int Id { get; set; }
            }

            public static class Consumer
            {
                public static int Scalar(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .Select<Row>(row => row.Id)
                        .Scalar<int>();

                public static Task<int> ScalarAsync(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .Select<Row>(row => row.Id)
                        .ScalarAsync<int>();

                public static Bing.Data.PagerList<Row> Page(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .ToPage<Row>();

                public static Task<Bing.Data.PagerList<Row>> PageAsync(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .ToPageAsync<Row>();
            }
            """;

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Contains("SqlGeneratedExecution.Scalar<int>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ScalarAsync<int>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToPage<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToPageAsync<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：集合条件应使用统一的生成式参数绑定入口。
    /// </summary>
    [Fact]
    public void Generate_WhenCollectionConditionIsUsed_ShouldEmitCollectionBinder()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;

            public sealed class Row
            {
                public int Id { get; set; }
            }

            public static class Consumer
            {
                public static List<Row> Load(ISqlQueryPlanExecutor executor, IEnumerable<int> ids) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .Where<Row, object>(row => row.Id, ids, Bing.Operator.In)
                        .ToList<Row>();
            }
            """;

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Contains("SqlGeneratedExecution.BindParameters(command, parameters)", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToBoundList<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证静态导入 SqlAggregates 后的裸 Count/ Sum 仍生成统一聚合列描述。
    /// </summary>
    [Fact]
    public void Generate_WhenStaticSqlAggregatesAreUsed_ShouldEmitAggregateColumns()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;
            using static Bing.Data.Sql.SqlAggregates;

            public sealed class Row
            {
                public int Id { get; set; }
                public int Quantity { get; set; }
            }

            public sealed class Summary
            {
                public long Count { get; set; }
                public int? Total { get; set; }
            }

            public static class Consumer
            {
                public static List<Summary> Load(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Row>("r")
                        .Select<Row>(row => new
                        {
                            Count = Count(row.Id),
                            Total = Sum(row.Quantity)
                        })
                        .ToList<Summary>();
            }
            """;

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Contains("SqlAggregateFunction.Count", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlAggregateFunction.Sum", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
    }

    /// <summary>
    /// 测试目的：多调用点投影、自连接、显式别名及单段和双段读取应生成可编译拦截器与继承元数据。
    /// </summary>
    [Fact]
    public void Generate_WhenProjectionJoinAndMappedTerminalsAreUsed_ShouldEmitCompilableCallSites()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;

            public class EmployeeBase
            {
                public string BaseName { get; set; } = string.Empty;
            }

            public sealed class Employee : EmployeeBase
            {
                public int Id { get; set; }
                public int ManagerId { get; set; }
                public string Name { get; set; } = string.Empty;
                public string Secret { get; set; } = string.Empty;
            }

            public sealed class EmployeeView
            {
                public int EmployeeId { get; set; }
                public string ManagerName { get; set; } = string.Empty;
            }

            public static class Consumer
            {
                public static List<Employee> LoadSingle(ISqlQueryPlanExecutor executor)
                {
                    var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Employee>("employee");
                    query.SelectAll()
                        .ExcludeSelect<Employee>(row => row.Secret, "employee")
                        .Select<Employee>(row => row.Name, "employee")
                        .Select<Employee>(row => row.BaseName, "employee")
                        .ReplaceSelect<Employee>(row => row.Id, "employee");
                    return query.ToList<Employee>();
                }

                public static List<EmployeeView> LoadSelfJoin(ISqlQueryPlanExecutor executor)
                {
                    var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Employee>("employee")
                        .Join<Employee, Employee>((employee, manager) => employee.ManagerId == manager.Id, "boss");
                    query.Select<Employee, Employee, EmployeeView>(
                        (employee, manager) => new EmployeeView()
                        {
                            EmployeeId = employee.Id,
                            ManagerName = manager.Name
                        }, "employee", "boss");
                    return query.ToList<Employee, Employee, EmployeeView>((employee, manager) => new EmployeeView()
                    {
                        EmployeeId = employee.Id,
                        ManagerName = manager.Name
                    });
                }
            }
            """;

        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);

        Assert.Contains("InterceptsLocationAttribute", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedQueryBridge.Project", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedQueryBridge.Join", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToBoundList<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("BaseName", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("@rightAlias, null)", result.GeneratedSource, StringComparison.Ordinal);
        Assert.True(Regex.Matches(result.GeneratedSource, @"\bCall_\d+\(").Count >= 4);
    }

    /// <summary>
    /// 测试目的：聚合函数参数、匿名结果名和捕获的比较值应生成可编译的受控桥接调用。
    /// </summary>
    [Fact]
    public void Generate_WhenAggregateProjectionAndCapturedComparisonAreUsed_ShouldCompileAndPreserveRoles()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;

            public sealed class Purchase
            {
                public int Id { get; set; }
                public int Quantity { get; set; }
            }

            public static class Consumer
            {
                public static List<Purchase> Load(ISqlQueryPlanExecutor executor, int minimumId)
                {
                    var captured = minimumId;
                    var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<Purchase>("orders");
                    query.Where<Purchase>(order => order.Id > captured);
                    query.Select<Purchase>((o, f) => f.Count(x => x.Id, "abc"));
                    query.ReplaceSelect<Purchase>((order, functions) => new
                    {
                        orderCount = functions.Count(x => x.Id),
                        totalQuantity = functions.Sum(x => x.Quantity)
                    });
                    return query.ToList<Purchase>();
                }
            }
            """;

        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);

        Assert.Contains("SqlGeneratedQueryBridge.Compare", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("ComparisonValue(@predicate)", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlAggregateFunction.Count", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlAggregateFunction.Sum", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("\"abc\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("\"orderCount\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("\"totalQuantity\"", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：同一调用点源文件路径改变后重新运行生成器应更新拦截位置编码。
    /// </summary>
    [Fact]
    public void Generate_WhenSourcePathChanges_ShouldRecompileWithUpdatedInterceptLocation()
    {
        const string source = """
            using Bing.Data.Sql;

            public sealed class PathEntity
            {
                public int Id { get; set; }
            }

            public static class Consumer
            {
                public static SqlLambdaQuery Build(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<PathEntity>("p")
                        .Select<PathEntity>(row => row.Id, "p");
            }
            """;

        var first = RunGenerator(source, "FirstConsumer.cs");
        var second = RunGenerator(source, "SecondConsumer.cs");

        AssertNoCompilationErrors(first);
        AssertNoCompilationErrors(second);
        Assert.NotEqual(first.GeneratedSource, second.GeneratedSource);
    }

    /// <summary>
    /// 测试目的：strict 模式下无法生成的投影表达式应报告 BINGSQLG001 错误诊断。
    /// </summary>
    [Fact]
    public void Generate_WhenStrictModeSeesUnsupportedProjection_ShouldReportBingSqlG001()
    {
        const string source = """
            using Bing.Data.Sql;

            public sealed class StrictEntity
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }

            public static class Consumer
            {
                public static SqlLambdaQuery Build(ISqlQueryPlanExecutor executor) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, null!)
                        .From<StrictEntity>("e")
                        .Select<StrictEntity>(row => row.Name.ToUpper());
            }
            """;

        var strictOptions = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.BingSqlAotStrict"] = "true"
        });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: strictOptions);
        var diagnostics = result.Diagnostics.Where(item => item.Id == "BINGSQLG001").ToArray();
        Assert.NotEmpty(diagnostics);
        var diagnostic = diagnostics[0];
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Select", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证禁用拦截器时保留运行时回退并产生明确诊断。
    /// </summary>
    [Fact]
    public void Generate_WhenInterceptorsAreDisabled_ShouldFallbackWithExplicitDiagnostic()
    {
        const string source = "using Bing.Data.Sql; public class Row { public int Id { get; set; } } public class C { public SqlLambdaQuery Build(SqlLambdaQuery q) => q.Select<Row>(r => r.Id); }";
        var result = RunGenerator(source, enableInterceptors: false);
        AssertNoCompilationErrors(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002" && diagnostic.GetMessage().Contains("命名空间"));
    }

    /// <summary>
    /// 验证严格模式在不支持的读取调用处报告诊断。
    /// </summary>
    [Fact]
    public void Generate_WhenUnsupportedReaderIsStrict_ShouldReportAtCallSite()
    {
        const string source = "using Bing.Data.Sql; public class Row { public int Id { get; } } public class C { public object Load(SqlLambdaQuery q) => q.ToList<Row>(); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001" && diagnostic.Location.IsInSource);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
    }

    /// <summary>
    /// 验证 JIT 模式保留不支持投影的运行时回退并说明原因。
    /// </summary>
    [Fact]
    public void Generate_WhenUnsupportedProjectionInJit_ShouldRetainRuntimeAndExplainFallback()
    {
        const string source = "using Bing.Data.Sql; public class Row { public int Id { get; set; } } public class C { public SqlLambdaQuery Build(SqlLambdaQuery q) => q.Select<Row>(r => r.Id + 1); }";
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002" && diagnostic.Severity == DiagnosticSeverity.Info);
    }

    /// <summary>
    /// 验证拦截器无法命名目标类型时报告诊断。
    /// </summary>
    /// <param name="method">待分析的方法源码。</param>
    /// <param name="entity">待生成的实体类型源码。</param>
    [Theory]
    [InlineData("public SqlLambdaQuery Build<T>(SqlLambdaQuery q) where T : Row => q.Select<T>(r => r.Id);", "public class Row { public int Id { get; set; } }")]
    [InlineData("private class Row { public int Id { get; set; } } public SqlLambdaQuery Build(SqlLambdaQuery q) => q.Select<Row>(r => r.Id);", "")]
    public void Generate_WhenTypeCannotBeNamedByInterceptor_ShouldDiagnoseInsteadOfEmittingInvalidCode(string method, string entity)
    {
        var source = "using Bing.Data.Sql; " + entity + " public class C { " + method + " }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785" || diagnostic.Id == "CS0122");
    }

    /// <summary>
    /// 验证复用生成驱动器时重新编译修改后的投影。
    /// </summary>
    [Fact]
    public void Generate_WhenSameDriverReceivesEditedTree_ShouldRecompileUpdatedProjection()
    {
        const string source = "using Bing.Data.Sql; public class Row { public int Id { get; set; } public int Value { get; set; } } public class C { public SqlLambdaQuery Build(SqlLambdaQuery q) => q.Select<Row>(r => r.Id); }";
        var options = new CSharpParseOptions(LanguageVersion.Preview).WithFeatures(new[] { new KeyValuePair<string, string>("InterceptorsNamespaces", "Bing.Data.Sql.Generated") });
        var tree = CSharpSyntaxTree.ParseText(source, options, "Incremental.cs");
        var compilation = CSharpCompilation.Create("Incremental", new[] { tree }, CreateMetadataReferences(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new SqlQueryGenerator().AsSourceGenerator() }, parseOptions: options);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var first, out _);
        var original = driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString();
        var edited = CSharpSyntaxTree.ParseText(source.Replace("r => r.Id", "r => r.Value"), options, "Incremental.cs");
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation.ReplaceSyntaxTree(tree, edited), out var second, out _);
        Assert.NotEqual(original, driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString());
        using var output = new MemoryStream();
        var result = second.Emit(output);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
    }

    /// <summary>
    /// 验证空 DTO 投影在调用点被拒绝。
    /// </summary>
    [Fact]
    public void Generate_WhenDtoProjectionIsEmpty_ShouldRejectAtTheCallSite()
    {
        const string source = "using Bing.Data.Sql; public class Row { public int Id { get; set; } } public class Dto {} public class C { public SqlLambdaQuery Build(SqlLambdaQuery q) => q.Select<Row, Dto>(r => new Dto { }); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001" && diagnostic.Location.IsInSource);
    }

    /// <summary>显式值重载应直接传入值、别名和运算符，不读取表达式闭包。</summary>
    [Theory]
    [InlineData("q.Where<Row, int>(row => row.Id, minimum)", "global::Bing.Operator.Equal")]
    [InlineData("q.Where<Row, int>(row => row.Id, minimum, Bing.Operator.Greater)", "@operator")]
    [InlineData("q.Where<Row, int>(row => row.Id, minimum, \"items\")", "global::Bing.Operator.Equal")]
    [InlineData("q.Where<Row, int>(row => row.Id, minimum, \"items\", Bing.Operator.Greater)", "@operator")]
    public void Generate_WhenStrictExplicitValueIsUsed_ShouldPassValueWithoutClosureReflection(string expression, string comparison)
    {
        var source = "using Bing.Data.Sql; public class Row { public int Id { get; set; } } public class C { public SqlLambdaQuery Build(SqlLambdaQuery q, int minimum) => " + expression + "; }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        Assert.Contains("SqlGeneratedQueryBridge.Compare", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains(comparison + ", @value)", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>严格模式的常量比较和单/多行终结方法应使用统一静态执行路径。</summary>
    [Fact]
    public void Generate_WhenStrictConstantAndTerminalAreUsed_ShouldAvoidLegacyValueAndReaderBridges()
    {
        const string source = "using System.Collections.Generic; using System.Threading.Tasks; using Bing.Data.Sql; public class Row { public int Id { get; set; } } public class C { public object Load(SqlLambdaQuery q) => q.Where<Row>(row => row.Id > 4).ToList<Row>(); public object LoadSingle(SqlLambdaQuery q) => q.Where<Row>(row => row.Id > 4).To<Row>(); public Task<List<Row>> LoadAsync(SqlLambdaQuery q) => q.Where<Row>(row => row.Id > 4).ToListAsync<Row>(); public Task<Row> LoadSingleAsync(SqlLambdaQuery q) => q.Where<Row>(row => row.Id > 4).ToAsync<Row>(); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        Assert.Contains(" = (int)4;", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("global::Bing.Operator.Greater, Constant_", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToBoundList<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToBoundSingle<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToBoundListAsync<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlGeneratedExecution.ToBoundSingleAsync<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ComparisonValue", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("source.ToList<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("source.To<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("source.ToListAsync<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("source.ToAsync<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>有明确 JSON 列声明的实体使用带字段转换的静态结果读取。</summary>
    [Theory]
    [InlineData("json", "ToListAsync")]
    [InlineData("JSONB", "To")]
    public void Generate_WhenResultHasJsonColumn_ShouldUseModelReader(string columnType, string terminal)
    {
        var source = "using Bing.Data.Sql; using System.ComponentModel.DataAnnotations.Schema; " +
            "public class Payload {} public class Row { [Column(TypeName = \"" + columnType + "\")] public Payload Data { get; set; } } " +
            "public class C { public object Load(SqlLambdaQuery q) => q." + terminal + "<Row>(); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        Assert.Contains("source." + terminal + "<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>内置范围和多范围使用生成的强类型读取器，保留可空列语义。</summary>
    [Theory]
    [InlineData("int", "int", "int4")]
    [InlineData("long", "long", "int8")]
    [InlineData("decimal", "decimal", "num")]
    [InlineData("System.DateTime", "global::System.DateTime", "tstz")]
    public void Generate_WhenResultHasPostgreSqlRanges_ShouldReadNativeTypes(string clrType, string emittedType, string columnType)
    {
        var source = $$"""
            using Bing.Data.Sql;
            using System.ComponentModel.DataAnnotations.Schema;
            using NpgsqlTypes;
            namespace NpgsqlTypes { public struct NpgsqlRange<T> {} }
            public class Row
            {
                [Column(TypeName = "{{columnType}}range")] public NpgsqlRange<{{clrType}}>? Bounds { get; set; }
                [Column(TypeName = "{{columnType}}multirange")] public NpgsqlRange<{{clrType}}>[] Periods { get; set; }
            }
            public class C { public object Load(SqlLambdaQuery q) => q.ToListAsync<Row>(); }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        Assert.Contains($"reader.GetFieldValue<global::NpgsqlTypes.NpgsqlRange<{emittedType}>>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains($"reader.GetFieldValue<global::NpgsqlTypes.NpgsqlRange<{emittedType}>[]>", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>JSON 列的模型转换优先于范围的直接读取。</summary>
    [Fact]
    public void Generate_WhenRangeArrayUsesJsonColumn_ShouldUseModelReader()
    {
        const string source = """
            using Bing.Data.Sql;
            using System.ComponentModel.DataAnnotations.Schema;
            using NpgsqlTypes;
            namespace NpgsqlTypes { public struct NpgsqlRange<T> {} }
            public class Row
            {
                [Column(TypeName = "jsonb")] public NpgsqlRange<int>[] Periods { get; set; }
            }
            public class C { public object Load(SqlLambdaQuery q) => q.ToListAsync<Row>(); }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        Assert.Contains("source.ToListAsync<global::Row>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("reader.GetFieldValue<global::NpgsqlTypes.NpgsqlRange<int>[]>", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>无法转换的复合列仍在严格模式下产生诊断。</summary>
    [Fact]
    public void Generate_WhenResultHasUnsupportedComplexColumn_ShouldRejectAtTheCallSite()
    {
        const string source = "using Bing.Data.Sql; using System.ComponentModel.DataAnnotations.Schema; " +
            "public class Payload {} public class Row { [Column(TypeName = \"text\")] public Payload Data { get; set; } } " +
            "public class C { public object Load(SqlLambdaQuery q) => q.ToListAsync<Row>(); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001" && diagnostic.Location.IsInSource);
    }

    /// <summary>严格模式应在调用点拒绝仍需反射取值的闭包条件，并给出迁移方法。</summary>
    [Fact]
    public void Generate_WhenStrictCapturedComparisonIsUsed_ShouldRecommendExplicitValueOverload()
    {
        const string source = "using Bing.Data.Sql; public class Row { public int Id { get; set; } } public class C { public object Load(SqlLambdaQuery q, int minimum) => q.Where<Row>(row => row.Id > minimum); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001" && diagnostic.Location.IsInSource && diagnostic.GetMessage().Contains("Where<TEntity, TValue>"));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
    }

    /// <summary>
    /// 运行 SQL 查询生成器并收集编译结果。
    /// </summary>
    /// <param name="source">待生成或分析的 C# 源码。</param>
    /// <param name="path">生成器输入源码的文件路径。</param>
    /// <param name="analyzerConfigOptionsProvider">生成器使用的分析器配置选项提供程序。</param>
    /// <param name="enableInterceptors">是否启用生成代码拦截器。</param>
    /// <returns>生成源码、编译诊断及程序集输出的集合。</returns>
    private static GeneratorResult RunGenerator(string source, string path = "GeneratedConsumer.cs",
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider = null, bool enableInterceptors = true)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview)
            .WithFeatures(enableInterceptors ? new[]
            {
                new KeyValuePair<string, string>("InterceptorsNamespaces", "Bing.Data.Sql.Generated")
            } : Array.Empty<KeyValuePair<string, string>>());
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, path);
        var compilation = CSharpCompilation.Create(
            "SqlQueryGeneratorConsumer",
            new[] { syntaxTree },
            CreateMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Disable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new SqlQueryGenerator().AsSourceGenerator() },
            parseOptions: parseOptions,
            optionsProvider: analyzerConfigOptionsProvider ?? EmptyAnalyzerConfigOptionsProvider.Instance);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation,
            out var driverDiagnostics);

        var runResult = driver.GetRunResult();
        var generatedSource = string.Join(Environment.NewLine,
            runResult.Results.SelectMany(item => item.GeneratedSources)
                .Select(item => item.SourceText.ToString()));
        using var output = new MemoryStream();
        var emitResult = updatedCompilation.Emit(output);
        var diagnostics = driverDiagnostics
            .Concat(runResult.Diagnostics)
            .Concat(updatedCompilation.GetDiagnostics())
            .Concat(emitResult.Diagnostics)
            .ToImmutableArray();
        return new GeneratorResult(generatedSource, diagnostics, emitResult.Success, output.ToArray());
    }

    /// <summary>
    /// 创建生成器测试所需的元数据引用。
    /// </summary>
    /// <returns>生成器测试编译使用的元数据引用集合。</returns>
    private static ImmutableArray<MetadataReference> CreateMetadataReferences()
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        var paths = trustedAssemblies.Split(Path.PathSeparator).ToList();
        paths.Add(typeof(SqlLambdaQuery).Assembly.Location);
        paths.Add(typeof(SqlGeneratedExecution).Assembly.Location);
        paths.Add(typeof(SqlQueryBase).Assembly.Location);
        return paths.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();
    }

    /// <summary>
    /// 验证生成代码成功编译且没有错误诊断。
    /// </summary>
    /// <param name="result">生成器运行及编译结果。</param>
    private static void AssertNoCompilationErrors(GeneratorResult result)
    {
        var errors = result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(result.EmitSucceeded,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.ToString())));
        Assert.Empty(errors);
    }

    /// <summary>
    /// 封装 SQL 查询生成器测试的源码、诊断和编译结果。
    /// </summary>
    /// <param name="GeneratedSource">生成的源码文本。</param>
    /// <param name="Diagnostics">源码生成诊断集合。</param>
    /// <param name="EmitSucceeded">是否成功发出程序集。</param>
    /// <param name="AssemblyImage">编译生成的程序集映像。</param>
    private sealed record GeneratorResult(string GeneratedSource, ImmutableArray<Diagnostic> Diagnostics,
        bool EmitSucceeded, byte[] AssemblyImage);

    /// <summary>
    /// 为源码生成器测试提供空的分析器配置选项。
    /// </summary>
    private sealed class EmptyAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        /// <summary>
        /// 获取共享的配置提供器实例。
        /// </summary>
        public static EmptyAnalyzerConfigOptionsProvider Instance { get; } = new();

        /// <summary>
        /// 获取不含配置项的分析器选项。
        /// </summary>
        public static AnalyzerConfigOptions EmptyOptions { get; } = new TestAnalyzerConfigOptions(
            new Dictionary<string, string>());

        /// <inheritdoc />
        public override AnalyzerConfigOptions GlobalOptions => EmptyOptions;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyOptions;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyOptions;
    }

    /// <summary>
    /// 组织TestAnalyzerConfigOptionsProvider相关测试场景。
    /// </summary>
    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        /// <summary>
        /// 保存分析器测试的全局配置选项。
        /// </summary>
        private readonly AnalyzerConfigOptions _globalOptions;

        /// <summary>
        /// 初始化一个 <see cref="TestAnalyzerConfigOptionsProvider" /> 类型的实例。
        /// </summary>
        /// <param name="globalValues">SQL 查询生成测试使用的全局值集合。</param>
        public TestAnalyzerConfigOptionsProvider(IDictionary<string, string> globalValues)
        {
            _globalOptions = new TestAnalyzerConfigOptions(globalValues);
        }

        /// <inheritdoc />
        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
            EmptyAnalyzerConfigOptionsProvider.EmptyOptions;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            EmptyAnalyzerConfigOptionsProvider.EmptyOptions;
    }

    /// <summary>
    /// 组织TestAnalyzerConfigOptions相关测试场景。
    /// </summary>
    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        /// <summary>
        /// 保存分析器测试的配置选项值。
        /// </summary>
        private readonly IDictionary<string, string> _values;

        /// <summary>
        /// 初始化一个 <see cref="TestAnalyzerConfigOptions" /> 类型的实例。
        /// </summary>
        /// <param name="values">用于初始化实例的数据项集合。</param>
        public TestAnalyzerConfigOptions(IDictionary<string, string> values)
        {
            _values = values;
        }

        /// <inheritdoc />
        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value);
    }
}

