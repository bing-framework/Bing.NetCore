using System;
using System.Globalization;
using Bing.Data.Sql.Builders;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlModelGeneratorTest
{
    /// <summary>多行筛选插入按传入列顺序生成连续的行优先参数序号。</summary>
    [Theory]
    [InlineData("bing.sqlite", 2,
        "Insert Into `selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData("bing.sqlite", 3,
        "Insert Into `selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)")]
    [InlineData("bing.mysql", 2,
        "Insert Into `selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData("bing.mysql", 3,
        "Insert Into `selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)")]
    [InlineData("bing.postgresql", 2,
        "Insert Into \"selected_rows\" (\"unit_price\", \"display_name\") Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData("bing.postgresql", 3,
        "Insert Into \"selected_rows\" (\"unit_price\", \"display_name\") Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)")]
    [InlineData("bing.sqlserver", 2,
        "Insert Into [selected_rows] ([unit_price], [display_name]) Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData("bing.sqlserver", 3,
        "Insert Into [selected_rows] ([unit_price], [display_name]) Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5)")]
    public void Generate_SelectedMutationColumnsSql_ShouldRenderMultiRowInsert(string provider, int rows,
        string expected)
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());

        Assert.Equal(expected, factory(provider, SqlOperationKind.InsertValues,
            new[] { "Price", "Name" }, rows, false));
    }

    /// <summary>多列子集在支持静态 Schema 的非 SQLite Provider 上保留完整名称。</summary>
    [Theory]
    [InlineData("bing.mysql",
        "Insert Into `sales_2026`.`selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1)")]
    [InlineData("bing.postgresql",
        "Insert Into \"sales_2026\".\"selected_rows\" (\"unit_price\", \"display_name\") Values (@_p_0, @_p_1)")]
    [InlineData("bing.sqlserver",
        "Insert Into [sales_2026].[selected_rows] ([unit_price], [display_name]) Values (@_p_0, @_p_1)")]
    [InlineData("bing.oracle",
        "Insert Into \"sales_2026\".\"selected_rows\" (\"unit_price\", \"display_name\") Values (:p_0, :p_1)")]
    public void Generate_SelectedMutationColumnsSql_ShouldRenderSchemaQualifiedInsert(string provider,
        string expected)
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel(schema: "sales_2026"));

        Assert.Equal(expected, factory(provider, SqlOperationKind.InsertValues,
            new[] { "Price", "Name" }, 1, false));
    }

    /// <summary>Oracle 保留单行多列生成，并明确拒绝未支持的多行形状。</summary>
    [Fact]
    public void Generate_SelectedMutationColumnsSql_ShouldKeepOracleSingleRowAndDeclineMultiRow()
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());

        Assert.Equal("Insert Into \"selected_rows\" (\"unit_price\", \"display_name\") Values (:p_0, :p_1)",
            factory("bing.oracle", SqlOperationKind.InsertValues, new[] { "Price", "Name" }, 1, false));
        Assert.Null(factory("bing.oracle", SqlOperationKind.InsertValues, new[] { "Price", "Name" }, 2, false));
    }

    /// <summary>仅正数行数的 InsertValues 可使用批量回传生成 SQL，其他形状回退。</summary>
    [Theory]
    [InlineData(SqlOperationKind.InsertValues, 0)]
    [InlineData(SqlOperationKind.InsertValues, -1)]
    [InlineData(SqlOperationKind.Update, 2)]
    [InlineData(SqlOperationKind.Delete, 1)]
    [InlineData(SqlOperationKind.Select, 2)]
    public void Generate_SelectedMutationColumnsSql_ShouldDeclineUnsupportedRowsAndOperations(
        SqlOperationKind operation, int rows)
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());

        Assert.Null(factory("bing.sqlite", operation, new[] { "Price", "Name" }, rows, false));
    }

    /// <summary>多行 Returning 保留全实体字段及各 Provider 的子句位置。</summary>
    [Theory]
    [InlineData("bing.sqlite",
        "Insert Into `selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1), (@_p_2, @_p_3) Returning `row_id` As `Id`, `display_name` As `Name`, `unit_price` As `Price`, `stock_code` As `Code`, `calculated_value` As `Calculated`, `row_version` As `Version`")]
    [InlineData("bing.postgresql",
        "Insert Into \"selected_rows\" (\"unit_price\", \"display_name\") Values (@_p_0, @_p_1), (@_p_2, @_p_3) Returning \"row_id\" As \"Id\", \"display_name\" As \"Name\", \"unit_price\" As \"Price\", \"stock_code\" As \"Code\", \"calculated_value\" As \"Calculated\", \"row_version\" As \"Version\"")]
    [InlineData("bing.sqlserver",
        "Insert Into [selected_rows] ([unit_price], [display_name]) Output [Inserted].[row_id] As [Id], [Inserted].[display_name] As [Name], [Inserted].[unit_price] As [Price], [Inserted].[stock_code] As [Code], [Inserted].[calculated_value] As [Calculated], [Inserted].[row_version] As [Version] Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    public void Generate_SelectedMutationColumnsSql_ShouldRenderMultiRowReturning(string provider, string expected)
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());

        Assert.Equal(expected, factory(provider, SqlOperationKind.InsertValues,
            new[] { "Price", "Name" }, 2, true));
    }

    /// <summary>批量参数序号在当前区域性变化时仍使用固定的 ASCII 数字格式。</summary>
    [Fact]
    public void Generate_SelectedMutationColumnsSql_ShouldFormatRowMajorIndexesInvariantly()
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Assert.Equal(
                "Insert Into `selected_rows` (`unit_price`, `display_name`) Values (@_p_0, @_p_1), (@_p_2, @_p_3), (@_p_4, @_p_5), (@_p_6, @_p_7), (@_p_8, @_p_9), (@_p_10, @_p_11)",
                factory("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Price", "Name" }, 6, false));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>批量 SQL 工厂拒绝未知、Identity 和 Computed 写列。</summary>
    [Fact]
    public void Generate_SelectedMutationColumnsSql_ShouldDeclineUnknownAndNonWritableColumns()
    {
        var factory = GetSelectedColumnsFactoryForTest(GenerateSelectedColumnsModel());

        Assert.Null(factory("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Name", "Missing" }, 2, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Id" }, 2, false));
        Assert.Null(factory("bing.sqlite", SqlOperationKind.InsertValues, new[] { "Calculated" }, 2, false));
    }
}
