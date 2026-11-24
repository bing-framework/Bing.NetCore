using System;
using System.Collections.Generic;
using System.Reflection;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    /// <summary>按所选可写列为五种 Provider 生成完整 Insert/Update SQL，并按写入顺序分配参数。</summary>
    [Fact]
    public void Generate_SelectedMutationColumnsSql_ShouldRenderCompleteInsertAndUpdateForAllProviders()
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());
        var cases = new (string Provider, SqlOperationKind Operation, string[] Columns, string Expected)[]
        {
            ("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Price", "Name" },
                "Insert Into `selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1)"),
            ("bing.sqlite", SqlOperationKind.Update, new[] { "Price", "Name" },
                "Update `selected_rows` Set `unit_price` = @_p_0, `display_name` = @_p_1 Where `row_id` = @_p_2 And `row_version` = @_p_3"),
            ("bing.mysql", SqlOperationKind.InsertValues, new[] { "Price", "Name" },
                "Insert Into `selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1)"),
            ("bing.mysql", SqlOperationKind.Update, new[] { "Price", "Name" },
                "Update `selected_rows` Set `unit_price` = @_p_0, `display_name` = @_p_1 Where `row_id` = @_p_2 And `row_version` = @_p_3"),
            ("bing.postgresql", SqlOperationKind.InsertValues, new[] { "Price", "Name" },
                "Insert Into \"selected_rows\" (\"unit_price\", \"display_name\") Values (@_p_0, @_p_1)"),
            ("bing.postgresql", SqlOperationKind.Update, new[] { "Price", "Name" },
                "Update \"selected_rows\" Set \"unit_price\" = @_p_0, \"display_name\" = @_p_1 Where \"row_id\" = @_p_2 And \"row_version\" = @_p_3"),
            ("bing.sqlserver", SqlOperationKind.InsertValues, new[] { "Price", "Name" },
                "Insert Into [selected_rows] ([unit_price], [display_name]) Values (@_p_0, @_p_1)"),
            ("bing.sqlserver", SqlOperationKind.Update, new[] { "Price", "Name" },
                "Update [selected_rows] Set [unit_price] = @_p_0, [display_name] = @_p_1 Where [row_id] = @_p_2 And [row_version] = @_p_3"),
            ("bing.oracle", SqlOperationKind.InsertValues, new[] { "Price", "Name" },
                "Insert Into \"selected_rows\" (\"unit_price\", \"display_name\") Values (:p_0, :p_1)"),
            ("bing.oracle", SqlOperationKind.Update, new[] { "Price", "Name" },
                "Update \"selected_rows\" Set \"unit_price\" = :p_0, \"display_name\" = :p_1 Where \"row_id\" = :p_2 And \"row_version\" = :p_3")
        };

        foreach (var item in cases)
            Assert.Equal(item.Expected, factory(item.Provider, item.Operation, item.Columns, 1, false));
    }

    /// <summary>Insert/Update 整行 Returning 覆盖 SQLite、PostgreSQL、SQL Server，并保持 SQL Server Output 子句位置。</summary>
    [Fact]
    public void Generate_SelectedMutationColumnsSql_ShouldRenderFullReturningForSupportedProviders()
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());
        var returningColumns =
            "`row_id` As `Id`, `display_name` As `Name`, `unit_price` As `Price`, `stock_code` As `Code`, `calculated_value` As `Calculated`, `row_version` As `Version`";
        var postgresReturningColumns =
            "\"row_id\" As \"Id\", \"display_name\" As \"Name\", \"unit_price\" As \"Price\", \"stock_code\" As \"Code\", \"calculated_value\" As \"Calculated\", \"row_version\" As \"Version\"";
        var cases = new (string Provider, SqlOperationKind Operation, string[] Columns, string Expected)[]
        {
            ("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Price", "Name" },
                "Insert Into `selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1) Returning " + returningColumns),
            ("bing.sqlite", SqlOperationKind.Update, new[] { "Price", "Name" },
                "Update `selected_rows` Set `unit_price` = @_p_0, `display_name` = @_p_1 Where `row_id` = @_p_2 And `row_version` = @_p_3 Returning " + returningColumns),
            ("bing.postgresql", SqlOperationKind.InsertValues, new[] { "Price", "Name" },
                "Insert Into \"selected_rows\" (\"unit_price\", \"display_name\") Values (@_p_0, @_p_1) Returning " + postgresReturningColumns),
            ("bing.postgresql", SqlOperationKind.Update, new[] { "Price", "Name" },
                "Update \"selected_rows\" Set \"unit_price\" = @_p_0, \"display_name\" = @_p_1 Where \"row_id\" = @_p_2 And \"row_version\" = @_p_3 Returning " + postgresReturningColumns),
            ("bing.sqlserver", SqlOperationKind.InsertValues, new[] { "Price", "Name" },
                "Insert Into [selected_rows] ([unit_price], [display_name]) Output [Inserted].[row_id] As [Id], [Inserted].[display_name] As [Name], [Inserted].[unit_price] As [Price], [Inserted].[stock_code] As [Code], [Inserted].[calculated_value] As [Calculated], [Inserted].[row_version] As [Version] Values (@_p_0, @_p_1)"),
            ("bing.sqlserver", SqlOperationKind.Update, new[] { "Price", "Name" },
                "Update [selected_rows] Set [unit_price] = @_p_0, [display_name] = @_p_1 Output [Inserted].[row_id] As [Id], [Inserted].[display_name] As [Name], [Inserted].[unit_price] As [Price], [Inserted].[stock_code] As [Code], [Inserted].[calculated_value] As [Calculated], [Inserted].[row_version] As [Version] Where [row_id] = @_p_2 And [row_version] = @_p_3")
        };

        foreach (var item in cases)
            Assert.Equal(item.Expected, factory(item.Provider, item.Operation, item.Columns, 1, true));
    }

    /// <summary>不支持 Returning 的 Provider 对整行返回请求返回空值。</summary>
    [Theory]
    [InlineData("bing.mysql")]
    [InlineData("bing.oracle")]
    public void Generate_SelectedMutationColumnsSql_ShouldDeclineReturningForUnsupportedProviders(string provider)
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());

        Assert.Null(factory(provider, SqlOperationKind.InsertValues, new[] { "Price", "Name" }, 1, true));
        Assert.Null(factory(provider, SqlOperationKind.Update, new[] { "Price", "Name" }, 1, true));
    }

    /// <summary>非法 Provider、未知列、空写入列集合和不支持操作均不能生成静态 SQL。</summary>
    [Fact]
    public void Generate_SelectedMutationColumnsSql_ShouldDeclineUnsupportedShapes()
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());

        Assert.Null(factory("bing.unknown", SqlOperationKind.Update, new[] { "Name" }, 1, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Name", "Missing" }, 1, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.Update, new[] { "Name", "Missing" }, 1, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.InsertValues, Array.Empty<string>(), 1, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.Update, Array.Empty<string>(), 1, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.Delete, Array.Empty<string>(), 1, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.Delete, Array.Empty<string>(), 1, true));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.Select, new[] { "Name" }, 1, false));
    }

    /// <summary>无键模型不能生成 Update，带 Schema 的表不能为 SQLite 预生成 SQL。</summary>
    [Fact]
    public void Generate_SelectedMutationColumnsSql_ShouldRespectKeyAndSchemaConstraints()
    {
        var noKeyFactory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel(includeKey: false));
        Assert.Null(noKeyFactory("bing.sqlite", SqlOperationKind.Update, new[] { "Name" }, 1, false));

        var schemaFactory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel(schema: "sales_2026"));
        Assert.Null(schemaFactory("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Price", "Name" }, 1, false));
        Assert.Null(schemaFactory("bing.sqlite", SqlOperationKind.Update, new[] { "Price", "Name" }, 1, true));
        Assert.Equal(
            "Insert Into \"sales_2026\".\"selected_rows\" (\"unit_price\", \"display_name\") Values (@_p_0, @_p_1) Returning \"row_id\" As \"Id\", \"display_name\" As \"Name\", \"unit_price\" As \"Price\", \"stock_code\" As \"Code\", \"calculated_value\" As \"Calculated\", \"row_version\" As \"Version\"",
            schemaFactory("bing.postgresql", SqlOperationKind.InsertValues, new[] { "Price", "Name" }, 1, true));
    }

    /// <summary>生成键和 Computed 列不属于可写输入，不能被静态 SQL 工厂悄悄剔除。</summary>
    [Fact]
    public void Generate_SelectedMutationColumnsSql_ShouldDeclineNonWritableColumns()
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());

        Assert.Null(factory("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Id", "Price", "Name" }, 1, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.Update, new[] { "Id", "Price", "Name" }, 1, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Calculated", "Price", "Name" }, 1, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.Update, new[] { "Calculated", "Price", "Name" }, 1, false));

        Assert.Equal("Insert Into `selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1)",
            factory("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Price", "Name" }, 1, false));
    }

    /// <summary>参数和结果角色不生成面向写模型的选列 SQL 工厂。</summary>
    [Theory]
    [InlineData("SqlParameters")]
    [InlineData("SqlResult")]
    public void Generate_SelectedMutationColumnsSql_ShouldRespectRegistrationRole(string role)
    {
        var model = GenerateSelectedColumnsModel(role: role);
        var property = GetSelectedColumnsFactoryPropertyForTest();

        Assert.Null(property.GetValue(model));
    }

    private static EntityModelMetadata GenerateSelectedColumnsModel(string schema = null, bool includeKey = true,
        string role = "SqlModel")
    {
        var schemaArgument = schema == null ? string.Empty : ", Schema=\"" + schema + "\"";
        var key = includeKey
            ? "[Key, Column(\"row_id\"), DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id {get;set;}"
            : string.Empty;
        var result = RunGenerator("using Bing.Data.Sql.Generated; " +
            "using System.ComponentModel.DataAnnotations; using System.ComponentModel.DataAnnotations.Schema; " +
            "[Table(\"selected_rows\"" + schemaArgument + ")] public sealed class SelectedRow { " + key +
            "[Column(\"display_name\")] public string Name {get;set;} " +
            "[Column(\"unit_price\")] public decimal Price {get;set;} " +
            "[Column(\"stock_code\")] public string Code {get;set;} " +
            "[DatabaseGenerated(DatabaseGeneratedOption.Computed), Column(\"calculated_value\")] public int Calculated {get;set;} " +
            "[ConcurrencyCheck, Column(\"row_version\")] public int Version {get;set;} } " +
            "[" + role + "(typeof(SelectedRow))] public static partial class SelectedColumnsContext {}");
        AssertNoCompilationErrors(result);
        return LoadSelectedColumnsModel(result);
    }

    private static EntityModelMetadata LoadSelectedColumnsModel(GeneratorResult result) =>
        Assert.Single((EntityModelMetadata[])Assembly.Load(result.AssemblyImage)
            .GetType("SelectedColumnsContext")!.GetMethod("CreateModels")!.Invoke(null, null)!);

    private static Func<string, SqlOperationKind, IReadOnlyList<string>, int, bool, string>
        GetSelectedColumnsFactoryForTest(EntityModelMetadata model)
    {
        var property = GetSelectedColumnsFactoryPropertyForTest();
        Assert.NotNull(property);
        Assert.Equal(typeof(Func<string, SqlOperationKind, IReadOnlyList<string>, int, bool, string>), property!.PropertyType);
        return Assert.IsType<Func<string, SqlOperationKind, IReadOnlyList<string>, int, bool, string>>(
            property.GetValue(model));
    }

    private static PropertyInfo GetSelectedColumnsFactoryPropertyForTest()
    {
        var property = typeof(EntityModelMetadata).GetProperty("GeneratedMutationColumnsSql",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        Assert.Equal(typeof(Func<string, SqlOperationKind, IReadOnlyList<string>, int, bool, string>), property!.PropertyType);
        return property;
    }

    /// <summary>精确合同变更使生成输入失效，错误签名不生成赋值，无关编辑命中缓存。</summary>
    [Fact]
    public void Generate_WhenMutationColumnsSqlContractChanges_ShouldInvalidateAndRequireExactSignature()
    {
        var legacy = RuntimeContractSource(includeGeneratedContracts: true) +
            "namespace Bing.Data.Sql.Builders { public enum SqlOperationKind { InsertValues, Update, Delete } }";
        var exact = legacy.Replace("public Func<object> GeneratedFactory { get; }",
            "public Func<object> GeneratedFactory { get; } " +
            "public Func<string, Bing.Data.Sql.Builders.SqlOperationKind, IReadOnlyList<string>, int, bool, string> GeneratedMutationColumnsSql { get; set; }",
            StringComparison.Ordinal);
        var legacyFourParameter = legacy.Replace("public Func<object> GeneratedFactory { get; }",
            "public Func<object> GeneratedFactory { get; } " +
            "public Func<string, Bing.Data.Sql.Builders.SqlOperationKind, IReadOnlyList<string>, bool, string> GeneratedMutationColumnsSql { get; set; }",
            StringComparison.Ordinal);
        var incompatible = legacy.Replace("public Func<object> GeneratedFactory { get; }",
            "public Func<object> GeneratedFactory { get; } " +
            "public Func<string, Bing.Data.Sql.Builders.SqlOperationKind, List<string>, int, bool, string> GeneratedMutationColumnsSql { get; set; }",
            StringComparison.Ordinal);
        var first = RunIncremental(false, new SourceInput("Runtime.cs", legacy),
            new SourceInput("Model.cs", "using Bing.Data.Sql.Generated; public sealed class Row { " +
                "public int Id {get;set;} public string Name {get;set;} } " +
                "[SqlModel(typeof(Row))] public partial class Context {}"),
            new SourceInput("Other.cs", "public class Other {}"));
        string Generated(GeneratorDriver driver) =>
            Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources).SourceText.ToString();

        Assert.DoesNotContain("GeneratedMutationColumnsSql =", Generated(first.Driver), StringComparison.Ordinal);

        var upgraded = UpdateIncremental(first, "Runtime.cs", exact);
        Assert.Single(GetGenerateReasons(upgraded.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.Contains("GeneratedMutationColumnsSql =", Generated(upgraded.Driver), StringComparison.Ordinal);

        var unrelated = UpdateIncremental(upgraded, "Other.cs", "public class Other { public int Value {get;set;} }");
        Assert.Single(GetGenerateReasons(unrelated.Driver), reason => reason == IncrementalStepRunReason.Cached);
        Assert.Equal(Generated(upgraded.Driver), Generated(unrelated.Driver));

        var wrongSignature = UpdateIncremental(unrelated, "Runtime.cs", incompatible);
        Assert.Single(GetGenerateReasons(wrongSignature.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.DoesNotContain("GeneratedMutationColumnsSql =", Generated(wrongSignature.Driver), StringComparison.Ordinal);

        var oldContract = UpdateIncremental(wrongSignature, "Runtime.cs", legacyFourParameter);
        // 两个不兼容合同都关闭该可选能力，语义输入不变，应复用相同输出。
        Assert.Single(GetGenerateReasons(oldContract.Driver), reason => reason == IncrementalStepRunReason.Cached);
        Assert.DoesNotContain("GeneratedMutationColumnsSql =", Generated(oldContract.Driver), StringComparison.Ordinal);

        var restored = UpdateIncremental(oldContract, "Runtime.cs", exact);
        Assert.Single(GetGenerateReasons(restored.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.Contains("GeneratedMutationColumnsSql =", Generated(restored.Driver), StringComparison.Ordinal);

        var downgraded = UpdateIncremental(restored, "Runtime.cs", legacy);
        Assert.Single(GetGenerateReasons(downgraded.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.DoesNotContain("GeneratedMutationColumnsSql =", Generated(downgraded.Driver), StringComparison.Ordinal);
    }
}
