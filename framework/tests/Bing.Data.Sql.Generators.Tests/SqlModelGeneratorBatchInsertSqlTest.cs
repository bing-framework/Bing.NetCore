using Bing.Data.Sql.Builders;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    [Theory]
    [InlineData("bing.sqlite", "Insert Into `rows` (`display_name`, `Version`) Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)")]
    [InlineData("bing.mysql", "Insert Into `rows` (`display_name`, `Version`) Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)")]
    [InlineData("bing.postgresql", "Insert Into \"rows\" (\"display_name\", \"Version\") Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)")]
    [InlineData("bing.sqlserver", "Insert Into [rows] ([display_name], [Version]) Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)")]
    public void Generate_BatchInsertWithDefaultColumns_ShouldReturnCompleteSql(string provider, string expected)
    {
        var factory = ShapeFactory();

        Assert.Equal(expected, factory(provider, SqlOperationKind.InsertValues, null, 3));
    }

    [Theory]
    [InlineData("bing.sqlite", "Insert Into `rows` (`display_name`) Values (@_p_0), (@_p_1)")]
    [InlineData("bing.mysql", "Insert Into `rows` (`display_name`) Values (@_p_0), (@_p_1)")]
    [InlineData("bing.postgresql", "Insert Into \"rows\" (\"display_name\") Values (@_p_0), (@_p_1)")]
    [InlineData("bing.sqlserver", "Insert Into [rows] ([display_name]) Values (@_p_0), (@_p_1)")]
    public void Generate_BatchInsertWithSingleColumn_ShouldReturnCompleteSql(string provider, string expected)
    {
        var factory = ShapeFactory();

        Assert.Equal(expected, factory(provider, SqlOperationKind.InsertValues, "Name", 2));
    }

    [Theory]
    [InlineData("bing.sqlite", 0)]
    [InlineData("bing.sqlite", -1)]
    [InlineData("custom", 3)]
    [InlineData("bing.oracle", 3)]
    public void Generate_UnsupportedBatchInsertShape_ShouldReturnNull(string provider, int rows)
    {
        var factory = ShapeFactory();

        Assert.Null(factory(provider, SqlOperationKind.InsertValues, null, rows));
    }

    [Theory]
    [InlineData("bing.postgresql", "Insert Into \"sales_2026\".\"schema_rows\" (\"display_name\", \"Version\") Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)")]
    [InlineData("bing.sqlserver", "Insert Into [sales_2026].[schema_rows] ([display_name], [Version]) Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)")]
    [InlineData("bing.sqlite", null)]
    public void Generate_SchemaBatchInsert_ShouldReturnSupportedCompleteSql(string provider, string expected)
    {
        var model = GenerateSchemaModel();
        var factory = GetMutationShapeFactory(model);

        Assert.Equal(expected, factory(provider, SqlOperationKind.InsertValues, null, 3));
    }
}
