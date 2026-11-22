using System;
using System.Linq;
using System.Reflection;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    /// <summary>可选 SQL 合同升级/降级必须使增量输入失效，无关编辑仍命中已生成结果。</summary>
    [Fact]
    public void Generate_WhenMutationSqlContractChanges_ShouldInvalidateOnlyRelevantInput()
    {
        var legacy = RuntimeContractSource(includeGeneratedContracts: true) +
            "namespace Bing.Data.Sql.Builders { public enum SqlOperationKind { InsertValues, Update, Delete } }";
        var current = legacy.Replace("public Func<object> GeneratedFactory { get; }",
            "public Func<object> GeneratedFactory { get; } " +
            "public Func<string, Bing.Data.Sql.Builders.SqlOperationKind, string> GeneratedMutationSql { get; set; }",
            StringComparison.Ordinal);
        var first = RunIncremental(false, new SourceInput("Runtime.cs", legacy),
            new SourceInput("Model.cs", "using Bing.Data.Sql.Generated; " +
                "public sealed class Row { public int Id {get;set;} } " +
                "[SqlModel(typeof(Row))] public partial class Context {}"),
            new SourceInput("Other.cs", "public class Other {}"));
        string Generated(GeneratorDriver driver) =>
            Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources).SourceText.ToString();
        Assert.DoesNotContain("GeneratedMutationSql", Generated(first.Driver), StringComparison.Ordinal);

        var upgraded = UpdateIncremental(first, "Runtime.cs", current);
        Assert.Single(GetGenerateReasons(upgraded.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.Contains("GeneratedMutationSql =", Generated(upgraded.Driver), StringComparison.Ordinal);

        var unrelated = UpdateIncremental(upgraded, "Other.cs", "public class Other { public int Value {get;set;} }");
        Assert.Single(GetGenerateReasons(unrelated.Driver), reason => reason == IncrementalStepRunReason.Cached);
        Assert.Equal(Generated(upgraded.Driver), Generated(unrelated.Driver));

        var downgraded = UpdateIncremental(unrelated, "Runtime.cs", legacy);
        Assert.Single(GetGenerateReasons(downgraded.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.DoesNotContain("GeneratedMutationSql", Generated(downgraded.Driver), StringComparison.Ordinal);
    }

    /// <summary>固定实体生成完整 SQLite CRUD，忽略 Identity、Computed、NotMapped 并保留复合键与并发顺序。</summary>
    [Theory]
    [InlineData(SqlOperationKind.InsertValues, "Insert Into `order_rows` (`TenantId`, `display_name`, `Version`) Values (@_p_0, @_p_1, @_p_2)")]
    [InlineData(SqlOperationKind.Update, "Update `order_rows` Set `display_name` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `TenantId` = @_p_3 And `Version` = @_p_4")]
    [InlineData(SqlOperationKind.Delete, "Delete From `order_rows` Where `Id` = @_p_0 And `TenantId` = @_p_1 And `Version` = @_p_2")]
    public void Generate_ModelMutationSql_ShouldReturnCompleteSqliteStatement(SqlOperationKind operation, string expected)
    {
        var result = RunGenerator("""
            using Bing.Data.Sql.Generated;
            using System.ComponentModel.DataAnnotations;
            using System.ComponentModel.DataAnnotations.Schema;
            [Table("order_rows")] public sealed class OrderRow {
                [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id {get;set;}
                [Key] public int TenantId {get;set;}
                [Column("display_name")] public string Name {get;set;}
                [ConcurrencyCheck] public int Version {get;set;}
                [DatabaseGenerated(DatabaseGeneratedOption.Computed)] public int Total {get;set;}
                [NotMapped] public string Local {get;set;}
            }
            [SqlModel(typeof(OrderRow))] public static partial class MutationContext {}
            """);
        AssertNoCompilationErrors(result);
        Assert.Contains("GeneratedMutationSql", result.GeneratedSource, StringComparison.Ordinal);
        var model = Assert.Single((EntityModelMetadata[])Assembly.Load(result.AssemblyImage)
            .GetType("MutationContext")!.GetMethod("CreateModels")!.Invoke(null, null)!);
        var sql = Assert.IsType<Func<string, SqlOperationKind, string>>(typeof(EntityModelMetadata)
            .GetProperty("GeneratedMutationSql")!.GetValue(model));
        Assert.Equal(expected, sql("bing.sqlite", operation));
        Assert.Null(sql("custom", operation));
        Assert.Null(sql("bing.sqlite", SqlOperationKind.Select));
    }

    /// <summary>没有主键的模型只能生成 Insert，Raw/Result 角色不能隐式获得写入 SQL。</summary>
    [Theory]
    [InlineData("SqlModel", true)]
    [InlineData("SqlParameters", false)]
    [InlineData("SqlResult", false)]
    public void Generate_ModelMutationSql_ShouldRespectRegistrationRoleAndMissingKeys(string role, bool hasInsert)
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; public sealed class NoKey { public string Name {get;set;} } " +
            "[" + role + "(typeof(NoKey))] public static partial class NoKeyContext {}");
        AssertNoCompilationErrors(result);
        var model = Assert.Single((EntityModelMetadata[])Assembly.Load(result.AssemblyImage)
            .GetType("NoKeyContext")!.GetMethod("CreateModels")!.Invoke(null, null)!);
        var factory = typeof(EntityModelMetadata).GetProperty("GeneratedMutationSql")?.GetValue(model)
            as Func<string, SqlOperationKind, string>;
        if (!hasInsert) { Assert.Null(factory); return; }
        Assert.NotNull(factory);
        Assert.Equal("Insert Into `NoKey` (`Name`) Values (@_p_0)", factory!("bing.sqlite", SqlOperationKind.InsertValues));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.Update));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.Delete));
    }

    /// <summary>无法安全预渲染的标识符继续交给运行时校验，不生成绕过校验的 SQL。</summary>
    [Theory]
    [InlineData("bad;table")]
    [InlineData("*")]
    [InlineData("table.name")]
    public void Generate_ModelMutationSql_ShouldDeclineUnsupportedIdentifiers(string table)
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; using System.ComponentModel.DataAnnotations.Schema; " +
            "[Table(\"" + table + "\")] public sealed class Row {public int Id {get;set;}} " +
            "[SqlModel(typeof(Row))] public static partial class RowContext {}");
        AssertNoCompilationErrors(result);
        var model = Assert.Single((EntityModelMetadata[])Assembly.Load(result.AssemblyImage)
            .GetType("RowContext")!.GetMethod("CreateModels")!.Invoke(null, null)!);
        Assert.Null(typeof(EntityModelMetadata).GetProperty("GeneratedMutationSql")?.GetValue(model));
    }
}
