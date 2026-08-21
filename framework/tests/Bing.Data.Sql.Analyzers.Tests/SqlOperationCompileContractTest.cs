using Bing.Data.Sql;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Bing.Data.Sql.Analyzers.Tests;

/// <summary>
/// SQL Operation Fluent API 编译契约测试。
/// </summary>
public class SqlOperationCompileContractTest
{
    /// <summary>
    /// 测试 - 普通查询能力源不应暴露 Insert Fluent API。
    /// </summary>
    [Fact]
    public void InsertExtension_WhenSourceOnlySupportsQuery_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Operations;
            using Bing.Data.Sql.Metadata;

            sealed class QueryOnly : ISqlQueryOperation { }

            static class Consumer
            {
                static void Use(QueryOnly source) => source.InsertInto(default(SqlTableReference));
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        AssertCompileFailedForMissingExtension(diagnostics, "InsertInto");
    }

    /// <summary>
    /// 测试 - Insert Builder 不应暴露 Update Fluent API。
    /// </summary>
    [Fact]
    public void UpdateExtension_WhenSourceIsInsertBuilder_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            using Bing.Data.Sql.Metadata;

            static class Consumer
            {
                static void Use(ISqlInsertBuilder source) => source.Update(default(SqlTableReference));
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        AssertCompileFailedForMissingExtension(diagnostics, "Update");
    }

    /// <summary>
    /// 测试 - Update Builder 不应暴露 Delete Fluent API。
    /// </summary>
    [Fact]
    public void DeleteExtension_WhenSourceIsUpdateBuilder_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            using Bing.Data.Sql.Metadata;

            static class Consumer
            {
                static void Use(ISqlUpdateBuilder source) => source.DeleteFrom(default(SqlTableReference));
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        AssertCompileFailedForMissingExtension(diagnostics, "DeleteFrom");
    }

    /// <summary>
    /// 测试 - 仅实现 Marker 而缺少 Accessor 的类型不应满足 Insert 扩展约束。
    /// </summary>
    [Fact]
    public void InsertExtension_WhenMarkerHasNoAccessor_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Operations;
            using Bing.Data.Sql.Metadata;

            sealed class InsertMarkerOnly : IInsert { }

            static class Consumer
            {
                static void Use(InsertMarkerOnly source) => source.InsertInto(default(SqlTableReference));
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        AssertCompileFailedForMissingExtension(diagnostics, "InsertInto");
    }

    /// <summary>
    /// 测试 - 正确的专用 Insert Builder 应满足 Insert 扩展约束。
    /// </summary>
    [Fact]
    public void InsertExtension_WhenSourceProvidesMarkerAndAccessor_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            using Bing.Data.Sql.Metadata;

            static class Consumer
            {
                static void Use(ISqlInsertBuilder source) => source.InsertInto(default(SqlTableReference));
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：专用 Update Builder 应公开结构化 UpdateFrom、SetFrom 和 WhereFrom Fluent API。
    /// </summary>
    [Fact]
    public void UpdateFromExtensions_WhenSourceIsUpdateBuilder_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            using Bing.Data.Sql.Metadata;

            static class Consumer
            {
                static void Use(ISqlUpdateBuilder source) => source
                    .UpdateFrom(new SqlTableReference { TableName = "updates", Alias = "s" })
                    .SetFrom("Name", "Name")
                    .WhereFrom("Id", "Id");
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：专用 Delete Builder 应公开结构化 DeleteUsing 和 WhereUsing Fluent API。
    /// </summary>
    [Fact]
    public void DeleteUsingExtensions_WhenSourceIsDeleteBuilder_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            using Bing.Data.Sql.Metadata;

            static class Consumer
            {
                static void Use(ISqlDeleteBuilder source) => source
                    .DeleteUsing(new SqlTableReference { TableName = "deletes", Alias = "s" })
                    .WhereUsing("Id", "Id");
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：统一 Builder 应公开结构化和实体映射 Returning Fluent API。
    /// </summary>
    [Fact]
    public void ReturningExtensions_WhenSourceIsUnifiedBuilder_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item
            {
                public int Id { get; set; }
            }

            static class Consumer
            {
                static void Use(ISqlBuilder source)
                {
                    source.Returning("Id");
                    source.Returning<Item>(item => item.Id);
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：第三方 Provider 应能实现返回结果子句位置、关键字和限定符方言合同。
    /// </summary>
    [Fact]
    public void ReturningDialect_WhenImplementedByProvider_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            using Bing.Data.Sql.Builders.Mutations;

            sealed class OutputDialect : ISqlReturningDialect
            {
                public SqlReturningClausePosition Position => SqlReturningClausePosition.BeforeSource;
                public string GetKeyword(SqlExecutionKind executionKind) => "Output";
                public string GetQualifier(SqlExecutionKind executionKind, string configuredQualifier) =>
                    executionKind == SqlExecutionKind.Delete ? "Deleted" : "Inserted";
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 编译动态 C# 源码并收集诊断信息。
    /// </summary>
    /// <param name="source">待验证公开 SQL 操作契约的 C# 源码。</param>
    /// <returns>编译过程产生的诊断集合。</returns>
    private static IReadOnlyList<Diagnostic> Compile(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(ISqlBuilder).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "SqlOperationCompileContract",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetDiagnostics();
    }

    /// <summary>
    /// 断言因缺少指定扩展方法而产生编译错误。
    /// </summary>
    /// <param name="diagnostics">动态编译产生的诊断集合。</param>
    /// <param name="method">预期出现在错误消息中的缺失方法名称。</param>
    private static void AssertCompileFailedForMissingExtension(IReadOnlyList<Diagnostic> diagnostics, string method)
    {
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.GetMessage().Contains(method));
    }
}