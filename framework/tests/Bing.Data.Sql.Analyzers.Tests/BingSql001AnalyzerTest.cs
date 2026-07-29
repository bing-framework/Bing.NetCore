using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Bing.Data.Sql.Analyzers;
using Xunit;

namespace Bing.Data.Sql.Analyzers.Tests;

/// <summary>
/// BINGSQL001 分析器测试。
/// </summary>
public class BingSql001AnalyzerTest
{
    /// <summary>
    /// 测试 - Count 仅传递一个位置字符串参数时应报告迁移风险。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenCountUsesSinglePositionalString_ShouldReportDiagnostic()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Query query) => query.Count("Total");
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BINGSQL001", diagnostic.Id);
    }

    /// <summary>
    /// 测试 - Count 使用命名列或别名参数时不应报告迁移风险。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenCountUsesNamedArgument_ShouldNotReportDiagnostic()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            public class Test
            {
                public void Execute(Query query)
                {
                    query.Count(column: "Total");
                    query.Count(alias: "Total");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 测试 - 非 Bing.Data.Sql 的同名 Count 调用不应报告迁移风险。
    /// </summary>
    [Fact]
    public async Task Analyze_WhenCountDoesNotTargetSqlExtension_ShouldNotReportDiagnostic()
    {
        // Arrange
        const string source = """
            public class Test
            {
                public void Execute(OtherQuery query) => query.Count("Total");
            }

            public class OtherQuery
            {
                public void Count(string value) { }
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// 使用包含最小 Bing.Data.Sql API 替身的编译分析源码。
    /// </summary>
    /// <param name="source">待分析的调用方源码。</param>
    /// <returns>分析器产生的诊断集合。</returns>
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(SqlApiStub),
            CSharpSyntaxTree.ParseText(source)
        };
        var compilation = CSharpCompilation.Create(
            "BingSql001Tests",
            syntaxTrees,
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new AmbiguousCountAnalyzer());
        var diagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        return diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ToImmutableArray();
    }

    /// <summary>
    /// 用于隔离 Analyzer 行为的最小 SQL API 源码替身。
    /// </summary>
    private const string SqlApiStub = """
        namespace Bing.Data.Sql
        {
            public interface ISelect { }

            public class Query : ISelect { }

            public static class Extensions
            {
                public static T Count<T>(this T source, string column = "*", string alias = null, bool distinct = false)
                    where T : ISelect => source;
            }
        }
        """;
}