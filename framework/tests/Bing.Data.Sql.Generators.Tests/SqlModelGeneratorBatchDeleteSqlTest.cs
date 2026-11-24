using System;
using System.Linq;
using System.Reflection;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    /// <summary>单主键批量删除按映射键列生成各 Provider 的完整 IN SQL，覆盖常用和较大参数组。</summary>
    [Theory]
    [InlineData("bing.sqlite", "`", "`", "@_p_")]
    [InlineData("bing.mysql", "`", "`", "@_p_")]
    [InlineData("bing.postgresql", "\"", "\"", "@_p_")]
    [InlineData("bing.sqlserver", "[", "]", "@_p_")]
    [InlineData("bing.oracle", "\"", "\"", ":p_")]
    public void Generate_BatchDeleteWithSingleKey_ShouldReturnCompleteSql(string provider, string open,
        string close, string parameterPrefix)
    {
        var model = GenerateBatchDeleteModel();
        var factory = GetMutationShapeFactory(model);

        Assert.Equal(ExpectedDeleteSql(open, close, parameterPrefix, 3),
            factory(provider, SqlOperationKind.Delete, null, 3));
        Assert.Equal(ExpectedDeleteSql(open, close, parameterPrefix, 12),
            factory(provider, SqlOperationKind.Delete, null, 12));
    }

    /// <summary>静态 Schema 的批量删除保留 Provider 限定表名；SQLite 不生成 Schema 语句。</summary>
    [Theory]
    [InlineData("bing.sqlite", "`", "`", "@_p_")]
    [InlineData("bing.mysql", "`", "`", "@_p_")]
    [InlineData("bing.postgresql", "\"", "\"", "@_p_")]
    [InlineData("bing.sqlserver", "[", "]", "@_p_")]
    [InlineData("bing.oracle", "\"", "\"", ":p_")]
    public void Generate_SchemaBatchDelete_ShouldUseQualifiedTableOrDecline(string provider, string open,
        string close, string parameterPrefix)
    {
        var model = GenerateBatchDeleteModel(schema: "sales_2026");
        var factory = GetMutationShapeFactory(model);

        if (provider == "bing.sqlite")
        {
            Assert.Null(factory(provider, SqlOperationKind.Delete, null, 3));
            return;
        }

        Assert.Equal(ExpectedDeleteSql(open, close, parameterPrefix, 3, "sales_2026"),
            factory(provider, SqlOperationKind.Delete, null, 3));
    }

    /// <summary>非法行数、非删除形状、未知 Provider 或属性均不伪造批量删除 SQL。</summary>
    [Theory]
    [InlineData("bing.sqlite", null, 0)]
    [InlineData("bing.sqlite", null, 1)]
    [InlineData("bing.sqlite", null, -1)]
    [InlineData("bing.sqlite", "Id", 3)]
    [InlineData("bing.sqlite", "missing", 3)]
    [InlineData("custom", null, 3)]
    public void Generate_UnsupportedBatchDeleteShape_ShouldReturnNull(string provider, string property, int rows)
    {
        var factory = GetMutationShapeFactory(GenerateBatchDeleteModel());

        Assert.Null(factory(provider, SqlOperationKind.Delete, property, rows));
    }

    /// <summary>单行仍使用默认 CRUD SQL，形状工厂只负责多行删除。</summary>
    [Theory]
    [InlineData("bing.sqlite", "Delete From `rows` Where `row_id` = @_p_0")]
    [InlineData("bing.mysql", "Delete From `rows` Where `row_id` = @_p_0")]
    [InlineData("bing.postgresql", "Delete From \"rows\" Where \"row_id\" = @_p_0")]
    [InlineData("bing.sqlserver", "Delete From [rows] Where [row_id] = @_p_0")]
    [InlineData("bing.oracle", "Delete From \"rows\" Where \"row_id\" = :p_0")]
    public void Generate_SingleDelete_ShouldUseDefaultMutationSql(string provider, string expected)
    {
        var model = GenerateBatchDeleteModel();

        Assert.Null(GetMutationShapeFactory(model)(provider, SqlOperationKind.Delete, null, 1));
        Assert.Equal(expected, GetMutationFactory(model)(provider, SqlOperationKind.Delete));
    }

    /// <summary>没有主键的模型仍不生成批量删除形状。</summary>
    [Fact]
    public void Generate_DeleteModelWithoutKey_ShouldReturnNull()
    {
        var model = GenerateBatchDeleteModel("public string Name {get;set;}");

        Assert.Null(GetMutationShapeFactory(model)("bing.sqlite", SqlOperationKind.Delete, null, 3));
    }

    private static EntityModelMetadata GenerateBatchDeleteModel(
        string keyProperties = "[Key, Column(\"row_id\")] public int Id {get;set;}",
        string otherProperties = "", string schema = null)
    {
        var schemaArgument = schema == null ? string.Empty : ", Schema=\"" + schema + "\"";
        var result = RunGenerator("using Bing.Data.Sql.Generated; " +
            "using System.ComponentModel.DataAnnotations; " +
            "using System.ComponentModel.DataAnnotations.Schema; [Table(\"rows\"" + schemaArgument + ")] " +
            "public sealed class Row { " + keyProperties + " " + otherProperties + " } " +
            "[SqlModel(typeof(Row))] public partial class Context {}");
        AssertNoCompilationErrors(result);
        return Assert.Single((EntityModelMetadata[])Assembly.Load(result.AssemblyImage)
            .GetType("Context")!.GetMethod("CreateModels")!.Invoke(null, null)!);
    }

    private static string ExpectedDeleteSql(string open, string close, string parameterPrefix, int rows,
        string schema = null)
    {
        var table = open + "rows" + close;
        if (schema != null)
            table = open + schema + close + "." + table;
        var parameters = string.Join(",", Enumerable.Range(0, rows).Select(index => parameterPrefix + index));
        return "Delete From " + table + " Where " + open + "row_id" + close + " In (" + parameters + ")";
    }
}
