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
    /// <summary>只展开单列写入和双行默认列形状，不枚举任意列组合。</summary>
    [Theory]
    [InlineData("bing.sqlite", SqlOperationKind.InsertValues, "Name", 1, "Insert Into `rows` (`display_name`) Values (@_p_0)")]
    [InlineData("bing.mysql", SqlOperationKind.InsertValues, "Name", 1, "Insert Into `rows` (`display_name`) Values (@_p_0)")]
    [InlineData("bing.postgresql", SqlOperationKind.InsertValues, "Name", 1, "Insert Into \"rows\" (\"display_name\") Values (@_p_0)")]
    [InlineData("bing.sqlserver", SqlOperationKind.InsertValues, "Name", 1, "Insert Into [rows] ([display_name]) Values (@_p_0)")]
    [InlineData("bing.oracle", SqlOperationKind.InsertValues, "Name", 1, "Insert Into \"rows\" (\"display_name\") Values (:p_0)")]
    [InlineData("bing.sqlite", SqlOperationKind.Update, "Name", 1, "Update `rows` Set `display_name` = @_p_0 Where `Id` = @_p_1 And `Version` = @_p_2")]
    [InlineData("bing.mysql", SqlOperationKind.Update, "Version", 1, "Update `rows` Set `Version` = @_p_0 Where `Id` = @_p_1 And `Version` = @_p_2")]
    [InlineData("bing.postgresql", SqlOperationKind.Update, "Name", 1, "Update \"rows\" Set \"display_name\" = @_p_0 Where \"Id\" = @_p_1 And \"Version\" = @_p_2")]
    [InlineData("bing.sqlserver", SqlOperationKind.Update, "Name", 1, "Update [rows] Set [display_name] = @_p_0 Where [Id] = @_p_1 And [Version] = @_p_2")]
    [InlineData("bing.oracle", SqlOperationKind.Update, "Name", 1, "Update \"rows\" Set \"display_name\" = :p_0 Where \"Id\" = :p_1 And \"Version\" = :p_2")]
    [InlineData("bing.sqlite", SqlOperationKind.InsertValues, null, 2, "Insert Into `rows` (`display_name`, `Version`) Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData("bing.mysql", SqlOperationKind.InsertValues, null, 2, "Insert Into `rows` (`display_name`, `Version`) Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData("bing.postgresql", SqlOperationKind.InsertValues, null, 2, "Insert Into \"rows\" (\"display_name\", \"Version\") Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData("bing.sqlserver", SqlOperationKind.InsertValues, null, 2, "Insert Into [rows] ([display_name], [Version]) Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    public void Generate_FiniteMutationShape_ShouldReturnCompleteConstant(string provider, SqlOperationKind operation,
        string property, int rows, string expected)
    {
        var factory = ShapeFactory();
        Assert.Equal(expected, factory(provider, operation, property, rows));
        Assert.Same(factory(provider, operation, property, rows), factory(provider, operation, property, rows));
    }

    /// <summary>未知列、键列更新、未声明行数和 Oracle 多行形状均明确回退。</summary>
    [Theory]
    [InlineData("custom", SqlOperationKind.Update, "Name", 1)]
    [InlineData("bing.sqlite", SqlOperationKind.Update, "Id", 1)]
    [InlineData("bing.sqlite", SqlOperationKind.Update, "missing", 1)]
    [InlineData("bing.mysql", SqlOperationKind.Update, "missing", 1)]
    [InlineData("bing.postgresql", SqlOperationKind.Update, "missing", 1)]
    [InlineData("bing.sqlserver", SqlOperationKind.Update, "missing", 1)]
    [InlineData("bing.oracle", SqlOperationKind.Update, "missing", 1)]
    [InlineData("bing.sqlite", SqlOperationKind.Update, null, 1)]
    [InlineData("bing.sqlite", SqlOperationKind.InsertValues, null, 3)]
    [InlineData("bing.sqlite", SqlOperationKind.InsertValues, "Name", 2)]
    [InlineData("bing.oracle", SqlOperationKind.InsertValues, null, 2)]
    public void Generate_UnsupportedMutationShape_ShouldReturnNull(string provider, SqlOperationKind operation,
        string property, int rows) => Assert.Null(ShapeFactory()(provider, operation, property, rows));

    private static Func<string, SqlOperationKind, string, int, string> ShapeFactory()
    {
        var model = GenerateProviderSqlModel("[Column(\"display_name\")] public string Name {get;set;} " +
            "[ConcurrencyCheck] public int Version {get;set;}");
        var contract = typeof(EntityModelMetadata).GetProperty("GeneratedMutationShapeSql");
        Assert.NotNull(contract);
        return Assert.IsType<Func<string, SqlOperationKind, string, int, string>>(contract!.GetValue(model));
    }

    /// <summary>新合同升级或降级触发重新生成，无关源码修改继续命中增量缓存。</summary>
    [Fact]
    public void Generate_WhenMutationShapeContractChanges_ShouldInvalidateOnlyRelevantInput()
    {
        var legacy = RuntimeContractSource(includeGeneratedContracts: true);
        var current = legacy.Replace("public Func<object> GeneratedFactory { get; }",
            "public Func<object> GeneratedFactory { get; } public Func<string, Bing.Data.Sql.Builders.SqlOperationKind, string, int, string> GeneratedMutationShapeSql {get;set;}",
            StringComparison.Ordinal);
        current += " namespace Bing.Data.Sql.Builders { public enum SqlOperationKind { InsertValues, Update, Delete } }";
        var first = RunIncremental(false, new SourceInput("Runtime.cs", legacy),
            new SourceInput("Model.cs", "using Bing.Data.Sql.Generated; using System.ComponentModel.DataAnnotations; " +
                "public class Row { [Key] public int Id {get;set;} public string Name {get;set;} } " +
                "[SqlModel(typeof(Row))] public partial class Context {}"),
            new SourceInput("Other.cs", "public class Other {}"));
        string Generated(GeneratorDriver driver) => Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources).SourceText.ToString();
        Assert.DoesNotContain("GeneratedMutationShapeSql", Generated(first.Driver), StringComparison.Ordinal);
        var upgraded = UpdateIncremental(first, "Runtime.cs", current);
        Assert.Single(GetGenerateReasons(upgraded.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.Contains("GeneratedMutationShapeSql =", Generated(upgraded.Driver), StringComparison.Ordinal);
        var unrelated = UpdateIncremental(upgraded, "Other.cs", "public class Other {public int Value {get;set;}}");
        Assert.Single(GetGenerateReasons(unrelated.Driver), reason => reason == IncrementalStepRunReason.Cached);
        Assert.Equal(Generated(upgraded.Driver), Generated(unrelated.Driver));
        var downgraded = UpdateIncremental(unrelated, "Runtime.cs", legacy);
        Assert.Single(GetGenerateReasons(downgraded.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.DoesNotContain("GeneratedMutationShapeSql", Generated(downgraded.Driver), StringComparison.Ordinal);
    }

    /// <summary>结果模型和需要运行时解析的对象名不得带入预生成写 SQL。</summary>
    [Theory]
    [InlineData("SqlResult", "rows", null)]
    [InlineData("SqlModel", "bad;table", null)]
    [InlineData("SqlModel", "table.name", null)]
    [InlineData("SqlModel", "rows", "custom_schema")]
    public void Generate_WhenModelCannotUseStaticMutationSql_ShouldOmitShapeFactory(string role, string table, string schema)
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; using System.ComponentModel.DataAnnotations.Schema; " +
            "[Table(\"" + table + "\"" + (schema == null ? "" : ",Schema=\"" + schema + "\"") + ")] " +
            "public class Row { public int Id {get;set;} } [" + role + "(typeof(Row))] public partial class Context {}");
        AssertNoCompilationErrors(result);
        var model = Assert.Single((EntityModelMetadata[])Assembly.Load(result.AssemblyImage)
            .GetType("Context")!.GetMethod("CreateModels")!.Invoke(null, null)!);
        Assert.Null(model.GeneratedMutationShapeSql);
    }
}
