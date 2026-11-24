using System;
using System.Reflection;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    /// <summary>整行生成式 Returning 为每个映射列输出 CLR 属性别名及完整 Provider SQL。</summary>
    [Theory]
    [InlineData("bing.sqlite", SqlOperationKind.InsertValues,
        "Insert Into `order_rows` (`TenantId`, `display_name`, `Version`) Values (@_p_0, @_p_1, @_p_2) Returning `Id` As `Id`, `TenantId` As `TenantId`, `display_name` As `Name`, `Version` As `Version`, `Total` As `Total`")]
    [InlineData("bing.sqlite", SqlOperationKind.Update,
        "Update `order_rows` Set `display_name` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `TenantId` = @_p_3 And `Version` = @_p_4 Returning `Id` As `Id`, `TenantId` As `TenantId`, `display_name` As `Name`, `Version` As `Version`, `Total` As `Total`")]
    [InlineData("bing.sqlite", SqlOperationKind.Delete,
        "Delete From `order_rows` Where `Id` = @_p_0 And `TenantId` = @_p_1 And `Version` = @_p_2 Returning `Id` As `Id`, `TenantId` As `TenantId`, `display_name` As `Name`, `Version` As `Version`, `Total` As `Total`")]
    [InlineData("bing.postgresql", SqlOperationKind.InsertValues,
        "Insert Into \"order_rows\" (\"TenantId\", \"display_name\", \"Version\") Values (@_p_0, @_p_1, @_p_2) Returning \"Id\" As \"Id\", \"TenantId\" As \"TenantId\", \"display_name\" As \"Name\", \"Version\" As \"Version\", \"Total\" As \"Total\"")]
    [InlineData("bing.postgresql", SqlOperationKind.Update,
        "Update \"order_rows\" Set \"display_name\" = @_p_0, \"Version\" = @_p_1 Where \"Id\" = @_p_2 And \"TenantId\" = @_p_3 And \"Version\" = @_p_4 Returning \"Id\" As \"Id\", \"TenantId\" As \"TenantId\", \"display_name\" As \"Name\", \"Version\" As \"Version\", \"Total\" As \"Total\"")]
    [InlineData("bing.postgresql", SqlOperationKind.Delete,
        "Delete From \"order_rows\" Where \"Id\" = @_p_0 And \"TenantId\" = @_p_1 And \"Version\" = @_p_2 Returning \"Id\" As \"Id\", \"TenantId\" As \"TenantId\", \"display_name\" As \"Name\", \"Version\" As \"Version\", \"Total\" As \"Total\"")]
    [InlineData("bing.sqlserver", SqlOperationKind.InsertValues,
        "Insert Into [order_rows] ([TenantId], [display_name], [Version]) Output [Inserted].[Id] As [Id], [Inserted].[TenantId] As [TenantId], [Inserted].[display_name] As [Name], [Inserted].[Version] As [Version], [Inserted].[Total] As [Total] Values (@_p_0, @_p_1, @_p_2)")]
    [InlineData("bing.sqlserver", SqlOperationKind.Update,
        "Update [order_rows] Set [display_name] = @_p_0, [Version] = @_p_1 Output [Inserted].[Id] As [Id], [Inserted].[TenantId] As [TenantId], [Inserted].[display_name] As [Name], [Inserted].[Version] As [Version], [Inserted].[Total] As [Total] Where [Id] = @_p_2 And [TenantId] = @_p_3 And [Version] = @_p_4")]
    [InlineData("bing.sqlserver", SqlOperationKind.Delete,
        "Delete From [order_rows] Output [Deleted].[Id] As [Id], [Deleted].[TenantId] As [TenantId], [Deleted].[display_name] As [Name], [Deleted].[Version] As [Version], [Deleted].[Total] As [Total] Where [Id] = @_p_0 And [TenantId] = @_p_1 And [Version] = @_p_2")]
    public void Generate_ModelMutationReturningSql_ShouldReturnCompleteProviderStatement(
        string provider, SqlOperationKind operation, string expected)
    {
        var result = GenerateReturningModelForTest();
        var factory = GetRequiredReturningFactoryForTest(result);

        Assert.Equal(expected, factory(provider, operation));
    }

    /// <summary>生成的 Returning SQL 合同对不支持 Provider 返回空值。</summary>
    [Theory]
    [InlineData("bing.mysql")]
    [InlineData("bing.oracle")]
    [InlineData("bing.unknown")]
    public void Generate_ModelMutationReturningSql_ShouldDeclineUnsupportedProviders(string provider)
    {
        var result = GenerateReturningModelForTest();
        var factory = GetRequiredReturningFactoryForTest(result);

        Assert.Null(factory(provider, SqlOperationKind.InsertValues));
        Assert.Null(factory(provider, SqlOperationKind.Update));
        Assert.Null(factory(provider, SqlOperationKind.Delete));
    }

    /// <summary>只有 SqlModel 写模型生成整行 Returning，参数和结果角色不生成该工厂。</summary>
    [Theory]
    [InlineData("SqlParameters")]
    [InlineData("SqlResult")]
    public void Generate_ModelMutationReturningSql_ShouldRespectRegistrationRole(string role)
    {
        var result = GenerateReturningModelForTest(role);

        Assert.Null(GetReturningFactoryPropertyForTest().GetValue(result));
    }

    /// <summary>不安全的表名或映射列名不能绕过运行时标识符校验生成静态 SQL。</summary>
    [Theory]
    [InlineData("bad;table", "display_name")]
    [InlineData("order_rows", "bad;column")]
    public void Generate_ModelMutationReturningSql_ShouldDeclineUnsafeIdentifiers(
        string tableName, string nameColumn)
    {
        var result = GenerateReturningModelForTest(tableName: tableName, nameColumn: nameColumn);

        Assert.Null(GetReturningFactoryPropertyForTest().GetValue(result));
    }

    /// <summary>带 Schema 的整行 SQL 保留非 SQLite Provider，SQLite 不生成可绕过校验的语句。</summary>
    [Fact]
    public void Generate_ModelMutationReturningSql_ShouldRespectSchemaProviderRules()
    {
        var result = GenerateReturningModelForTest(schema: "sales_2026");
        var factory = GetRequiredReturningFactoryForTest(result);

        Assert.Null(factory("bing.sqlite", SqlOperationKind.InsertValues));
        Assert.Equal(
            "Insert Into \"sales_2026\".\"order_rows\" (\"TenantId\", \"display_name\", \"Version\") Values (@_p_0, @_p_1, @_p_2) Returning \"Id\" As \"Id\", \"TenantId\" As \"TenantId\", \"display_name\" As \"Name\", \"Version\" As \"Version\", \"Total\" As \"Total\"",
            factory("bing.postgresql", SqlOperationKind.InsertValues));
    }

    /// <summary>新增 Returning SQL 合同升级和降级生成输入，无关文件编辑保持缓存命中。</summary>
    [Fact]
    public void Generate_WhenMutationReturningSqlContractChanges_ShouldInvalidateOnlyRelevantInput()
    {
        var legacy = RuntimeContractSource(includeGeneratedContracts: true) +
            "namespace Bing.Data.Sql.Builders { public enum SqlOperationKind { InsertValues, Update, Delete } }";
        var current = legacy.Replace("public Func<object> GeneratedFactory { get; }",
            "public Func<object> GeneratedFactory { get; } " +
            "public Func<string, Bing.Data.Sql.Builders.SqlOperationKind, string> GeneratedMutationReturningSql { get; set; }",
            StringComparison.Ordinal);
        var first = RunIncremental(false, new SourceInput("Runtime.cs", legacy),
            new SourceInput("Model.cs", "using Bing.Data.Sql.Generated; public sealed class Row { " +
                "public int Id {get;set;} public string Name {get;set;} } " +
                "[SqlModel(typeof(Row))] public partial class Context {}"),
            new SourceInput("Other.cs", "public class Other {}"));
        string Generated(GeneratorDriver driver) =>
            Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources).SourceText.ToString();
        Assert.DoesNotContain("GeneratedMutationReturningSql", Generated(first.Driver), StringComparison.Ordinal);

        var upgraded = UpdateIncremental(first, "Runtime.cs", current);
        Assert.Single(GetGenerateReasons(upgraded.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.Contains("GeneratedMutationReturningSql =", Generated(upgraded.Driver), StringComparison.Ordinal);

        var unrelated = UpdateIncremental(upgraded, "Other.cs", "public class Other { public int Value {get;set;} }");
        Assert.Single(GetGenerateReasons(unrelated.Driver), reason => reason == IncrementalStepRunReason.Cached);
        Assert.Equal(Generated(upgraded.Driver), Generated(unrelated.Driver));

        var downgraded = UpdateIncremental(unrelated, "Runtime.cs", legacy);
        Assert.Single(GetGenerateReasons(downgraded.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.DoesNotContain("GeneratedMutationReturningSql", Generated(downgraded.Driver), StringComparison.Ordinal);
    }

    private static EntityModelMetadata GenerateReturningModelForTest(string role = "SqlModel",
        string tableName = "order_rows", string schema = null, string nameColumn = "display_name")
    {
        var schemaAttribute = schema == null ? string.Empty : ", Schema=\"" + schema + "\"";
        var result = RunGenerator("using Bing.Data.Sql.Generated; " +
            "using System.ComponentModel.DataAnnotations; using System.ComponentModel.DataAnnotations.Schema; " +
            "[Table(\"" + tableName + "\"" + schemaAttribute + ")] public sealed class OrderRow { " +
            "[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id {get;set;} " +
            "[Key] public int TenantId {get;set;} [Column(\"" + nameColumn + "\")] public string Name {get;set;} " +
            "[ConcurrencyCheck] public int Version {get;set;} " +
            "[DatabaseGenerated(DatabaseGeneratedOption.Computed)] public int Total {get;set;} " +
            "[NotMapped] public string Local {get;set;} } [" + role +
            "(typeof(OrderRow))] public partial class ReturningContext {}");
        AssertNoCompilationErrors(result);
        return Assert.Single((EntityModelMetadata[])Assembly.Load(result.AssemblyImage)
            .GetType("ReturningContext")!.GetMethod("CreateModels")!.Invoke(null, null)!);
    }

    private static Func<string, SqlOperationKind, string> GetRequiredReturningFactoryForTest(
        EntityModelMetadata model) => Assert.IsType<Func<string, SqlOperationKind, string>>(
        GetReturningFactoryPropertyForTest().GetValue(model));

    private static PropertyInfo GetReturningFactoryPropertyForTest()
    {
        var property = typeof(EntityModelMetadata).GetProperty("GeneratedMutationReturningSql",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        Assert.Equal(typeof(Func<string, SqlOperationKind, string>), property!.PropertyType);
        return property;
    }
}
