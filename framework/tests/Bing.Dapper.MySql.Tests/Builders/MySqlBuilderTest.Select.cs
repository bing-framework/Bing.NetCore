using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试 - Select 子句
/// </summary>
public partial class MySqlBuilderTest
{
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
