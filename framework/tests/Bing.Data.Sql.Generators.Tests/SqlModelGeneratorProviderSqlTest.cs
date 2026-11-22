using System;
using System.Linq;
using System.Reflection;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    /// <summary>完整 SQL 包括方言标识符、复合键、并发列和参数顺序，未声明 Provider 不生成。</summary>
    [Theory]
    [InlineData("bing.mysql", SqlOperationKind.InsertValues, "Insert Into `rows` (`Tenant`, `display_name`, `Version`) Values (@_p_0, @_p_1, @_p_2)")]
    [InlineData("bing.mysql", SqlOperationKind.Update, "Update `rows` Set `display_name` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `Tenant` = @_p_3 And `Version` = @_p_4")]
    [InlineData("bing.mysql", SqlOperationKind.Delete, "Delete From `rows` Where `Id` = @_p_0 And `Tenant` = @_p_1 And `Version` = @_p_2")]
    [InlineData("bing.postgresql", SqlOperationKind.InsertValues, "Insert Into \"rows\" (\"Tenant\", \"display_name\", \"Version\") Values (@_p_0, @_p_1, @_p_2)")]
    [InlineData("bing.postgresql", SqlOperationKind.Update, "Update \"rows\" Set \"display_name\" = @_p_0, \"Version\" = @_p_1 Where \"Id\" = @_p_2 And \"Tenant\" = @_p_3 And \"Version\" = @_p_4")]
    [InlineData("bing.postgresql", SqlOperationKind.Delete, "Delete From \"rows\" Where \"Id\" = @_p_0 And \"Tenant\" = @_p_1 And \"Version\" = @_p_2")]
    [InlineData("bing.sqlserver", SqlOperationKind.InsertValues, "Insert Into [rows] ([Tenant], [display_name], [Version]) Values (@_p_0, @_p_1, @_p_2)")]
    [InlineData("bing.sqlserver", SqlOperationKind.Update, "Update [rows] Set [display_name] = @_p_0, [Version] = @_p_1 Where [Id] = @_p_2 And [Tenant] = @_p_3 And [Version] = @_p_4")]
    [InlineData("bing.sqlserver", SqlOperationKind.Delete, "Delete From [rows] Where [Id] = @_p_0 And [Tenant] = @_p_1 And [Version] = @_p_2")]
    [InlineData("bing.oracle", SqlOperationKind.InsertValues, "Insert Into \"rows\" (\"Tenant\", \"display_name\", \"Version\") Values (:p_0, :p_1, :p_2)")]
    [InlineData("bing.oracle", SqlOperationKind.Update, "Update \"rows\" Set \"display_name\" = :p_0, \"Version\" = :p_1 Where \"Id\" = :p_2 And \"Tenant\" = :p_3 And \"Version\" = :p_4")]
    [InlineData("bing.oracle", SqlOperationKind.Delete, "Delete From \"rows\" Where \"Id\" = :p_0 And \"Tenant\" = :p_1 And \"Version\" = :p_2")]
    public void Generate_ProviderMutationSql_ShouldEmitCompleteStatement(string provider, SqlOperationKind operation, string expected)
    {
        var model = GenerateProviderSqlModel("[Key] public int Tenant {get;set;} " +
            "[Column(\"display_name\")] public string Name {get;set;} [ConcurrencyCheck] public int Version {get;set;}");
        Assert.NotNull(model.GeneratedMutationSql);
        Assert.Equal(expected, model.GeneratedMutationSql(provider, operation));
        Assert.Null(model.GeneratedMutationSql("custom", operation));
        Assert.Null(model.GeneratedMutationSql(provider, SqlOperationKind.Select));
    }

    /// <summary>身份键 SQL 保留 OUTPUT 位置、DEFAULT VALUES 与 MySQL 空行语法，无隐式跨 Provider 回退。</summary>
    [Theory]
    [InlineData("bing.sqlite", false, "Insert Into `rows` (`display_name`) Values (@_p_0) Returning `Id`")]
    [InlineData("bing.sqlite", true, "Insert Into `rows` DEFAULT VALUES RETURNING `Id`")]
    [InlineData("bing.postgresql", false, "Insert Into \"rows\" (\"display_name\") Values (@_p_0) Returning \"Id\"")]
    [InlineData("bing.postgresql", true, "Insert Into \"rows\" DEFAULT VALUES RETURNING \"Id\"")]
    [InlineData("bing.sqlserver", false, "Insert Into [rows] ([display_name]) Output [Inserted].[Id] Values (@_p_0)")]
    [InlineData("bing.sqlserver", true, "Insert Into [rows] OUTPUT INSERTED.[Id] DEFAULT VALUES")]
    [InlineData("bing.mysql", false, "Insert Into `rows` (`display_name`) Values (@_p_0)")]
    [InlineData("bing.mysql", true, "Insert Into `rows` () VALUES ()")]
    public void Generate_ProviderKeyInsertSql_ShouldEmitCompleteStatement(string provider, bool keyOnly, string expected)
    {
        var model = GenerateProviderSqlModel(keyOnly ? "" : "[Column(\"display_name\")] public string Name {get;set;}");
        Assert.NotNull(model.GeneratedKeyInsertSql);
        Assert.Equal(expected, model.GeneratedKeyInsertSql(provider));
        Assert.Null(model.GeneratedKeyInsertSql("custom"));
    }

    private static EntityModelMetadata GenerateProviderSqlModel(string members)
    {
        var result = RunGenerator("using Bing.Data.Sql.Generated; using System.ComponentModel.DataAnnotations; " +
            "using System.ComponentModel.DataAnnotations.Schema; [Table(\"rows\")] public class Row { " +
            "[Key,DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id {get;set;} " + members +
            " [DatabaseGenerated(DatabaseGeneratedOption.Computed)] public int Total {get;set;} " +
            "[NotMapped] public string Local {get;set;} } [SqlModel(typeof(Row))] public partial class Context {}");
        AssertNoCompilationErrors(result);
        return Assert.Single((EntityModelMetadata[])Assembly.Load(result.AssemblyImage)
            .GetType("Context")!.GetMethod("CreateModels")!.Invoke(null, null)!);
    }
}
