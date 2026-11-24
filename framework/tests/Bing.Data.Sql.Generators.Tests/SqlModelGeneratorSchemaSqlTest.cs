using System;
using System.Reflection;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    /// <summary>静态 Schema 的完整 CRUD SQL 必须使用对应 Provider 的限定表名。</summary>
    [Theory]
    [InlineData("bing.mysql", SqlOperationKind.InsertValues,
        "Insert Into `sales_2026`.`schema_rows` (`display_name`, `Version`) Values (@_p_0, @_p_1)")]
    [InlineData("bing.mysql", SqlOperationKind.Update,
        "Update `sales_2026`.`schema_rows` Set `display_name` = @_p_0, `Version` = @_p_1 Where `row_id` = @_p_2 And `Version` = @_p_3")]
    [InlineData("bing.mysql", SqlOperationKind.Delete,
        "Delete From `sales_2026`.`schema_rows` Where `row_id` = @_p_0 And `Version` = @_p_1")]
    [InlineData("bing.postgresql", SqlOperationKind.InsertValues,
        "Insert Into \"sales_2026\".\"schema_rows\" (\"display_name\", \"Version\") Values (@_p_0, @_p_1)")]
    [InlineData("bing.postgresql", SqlOperationKind.Update,
        "Update \"sales_2026\".\"schema_rows\" Set \"display_name\" = @_p_0, \"Version\" = @_p_1 Where \"row_id\" = @_p_2 And \"Version\" = @_p_3")]
    [InlineData("bing.postgresql", SqlOperationKind.Delete,
        "Delete From \"sales_2026\".\"schema_rows\" Where \"row_id\" = @_p_0 And \"Version\" = @_p_1")]
    [InlineData("bing.sqlserver", SqlOperationKind.InsertValues,
        "Insert Into [sales_2026].[schema_rows] ([display_name], [Version]) Values (@_p_0, @_p_1)")]
    [InlineData("bing.sqlserver", SqlOperationKind.Update,
        "Update [sales_2026].[schema_rows] Set [display_name] = @_p_0, [Version] = @_p_1 Where [row_id] = @_p_2 And [Version] = @_p_3")]
    [InlineData("bing.sqlserver", SqlOperationKind.Delete,
        "Delete From [sales_2026].[schema_rows] Where [row_id] = @_p_0 And [Version] = @_p_1")]
    [InlineData("bing.oracle", SqlOperationKind.InsertValues,
        "Insert Into \"sales_2026\".\"schema_rows\" (\"display_name\", \"Version\") Values (:p_0, :p_1)")]
    [InlineData("bing.oracle", SqlOperationKind.Update,
        "Update \"sales_2026\".\"schema_rows\" Set \"display_name\" = :p_0, \"Version\" = :p_1 Where \"row_id\" = :p_2 And \"Version\" = :p_3")]
    [InlineData("bing.oracle", SqlOperationKind.Delete,
        "Delete From \"sales_2026\".\"schema_rows\" Where \"row_id\" = :p_0 And \"Version\" = :p_1")]
    public void Generate_SchemaMutationSql_ShouldReturnCompleteProviderStatement(string provider,
        SqlOperationKind operation, string expected)
    {
        var model = GenerateSchemaModel();
        var factory = GetMutationFactory(model);

        Assert.Equal(expected, factory(provider, operation));
    }

    /// <summary>静态 Schema 下的 Identity Insert 保留 Provider 返回键语法及 key-only 形状。</summary>
    [Theory]
    [InlineData("bing.mysql", false,
        "Insert Into `sales_2026`.`identity_rows` (`display_name`, `Version`) Values (@_p_0, @_p_1)")]
    [InlineData("bing.mysql", true,
        "Insert Into `sales_2026`.`identity_rows` () VALUES ()")]
    [InlineData("bing.postgresql", false,
        "Insert Into \"sales_2026\".\"identity_rows\" (\"display_name\", \"Version\") Values (@_p_0, @_p_1) Returning \"row_id\"")]
    [InlineData("bing.postgresql", true,
        "Insert Into \"sales_2026\".\"identity_rows\" DEFAULT VALUES RETURNING \"row_id\"")]
    [InlineData("bing.sqlserver", false,
        "Insert Into [sales_2026].[identity_rows] ([display_name], [Version]) Output [Inserted].[row_id] Values (@_p_0, @_p_1)")]
    [InlineData("bing.sqlserver", true,
        "Insert Into [sales_2026].[identity_rows] OUTPUT INSERTED.[row_id] DEFAULT VALUES")]
    [InlineData("bing.oracle", false,
        "Insert Into \"sales_2026\".\"identity_rows\" (\"display_name\", \"Version\") Values (:p_0, :p_1) Returning \"row_id\" Into :bing_identity_key")]
    [InlineData("bing.oracle", true,
        "Insert Into \"sales_2026\".\"identity_rows\" (\"row_id\") Values (DEFAULT) Returning \"row_id\" Into :bing_identity_key")]
    public void Generate_SchemaKeyInsertSql_ShouldReturnCompleteIdentityStatement(string provider,
        bool keyOnly, string expected)
    {
        var model = GenerateSchemaModel(tableName: "identity_rows", includeValues: !keyOnly);
        var factory = GetKeyInsertFactory(model);

        Assert.Equal(expected, factory(provider));
    }

    /// <summary>有限单列 Insert/Update 与默认双行 Insert 生成限定表名的完整 SQL。</summary>
    [Theory]
    [InlineData("bing.mysql", SqlOperationKind.InsertValues, "Name", 1,
        "Insert Into `sales_2026`.`schema_rows` (`display_name`) Values (@_p_0)")]
    [InlineData("bing.mysql", SqlOperationKind.Update, "Name", 1,
        "Update `sales_2026`.`schema_rows` Set `display_name` = @_p_0 Where `row_id` = @_p_1 And `Version` = @_p_2")]
    [InlineData("bing.postgresql", SqlOperationKind.InsertValues, "Name", 1,
        "Insert Into \"sales_2026\".\"schema_rows\" (\"display_name\") Values (@_p_0)")]
    [InlineData("bing.postgresql", SqlOperationKind.Update, "Name", 1,
        "Update \"sales_2026\".\"schema_rows\" Set \"display_name\" = @_p_0 Where \"row_id\" = @_p_1 And \"Version\" = @_p_2")]
    [InlineData("bing.sqlserver", SqlOperationKind.InsertValues, "Name", 1,
        "Insert Into [sales_2026].[schema_rows] ([display_name]) Values (@_p_0)")]
    [InlineData("bing.sqlserver", SqlOperationKind.Update, "Name", 1,
        "Update [sales_2026].[schema_rows] Set [display_name] = @_p_0 Where [row_id] = @_p_1 And [Version] = @_p_2")]
    [InlineData("bing.oracle", SqlOperationKind.InsertValues, "Name", 1,
        "Insert Into \"sales_2026\".\"schema_rows\" (\"display_name\") Values (:p_0)")]
    [InlineData("bing.oracle", SqlOperationKind.Update, "Name", 1,
        "Update \"sales_2026\".\"schema_rows\" Set \"display_name\" = :p_0 Where \"row_id\" = :p_1 And \"Version\" = :p_2")]
    [InlineData("bing.mysql", SqlOperationKind.InsertValues, null, 2,
        "Insert Into `sales_2026`.`schema_rows` (`display_name`, `Version`) Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData("bing.postgresql", SqlOperationKind.InsertValues, null, 2,
        "Insert Into \"sales_2026\".\"schema_rows\" (\"display_name\", \"Version\") Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData("bing.sqlserver", SqlOperationKind.InsertValues, null, 2,
        "Insert Into [sales_2026].[schema_rows] ([display_name], [Version]) Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData("bing.oracle", SqlOperationKind.InsertValues, null, 2, null)]
    public void Generate_SchemaMutationShapeSql_ShouldReturnSupportedCompleteShape(string provider,
        SqlOperationKind operation, string property, int rows, string expected)
    {
        var model = GenerateSchemaModel();
        var factory = GetMutationShapeFactory(model);

        Assert.Equal(expected, factory(provider, operation, property, rows));
    }

    /// <summary>Schema 目标不能误生成给 SQLite 的三个静态 SQL 工厂分支。</summary>
    [Fact]
    public void Generate_SchemaFactories_ShouldReturnNullForSqlite()
    {
        var model = GenerateSchemaModel();

        Assert.Null(GetMutationFactory(model)("bing.sqlite", SqlOperationKind.InsertValues));
        Assert.Null(GetKeyInsertFactory(model)("bing.sqlite"));
        Assert.Null(GetMutationShapeFactory(model)("bing.sqlite", SqlOperationKind.InsertValues, "Name", 1));
    }

    /// <summary>无效静态 Schema 不应生成任何绕过运行时名称校验的 SQL 工厂。</summary>
    [Theory]
    [InlineData("bad;schema")]
    [InlineData("schema.name")]
    [InlineData("   ")]
    public void Generate_InvalidSchema_ShouldOmitStaticFactories(string schema)
    {
        var model = GenerateSchemaModel(schema);

        Assert.Null(GetFactoryValue(model, "GeneratedMutationSql"));
        Assert.Null(GetFactoryValue(model, "GeneratedKeyInsertSql"));
        Assert.Null(GetFactoryValue(model, "GeneratedMutationShapeSql"));
    }

    /// <summary>null 与空字符串 Schema 都沿用无 Schema 的表名。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Generate_EmptySchema_ShouldUseUnqualifiedStaticSql(string schema)
    {
        var model = GenerateSchemaModel(schema);

        Assert.Equal("Insert Into `schema_rows` (`display_name`, `Version`) Values (@_p_0, @_p_1)",
            GetMutationFactory(model)("bing.mysql", SqlOperationKind.InsertValues));
    }

    private static EntityModelMetadata GenerateSchemaModel(string schema = "sales_2026",
        string tableName = "schema_rows", bool includeValues = true)
    {
        var schemaArgument = schema == null ? string.Empty : ", Schema=\"" + schema + "\"";
        var valueProperties = includeValues
            ? "[Column(\"display_name\")] public string Name {get;set;} " +
              "[ConcurrencyCheck] public int Version {get;set;}"
            : string.Empty;
        var result = RunGenerator("using Bing.Data.Sql.Generated; " +
            "using System.ComponentModel.DataAnnotations; " +
            "using System.ComponentModel.DataAnnotations.Schema; " +
            "[Table(\"" + tableName + "\"" + schemaArgument + ")] public sealed class SchemaRow { " +
            "[Key, Column(\"row_id\"), DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id {get;set;} " +
            valueProperties + "} [SqlModel(typeof(SchemaRow))] public partial class SchemaContext {}");
        AssertNoCompilationErrors(result);
        return Assert.Single((EntityModelMetadata[])Assembly.Load(result.AssemblyImage)
            .GetType("SchemaContext")!.GetMethod("CreateModels")!.Invoke(null, null)!);
    }

    private static Func<string, SqlOperationKind, string> GetMutationFactory(EntityModelMetadata model) =>
        Assert.IsType<Func<string, SqlOperationKind, string>>(GetFactoryValue(model, "GeneratedMutationSql"));

    private static Func<string, string> GetKeyInsertFactory(EntityModelMetadata model) =>
        Assert.IsType<Func<string, string>>(GetFactoryValue(model, "GeneratedKeyInsertSql"));

    private static Func<string, SqlOperationKind, string, int, string> GetMutationShapeFactory(
        EntityModelMetadata model) => Assert.IsType<Func<string, SqlOperationKind, string, int, string>>(
        GetFactoryValue(model, "GeneratedMutationShapeSql"));

    private static object GetFactoryValue(EntityModelMetadata model, string propertyName) =>
        typeof(EntityModelMetadata).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
        .GetValue(model);
}
