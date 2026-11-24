using System.Reflection;
using System.Linq;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    /// <summary>复合主键批量删除为每个 Provider 生成实体内配对的完整 OR 条件。</summary>
    [Theory]
    [InlineData("bing.sqlite", "`", "`", "@_p_")]
    [InlineData("bing.mysql", "`", "`", "@_p_")]
    [InlineData("bing.postgresql", "\"", "\"", "@_p_")]
    [InlineData("bing.sqlserver", "[", "]", "@_p_")]
    [InlineData("bing.oracle", "\"", "\"", ":p_")]
    public void Generate_PairedDeleteWithCompositeKeys_ShouldReturnCompleteSql(string provider, string open,
        string close, string parameterPrefix)
    {
        var model = GeneratePairedDeleteModel(
            "[Key, Column(\"tenant_id\")] public int TenantId {get;set;} " +
            "[Key, Column(\"row_id\")] public int Id {get;set;}");

        AssertPairedDeleteSql(model, provider, open, close, parameterPrefix, new[] { "tenant_id", "row_id" });
    }

    /// <summary>单主键并发删除按每个实体的键与令牌生成完整配对条件。</summary>
    [Theory]
    [InlineData("bing.sqlite", "`", "`", "@_p_")]
    [InlineData("bing.mysql", "`", "`", "@_p_")]
    [InlineData("bing.postgresql", "\"", "\"", "@_p_")]
    [InlineData("bing.sqlserver", "[", "]", "@_p_")]
    [InlineData("bing.oracle", "\"", "\"", ":p_")]
    public void Generate_PairedDeleteWithSingleKeyAndConcurrency_ShouldReturnCompleteSql(string provider,
        string open, string close, string parameterPrefix)
    {
        var model = GeneratePairedDeleteModel(
            "[Key, Column(\"row_id\")] public int Id {get;set;}",
            "[ConcurrencyCheck, Column(\"version_no\")] public int Version {get;set;}");

        AssertPairedDeleteSql(model, provider, open, close, parameterPrefix,
            new[] { "row_id", "version_no" });
    }

    /// <summary>复合主键与并发令牌按映射列顺序生成实体级完整配对条件。</summary>
    [Theory]
    [InlineData("bing.sqlite", "`", "`", "@_p_")]
    [InlineData("bing.mysql", "`", "`", "@_p_")]
    [InlineData("bing.postgresql", "\"", "\"", "@_p_")]
    [InlineData("bing.sqlserver", "[", "]", "@_p_")]
    [InlineData("bing.oracle", "\"", "\"", ":p_")]
    public void Generate_PairedDeleteWithCompositeKeysAndConcurrency_ShouldReturnCompleteSql(string provider,
        string open, string close, string parameterPrefix)
    {
        var model = GeneratePairedDeleteModel(
            "[Key, Column(\"tenant_id\")] public int TenantId {get;set;} " +
            "[Key, Column(\"row_id\")] public int Id {get;set;}",
            "[ConcurrencyCheck, Column(\"version_no\")] public int Version {get;set;}");

        AssertPairedDeleteSql(model, provider, open, close, parameterPrefix,
            new[] { "tenant_id", "row_id", "version_no" });
    }

    /// <summary>被标记为键和并发令牌的同一列按 Builder 现有顺序参与两次条件绑定。</summary>
    [Theory]
    [InlineData("bing.sqlite", "`", "`", "@_p_")]
    [InlineData("bing.mysql", "`", "`", "@_p_")]
    [InlineData("bing.postgresql", "\"", "\"", "@_p_")]
    [InlineData("bing.sqlserver", "[", "]", "@_p_")]
    [InlineData("bing.oracle", "\"", "\"", ":p_")]
    public void Generate_PairedDeleteWhenKeyIsAlsoConcurrency_ShouldKeepBothConditions(string provider,
        string open, string close, string parameterPrefix)
    {
        var model = GeneratePairedDeleteModel(
            "[Key, ConcurrencyCheck, Column(\"row_id\")] public int Id {get;set;}");

        AssertPairedDeleteSql(model, provider, open, close, parameterPrefix,
            new[] { "row_id", "row_id" });
    }

    /// <summary>静态 Schema 的配对删除保留 Provider 限定名，SQLite 继续拒绝静态 Schema SQL。</summary>
    [Theory]
    [InlineData("bing.sqlite", "`", "`", "@_p_")]
    [InlineData("bing.mysql", "`", "`", "@_p_")]
    [InlineData("bing.postgresql", "\"", "\"", "@_p_")]
    [InlineData("bing.sqlserver", "[", "]", "@_p_")]
    [InlineData("bing.oracle", "\"", "\"", ":p_")]
    public void Generate_SchemaPairedDelete_ShouldReturnQualifiedSqlOrDecline(string provider, string open,
        string close, string parameterPrefix)
    {
        var model = GeneratePairedDeleteModel(
            "[Key, Column(\"tenant_id\")] public int TenantId {get;set;} " +
            "[Key, Column(\"row_id\")] public int Id {get;set;}",
            "[ConcurrencyCheck, Column(\"version_no\")] public int Version {get;set;}", "sales_2026");
        var factory = GetMutationShapeFactory(model);

        if (provider == "bing.sqlite")
        {
            Assert.Null(factory(provider, SqlOperationKind.Delete, null, 3));
            return;
        }

        Assert.Equal(ExpectedPairedDeleteSql(open, close, parameterPrefix, 3,
                new[] { "tenant_id", "row_id", "version_no" }, "sales_2026"),
            factory(provider, SqlOperationKind.Delete, null, 3));
    }

    /// <summary>只有多行走配对形状；单行仍由默认 Delete SQL 工厂处理复合键与令牌。</summary>
    [Fact]
    public void Generate_SinglePairedDelete_ShouldKeepDefaultMutationSqlFactory()
    {
        var model = GeneratePairedDeleteModel(
            "[Key, Column(\"tenant_id\")] public int TenantId {get;set;} " +
            "[Key, Column(\"row_id\")] public int Id {get;set;}",
            "[ConcurrencyCheck, Column(\"version_no\")] public int Version {get;set;}");

        Assert.Null(GetMutationShapeFactory(model)("bing.sqlite", SqlOperationKind.Delete, null, 1));
        Assert.Equal("Delete From `rows` Where `tenant_id` = @_p_0 And `row_id` = @_p_1 And `version_no` = @_p_2",
            GetMutationFactory(model)("bing.sqlite", SqlOperationKind.Delete));
    }

    private static EntityModelMetadata GeneratePairedDeleteModel(string keyProperties, string otherProperties = "",
        string schema = null)
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

    private static void AssertPairedDeleteSql(EntityModelMetadata model, string provider, string open,
        string close, string parameterPrefix, string[] columns)
    {
        var factory = GetMutationShapeFactory(model);
        Assert.Equal(ExpectedPairedDeleteSql(open, close, parameterPrefix, 3, columns),
            factory(provider, SqlOperationKind.Delete, null, 3));
        Assert.Equal(ExpectedPairedDeleteSql(open, close, parameterPrefix, 12, columns),
            factory(provider, SqlOperationKind.Delete, null, 12));
    }

    private static string ExpectedPairedDeleteSql(string open, string close, string parameterPrefix, int rows,
        params string[] columns) => ExpectedPairedDeleteSql(open, close, parameterPrefix, rows, columns, null);

    private static string ExpectedPairedDeleteSql(string open, string close, string parameterPrefix, int rows,
        string[] columns, string schema)
    {
        var table = open + "rows" + close;
        if (schema != null)
            table = open + schema + close + "." + table;
        var entities = Enumerable.Range(0, rows).Select(row => "(" + string.Join(" And ",
            columns.Select((column, index) => open + column + close + " = " + parameterPrefix +
                (row * columns.Length + index))) + ")").ToArray();
        var condition = entities[0];
        for (var index = 1; index < entities.Length; index++)
            condition = "(" + condition + " Or " + entities[index] + ")";
        return "Delete From " + table + " Where " + condition;
    }
}
