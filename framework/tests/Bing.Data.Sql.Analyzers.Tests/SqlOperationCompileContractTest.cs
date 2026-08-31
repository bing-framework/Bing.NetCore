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
    /// 测试目的：最终查询入口应返回非泛型描述，并由终结方法选择结果类型。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingNonGenericDescriptionsAndTerminalResults_ShouldCompile()
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
                static void Use(ISqlQuery query)
                {
                    SqlLambdaQuery lambda = query.From<Item>("i");
                    var lambdaRows = lambda.ToList<Item>();
                    var fluentRows = query.Query().ToList<Item>();
                    var textRows = query.Sql("Select Id From Items").ToList<Item>();
                    var procedureRows = query.Procedure("GetItems").ExecuteList<Item>();
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：最终 Lambda 描述应支持连续 10 个根来源和 2～10 来源的二元 Join 链。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingTenSourcesAndBinaryJoins_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item
            {
                public int Id { get; set; }
                public int NextId { get; set; }
            }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    var description = query.From<Item>("i1")
                        .From<Item>("i2")
                        .From<Item>("i3")
                        .From<Item>("i4")
                        .From<Item>("i5")
                        .From<Item>("i6")
                        .From<Item>("i7")
                        .From<Item>("i8")
                        .From<Item>("i9")
                        .From<Item>("i10")
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j2", LeftAlias = "i1" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j3", LeftAlias = "j2" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j4", LeftAlias = "j3" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j5", LeftAlias = "j4" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j6", LeftAlias = "j5" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j7", LeftAlias = "j6" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j8", LeftAlias = "j7" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j9", LeftAlias = "j8" })
                        .Join<Item, Item>((first, second) => first.NextId == second.Id,
                            new SqlJoinOptions { RightAlias = "j10", LeftAlias = "j9" })
                        .Select<Item, Item>((first, second) => new object[] { first.Id, second.Id }, "i1", "j2")
                        .ToList<Item>();
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：已删除的起始阶段泛型 Query 入口不能被消费者编译使用。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingGenericRootQuery_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { }

            static class Consumer
            {
                static void Use(ISqlQuery query) => query.Query<Item>();
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        AssertCompileFailedForMissingExtension(diagnostics, "Query");
    }

    /// <summary>
    /// 测试目的：已删除的泛型 Raw、插值 SQL 和 Procedure 起始入口不能被消费者编译使用。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingGenericRawAndProcedureDescriptions_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    query.Sql<Item>("Select 1");
                    query.SqlInterpolated<Item>($"Select 1");
                    query.Procedure<Item>("GetItems");
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("Sql", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("Procedure", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试目的：已删除的泛型 Lambda 描述类型不能重新成为公共消费者入口。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingGenericLambdaDescription_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { }

            static class Consumer
            {
                static SqlLambdaQuery<Item> Use(ISqlQuery query) => query.From<Item>();
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("SqlLambdaQuery", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试目的：非泛型 Fluent 和 Raw SQL 描述应支持 Dapper 2、3、7 段多映射，并由终结方法选择结果类型。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingTwoThreeAndSevenWayMappings_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class Consumer
            {
                static Item Map2(Item first, Item second) => new Item { Id = first.Id + second.Id };
                static Item Map3(Item first, Item second, Item third) => new Item { Id = first.Id + second.Id + third.Id };
                static Item Map7(Item first, Item second, Item third, Item fourth, Item fifth, Item sixth, Item seventh) =>
                    new Item { Id = first.Id + second.Id + third.Id + fourth.Id + fifth.Id + sixth.Id + seventh.Id };

                static void Use(ISqlQuery query)
                {
                    query.Query().ToList<Item, Item, Item>(Map2);
                    query.Query().ToList<Item, Item, Item, Item>(Map3);
                    query.Query().ToList<Item, Item, Item, Item, Item, Item, Item, Item>(Map7);
                    query.Query().ToListAsync<Item, Item, Item>(Map2);
                    query.Query().ToListAsync<Item, Item, Item, Item>(Map3);
                    query.Query().ToListAsync<Item, Item, Item, Item, Item, Item, Item, Item>(Map7);

                    query.Sql("Select 1").ToList<Item, Item, Item>(Map2);
                    query.Sql("Select 1").ToList<Item, Item, Item, Item>(Map3);
                    query.Sql("Select 1").ToList<Item, Item, Item, Item, Item, Item, Item, Item>(Map7);
                    query.Sql("Select 1").ToListAsync<Item, Item, Item>(Map2);
                    query.Sql("Select 1").ToListAsync<Item, Item, Item, Item>(Map3);
                    query.Sql("Select 1").ToListAsync<Item, Item, Item, Item, Item, Item, Item, Item>(Map7);
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：第三方代码仅依赖 ISqlQuery 时，应能使用非泛型 Root 描述和终结泛型，不需要强转 SqlQueryBase。
    /// </summary>
    [Fact]
    public void QueryApi_WhenThirdPartyConsumerUsesISqlQuery_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class ThirdPartyConsumer
            {
                static void Use(ISqlQuery query)
                {
                    var rows = query.Query().ToList<Item>();
                    var value = query.Sql("Select Id From Items").Scalar<int>();
                    var procedure = query.Procedure("GetItems").ExecuteList<Item>();
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：Lambda 组合公共 API 只允许一元或二元表达式，不重新暴露三元来源入口。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingThreeParameterLambda_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    query.From<Item>("a")
                        .From<Item>("b")
                        .From<Item>("c")
                        .Where<Item, Item, Item>((a, b, c) => a.Id == b.Id && b.Id == c.Id);
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("Where", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试目的：实体 Join 使用普通右别名时，显式传入 null 应无歧义地编译为普通入口。
    /// </summary>
    [Fact]
    public void QueryApi_WhenJoinUsesNullRightAlias_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    query.From<Item>("left")
                        .Join<Item, Item>((left, right) => left.Id == right.Id, null)
                        .LeftJoin<Item, Item>((left, right) => left.Id == right.Id, null)
                        .RightJoin<Item, Item>((left, right) => left.Id == right.Id, null)
                        .FullJoin<Item, Item>((left, right) => left.Id == right.Id, null);
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：四类实体 Join 均应支持普通右别名和 SqlJoinOptions，并允许通过 options 指定 schema。
    /// </summary>
    [Fact]
    public void QueryApi_WhenJoinUsesAliasesAndSchemaOptions_ShouldCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    query.From<Item>("left")
                        .Join<Item, Item>((left, right) => left.Id == right.Id, "inner")
                        .LeftJoin<Item, Item>((left, right) => left.Id == right.Id, "left_join")
                        .RightJoin<Item, Item>((left, right) => left.Id == right.Id, "right_join")
                        .FullJoin<Item, Item>((left, right) => left.Id == right.Id, "full_join")
                        .Join<Item, Item>((left, right) => left.Id == right.Id,
                            new SqlJoinOptions { RightAlias = "inner_options", LeftAlias = "left", Schema = "reporting" })
                        .LeftJoin<Item, Item>((left, right) => left.Id == right.Id,
                            new SqlJoinOptions { RightAlias = "left_options", LeftAlias = "left", Schema = "reporting" })
                        .RightJoin<Item, Item>((left, right) => left.Id == right.Id,
                            new SqlJoinOptions { RightAlias = "right_options", LeftAlias = "left", Schema = "reporting" })
                        .FullJoin<Item, Item>((left, right) => left.Id == right.Id,
                            new SqlJoinOptions { RightAlias = "full_options", LeftAlias = "left", Schema = "reporting" });
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 测试目的：删除的高层 FromTable、ClearSelect 和旧三字符串 Join 入口不得被第三方代码编译使用。
    /// </summary>
    [Fact]
    public void QueryApi_WhenUsingRemovedLegacyMembers_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql;

            sealed class Item { public int Id { get; set; } }

            static class Consumer
            {
                static void Use(ISqlQuery query)
                {
                    query.FromTable("Items", "i");
                    query.From<Item>("i").ClearSelect();
                    query.From<Item>("i").Join<Item, Item>((left, right) => left.Id == right.Id, "r", "l");
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("FromTable", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("ClearSelect", StringComparison.Ordinal));
        Assert.True(diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) >= 3);
    }

    /// <summary>
    /// 测试目的：第三方消费者不得依赖内部 Helper 或 JoinItem 的内部注入和克隆协作。
    /// </summary>
    [Fact]
    public void BuilderInternals_WhenUsedByThirdPartyConsumer_ShouldNotCompile()
    {
        // Arrange
        const string source = """
            using Bing.Data.Sql.Builders.Core;
            using Bing.Data.Sql.Builders.Internal;

            static class Consumer
            {
                static void Use()
                {
                    var helper = new Helper(null);
                    JoinItem.CreateTable("Join", "Orders").SetDependency(helper);
                }
            }
            """;

        // Act
        var diagnostics = Compile(source);

        // Assert
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("Helper", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("SetDependency", StringComparison.Ordinal));
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
        references.Add(MetadataReference.CreateFromFile(typeof(SqlQueryBase).Assembly.Location));
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