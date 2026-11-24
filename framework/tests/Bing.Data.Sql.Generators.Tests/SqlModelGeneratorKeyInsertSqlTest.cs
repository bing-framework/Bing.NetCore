using System;
using System.Linq;
using System.Reflection;
using Bing.Data.Sql.Metadata;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    /// <summary>生成完整自增键 Insert/Returning，排除计算列并保留实际列名及参数顺序。</summary>
    [Theory]
    [InlineData("bing.sqlite", false, "Insert Into `identity_rows` (`display_name`, `Version`) Values (@_p_0, @_p_1) Returning `row_id`")]
    [InlineData("bing.sqlite", true, "Insert Into `identity_rows` DEFAULT VALUES RETURNING `row_id`")]
    [InlineData("bing.oracle", false, "Insert Into \"identity_rows\" (\"display_name\", \"Version\") Values (:p_0, :p_1) Returning \"row_id\" Into :bing_identity_key")]
    [InlineData("bing.oracle", true, "Insert Into \"identity_rows\" (\"row_id\") Values (DEFAULT) Returning \"row_id\" Into :bing_identity_key")]
    public void Generate_KeyInsertSql_ShouldEmitCompleteIdentityReturning(string provider, bool keyOnly, string expected)
    {
        var members = keyOnly ? string.Empty :
            "[Column(\"display_name\")] public string Name {get;set;} public int Version {get;set;}";
        var result = RunGenerator("""
            using Bing.Data.Sql.Generated;
            using System.ComponentModel.DataAnnotations;
            using System.ComponentModel.DataAnnotations.Schema;
            [Table("identity_rows")] public sealed class IdentityRow {
              [Key, Column("row_id"), DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id {get;set;}
              [DatabaseGenerated(DatabaseGeneratedOption.Computed)] public int Computed {get;set;}
              [NotMapped] public string Local {get;set;}
            """ + members + "} [SqlModel(typeof(IdentityRow))] public partial class KeyInsertContext {}");
        AssertNoCompilationErrors(result);
        var model = Assert.Single((EntityModelMetadata[])Assembly.Load(result.AssemblyImage)
            .GetType("KeyInsertContext")!.GetMethod("CreateModels")!.Invoke(null, null)!);
        var factory = Assert.IsType<Func<string, string>>(typeof(EntityModelMetadata)
            .GetProperty("GeneratedKeyInsertSql")?.GetValue(model));
        Assert.Equal(expected, factory(provider));
        Assert.Null(factory("custom"));
    }

    /// <summary>仅单个自增主键的写模型获得专用 SQL，其他形状继续现有路径。</summary>
    [Theory]
    [InlineData("SqlParameters", "[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]")]
    [InlineData("SqlResult", "[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]")]
    [InlineData("SqlModel", "[Key, DatabaseGenerated(DatabaseGeneratedOption.None)]")]
    [InlineData("SqlModel", "[Key, DatabaseGenerated(DatabaseGeneratedOption.Computed)]")]
    [InlineData("SqlModel", "")]
    public void Generate_KeyInsertSql_ShouldLimitToIdentityWriteModels(string role, string keyAttributes)
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; using System.ComponentModel.DataAnnotations; " +
            "using System.ComponentModel.DataAnnotations.Schema; public class Row {" + keyAttributes +
            "public long Id {get;set;} public string Name {get;set;} } [" + role +
            "(typeof(Row))] public partial class KeyRoleContext {}");
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain("GeneratedKeyInsertSql =", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>复合键、无效 Schema 或不可静态渲染表名不生成单键 Returning 常量。</summary>
    [Theory]
    [InlineData("[Table(\"rows\")]", "[Key] public int Tenant {get;set;}")]
    [InlineData("[Table(\"rows\", Schema=\"bad;schema\")]", "")]
    [InlineData("[Table(\"bad;table\")]", "")]
    public void Generate_KeyInsertSql_ShouldKeepUnsupportedShapesOnRuntimePath(string table, string extraKey)
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; using System.ComponentModel.DataAnnotations; " +
            "using System.ComponentModel.DataAnnotations.Schema; " + table + " public class Row {" +
            "[Key,DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id {get;set;} " + extraKey +
            "public string Name {get;set;} } [SqlModel(typeof(Row))] public partial class KeyShapeContext {}");
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain("GeneratedKeyInsertSql =", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>可选生成键 SQL 合同升级/降级使对应上下文重新生成，无关文件仍命中。</summary>
    [Fact]
    public void Generate_WhenKeyInsertSqlContractChanges_ShouldInvalidateOnlyRelevantInput()
    {
        var legacy = RuntimeContractSource(includeGeneratedContracts: true);
        var current = legacy.Replace("public Func<object> GeneratedFactory { get; }",
            "public Func<object> GeneratedFactory { get; } public Func<string,string> GeneratedKeyInsertSql {get;set;}",
            StringComparison.Ordinal);
        var first = RunIncremental(false, new SourceInput("Runtime.cs", legacy),
            new SourceInput("Model.cs", "using Bing.Data.Sql.Generated; using System.ComponentModel.DataAnnotations; " +
                "using System.ComponentModel.DataAnnotations.Schema; public sealed class Row {" +
                "[Key,DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id {get;set;} " +
                "public string Name {get;set;} } [SqlModel(typeof(Row))] public partial class Context {}"),
            new SourceInput("Other.cs", "public class Other {}"));
        string Generated(GeneratorDriver driver) =>
            Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources).SourceText.ToString();
        Assert.DoesNotContain("GeneratedKeyInsertSql", Generated(first.Driver), StringComparison.Ordinal);
        var upgraded = UpdateIncremental(first, "Runtime.cs", current);
        Assert.Single(GetGenerateReasons(upgraded.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.Contains("GeneratedKeyInsertSql =", Generated(upgraded.Driver), StringComparison.Ordinal);
        var unrelated = UpdateIncremental(upgraded, "Other.cs", "public class Other {public int Value {get;set;}}");
        Assert.Single(GetGenerateReasons(unrelated.Driver), reason => reason == IncrementalStepRunReason.Cached);
        Assert.Equal(Generated(upgraded.Driver), Generated(unrelated.Driver));
        var downgraded = UpdateIncremental(unrelated, "Runtime.cs", legacy);
        Assert.Single(GetGenerateReasons(downgraded.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.DoesNotContain("GeneratedKeyInsertSql", Generated(downgraded.Driver), StringComparison.Ordinal);
    }
}
