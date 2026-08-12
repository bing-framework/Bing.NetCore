using System.Collections.Immutable;
using Bing.Data.Sql.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Bing.Data.Sql.Analyzers.Tests;

/// <summary>
/// BINGSQL002 分析器测试。
/// </summary>
public class BingSql002AnalyzerTest
{
    /// <summary>
    /// 测试目的：直接插值 SQL 传入普通字符串入口时应报告参数化风险。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenInterpolatedSqlPassedToSqlEntryPoint_ShouldReportDiagnostic()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Query query, string name) => query.Sql<string>($"Select * From samples Where Name = '{name}'");
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BINGSQL002", diagnostic.Id);
    }

    /// <summary>
    /// 测试目的：Sql 文本入口接收插值 SQL 时必须报告安全诊断。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenInterpolatedSqlPassedToExplicitSqlEntryPoint_ShouldReportDiagnostic()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Query query, string name) => query.Sql<string>($"Select * From samples Where Name = '{name}'");
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BINGSQL002", diagnostic.Id);
    }

    /// <summary>
    /// 测试目的：经局部变量和字符串拼接传播的插值 SQL 传入普通入口时仍应报告。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenInterpolatedSqlFlowsThroughLocalOrConcat_ShouldReportDiagnostic()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Query query, string name)
                {
                    var sql = $"Select * From samples Where Name = '{name}'";
                    query.Sql<string>(sql);
                    query.Sql<string>("Select * From samples Where Name = '" + $"{name}'");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal("BINGSQL002", diagnostic.Id));
    }

    /// <summary>
    /// 测试目的：经转换、条件、空合并或 String.Concat 包装的插值 SQL 仍应报告风险。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenInterpolatedSqlFlowsThroughWrapperExpressions_ShouldReportDiagnostic()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Query query, string name, bool usePrimary)
                {
                    query.Sql<string>((string)$"Select * From samples Where Name = '{name}'");
                    query.Sql<string>(usePrimary ? $"Select * From samples Where Name = '{name}'" : "Select * From samples");
                    query.Sql<string>(null ?? $"Select * From samples Where Name = '{name}'");
                    query.Sql<string>(string.Concat("Select * From samples Where Name = '", $"{name}'"));
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        Assert.Equal(4, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal("BINGSQL002", diagnostic.Id));
    }

    /// <summary>
    /// 测试目的：参数对象和 FormattableString 安全入口不应报告 BINGSQL002。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenUsingSafeSqlEntrypoints_ShouldNotReportDiagnostic()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Query query, string name)
                {
                    query.Sql<string>("Select * From samples Where Name = @name", new { name });
                    query.SqlInterpolated<string>($"Select * From samples Where Name = {name}");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 测试目的：通过类型别名调用普通文本入口时仍应解析到 ISqlQuery 并报告风险。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenUsingAliasedQueryType_ShouldReportDiagnostic()
    {
        // Arrange
        const string source = """
            using QueryAlias = Bing.Data.Sql.Query;
            public class Test
            {
                public void Execute(QueryAlias query, string name) => query.Sql<string>($"Select * From samples Where Name = '{name}'");
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BINGSQL002", diagnostic.Id);
    }

    /// <summary>
    /// 测试目的：ISqlQuery 的普通文本扩展入口接收插值 SQL 时也应报告风险。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenInterpolatedSqlPassedToExtensionEntryPoint_ShouldReportDiagnostic()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Query query, string name) => query.Sql<string>($"Select * From samples Where Name = '{name}'");
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source, SqlExtensionApiStub);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BINGSQL002", diagnostic.Id);
    }

    /// <summary>
    /// 测试目的：执行器同步和异步原生 SQL 入口接收插值值时应报告参数化风险。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenInterpolatedSqlPassedToExecutorEntryPoints_ShouldReportDiagnostic()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Executor executor, string name)
                {
                    executor.ExecuteSql($"Delete From samples Where Name = '{name}'");
                    executor.ExecuteSqlAsync($"Delete From samples Where Name = '{name}'");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal("BINGSQL002", diagnostic.Id));
    }

    /// <summary>
    /// 测试目的：ISqlExecutor 的普通文本扩展入口接收插值 SQL 时也应报告风险。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenInterpolatedSqlPassedToExecutorExtensionEntryPoint_ShouldReportDiagnostic()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Executor executor, string name)
                {
                    executor.ExecuteSql($"Delete From samples Where Name = '{name}'");
                    executor.Sql($"Unrelated helper: {name}");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source, SqlExecutorExtensionApiStub);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BINGSQL002", diagnostic.Id);
    }

    /// <summary>
    /// 测试目的：生成代码中的普通文本入口不应报告 BINGSQL002，避免影响源码生成器输出。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenSourceIsGenerated_ShouldNotReportDiagnostic()
    {
        // Arrange
        const string source = """
            // <auto-generated/>
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Query query, string name) => query.Sql<string>($"Select * From samples Where Name = '{name}'");
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 使用包含最小 SQL API 替身的编译分析源码。
    /// </summary>
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, string apiStub = SqlApiStub)
    {
        var compilation = CSharpCompilation.Create("BingSql002Tests",
            new[] { CSharpSyntaxTree.ParseText(apiStub), CSharpSyntaxTree.ParseText(source) },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location)
            }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new UnsafeInterpolatedSqlAnalyzer());
        return (await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync())
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ToImmutableArray();
    }

    /// <summary>
    /// 用于隔离分析器行为的最小 SQL API 源码替身。
    /// </summary>
    private const string SqlApiStub = """
        namespace Bing.Data.Sql
        {
            public interface ISqlQuery
            {
                object Sql<TResult>(string sql, object parameters = null);
                object SqlInterpolated<TResult>(System.FormattableString sql);
            }

            public sealed class Query : ISqlQuery
            {
                public object Sql<TResult>(string sql, object parameters = null) => null;
                public object SqlInterpolated<TResult>(System.FormattableString sql) => null;
            }

            public interface ISqlExecutor
            {
                object ExecuteSql(string sql, object parameters = null);
                object ExecuteSqlAsync(string sql, object parameters = null);
            }

            public sealed class Executor : ISqlExecutor
            {
                public object ExecuteSql(string sql, object parameters = null) => null;
                public object ExecuteSqlAsync(string sql, object parameters = null) => null;
            }
        }
        """;

    /// <summary>
    /// 用于验证扩展入口识别的最小 SQL API 源码替身。
    /// </summary>
    private const string SqlExtensionApiStub = """
        namespace Bing.Data.Sql
        {
            public interface ISqlQuery { }

            public sealed class Query : ISqlQuery { }

            public static class QueryExtensions
            {
                public static object Sql<TResult>(this ISqlQuery query, string sql, object parameters = null) => null;
            }
        }
        """;

    /// <summary>
    /// 用于验证执行器扩展入口识别的最小 SQL API 源码替身。
    /// </summary>
    private const string SqlExecutorExtensionApiStub = """
        namespace Bing.Data.Sql
        {
            public interface ISqlExecutor { }

            public sealed class Executor : ISqlExecutor { }

            public static class ExecutorExtensions
            {
                public static object ExecuteSql(this ISqlExecutor executor, string sql, object parameters = null) => null;
                public static object Sql(this ISqlExecutor executor, string text) => null;
            }
        }
        """;
}