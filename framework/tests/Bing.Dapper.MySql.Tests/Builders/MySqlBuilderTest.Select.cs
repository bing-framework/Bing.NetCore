using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试 - Select 子句
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// 测试 - 限定列 Count 应分别引用表别名和列名。
    /// </summary>
    [Theory]
    [InlineData("u.Id")]
    [InlineData("\"u\".\"Id\"")]
    [InlineData("`u`.`Id`")]
    public void Count_WithQualifiedColumn_ShouldFormatEachIdentifierSegment(string column)
    {
        // Arrange
        const string expected = "Select Count(`u`.`Id`) As `Count` \r\nFrom `users` As `u`";

        // Act
        var sql = _builder.Count(column, "Count").From("users", "u").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 限定列 Sum 应分别引用表别名和列名。
    /// </summary>
    [Theory]
    [InlineData("u.Amount")]
    [InlineData("\"u\".\"Amount\"")]
    [InlineData("`u`.`Amount`")]
    public void Sum_WithQualifiedColumn_ShouldFormatEachIdentifierSegment(string column)
    {
        // Arrange
        const string expected = "Select Sum(`u`.`Amount`) As `Total` \r\nFrom `users` As `u`";

        // Act
        var sql = _builder.Sum(column, "Total").From("users", "u").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - 克隆后的限定列 Count 应保留聚合函数与分段引用。
    /// </summary>
    [Fact]
    public void Clone_WhenCountUsesQualifiedColumn_ShouldPreserveAggregation()
    {
        // Arrange
        const string expected = "Select Count(`u`.`Id`) As `Count` \r\nFrom `users` As `u`";
        _builder.Count("u.Id", "Count").From("users", "u");

        // Act
        var sql = _builder.Clone().ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 查询
    /// </summary>
    [Fact]
    public void Test_Select_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select `a` ");
        result.Append("From `t`");

        //执行
        _builder.Select("a").From("t");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：带点物理表名必须作为单个 MySQL 标识符渲染。
    /// </summary>
    [Fact]
    public void Test_Select_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select `c` ");
        result.Append("From `Order.Log2025`");

        //执行
        _builder.Select("c").From("Order.Log2025");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：AppendFrom 不应将调用方指定的方括号转换为 MySQL 标识符引号。
    /// </summary>
    [Fact]
    public void AppendFrom_ShouldNotApplyDialectFormatting()
    {
        _builder.Select("c").AppendFrom("[archive].[Order.Log2025] As raw_order");

        Assert.Equal("Select `c` \r\nFrom [archive].[Order.Log2025] As raw_order", _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：MySQL 带点物理表名必须作为一个标识符渲染。
    /// </summary>
    [Fact]
    public void MySqlDottedPhysicalTableNameTest()
    {
        Assert.Equal("Select `c` \r\nFrom `Merchants.Company`", _builder.Select("c").From("Merchants.Company").ToSql());
    }

    /// <summary>
    /// 测试目的：MySQL schema 与带点物理表名必须分别渲染。
    /// </summary>
    [Fact]
    public void MySqlSchemaAndDottedTableTest()
    {
        Assert.Equal("Select `c` \r\nFrom `archive_db`.`Merchants.Company`",
            _builder.Select("c").From("`archive_db`.`Merchants.Company`").ToSql());
    }

    /// <summary>
    /// 测试目的：反引号包围的物理表名中的句点必须保持为单个 MySQL 标识符。
    /// </summary>
    [Fact]
    public void From_WhenQuotedPhysicalTableContainsDot_ShouldPreserveAtomicName()
    {
        Assert.Equal("Select `c` \r\nFrom `Merchants.Company` As `merchant`",
            _builder.Select("c").From("`Merchants.Company`", "merchant").ToSql());
    }

    /// <summary>
    /// 测试目的：MySQL 反引号表名的非法结构必须在生成 SQL 前被拒绝。
    /// </summary>
    [Theory]
    [InlineData("`archive_db`.`Merchants.Company")]
    [InlineData("`archive_db`..`Merchants.Company`")]
    [InlineData("`archive_db`.`Merchants.Company`;Drop Table Users")]
    [InlineData("`archive_db`.orders")]
    [InlineData("archive_db.`orders`")]
    [InlineData("`archive_db`.Merchants.Company")]
    [InlineData("archive_db.`Merchants.Company`")]
    public void From_WhenQuotedTableNameIsInvalid_ShouldThrowArgumentException(string table)
    {
        Assert.Throws<ArgumentException>(() => _builder.Select("c").From(table));
    }
}
