using Bing.Dapper.Tests.Samples;
using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试 - Join 子句
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// 测试 - 内连接
    /// </summary>
    [Fact]
    public void Test_Join_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select `a3`.`a`,`a1`.`b1`,`a2`.`b2` ");
        result.AppendLine("From `b` As `a2` ");
        result.Append("Join `t.c` As `a3` On `a2`.`d`=@_p_0");

        //执行
        _builder.Select("a,a1.b1,a2.b2", "a3")
            .From("b", "a2")
            .Join("t.c", "a3").On("a2.d", "e");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试 - 普通 Inner Join 的 On 条件应绑定到最后一个 Join 并生成单个参数。
    /// </summary>
    [Fact]
    public void Join_WhenNormalTableValueOnConfigured_ShouldRenderSqlAndParameter()
    {
        // Arrange
        const string expected = "Select `o`.`Id`,`i`.`Id` \r\nFrom `orders` As `o` \r\nJoin `order_items` As `i` On `i`.`OrderId`=@_p_0";

        // Act
        var sql = _builder.Select("o.Id,i.Id").From("orders", "o")
            .Join("order_items", "i").On("i.OrderId", 42).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Single(_builder.GetParams());
        Assert.Equal(42, _builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试目的：AppendJoin 不应将调用方指定的方括号转换为 MySQL 标识符引号。
    /// </summary>
    [Fact]
    public void AppendJoin_ShouldNotApplyDialectFormatting()
    {
        _builder.Select("a")
            .From("source")
            .AppendJoin("[archive].[Order.Log2025] As raw_order");

        Assert.Equal("Select `a` \r\nFrom `source` \r\nJoin [archive].[Order.Log2025] As raw_order",
            _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：MySQL 的各类字符串 Join 都应识别反引号 schema 和带点物理表名。
    /// </summary>
    [Fact]
    public void Join_WhenUsingQuotedSchemaAndDottedPhysicalTable_ShouldRenderAllJoinTypes()
    {
        var sql = _builder.Select("o.Id")
            .From("Orders", "o")
            .Join("`archive_db`.`Merchants.Company`", "merchant")
            .LeftJoin("`archive_db`.`Order.Log2025`", "audit")
            .RightJoin("`archive_db`.`Payment.Record`", "payment")
            .ToSql();

        Assert.Equal("Select `o`.`Id` \r\nFrom `Orders` As `o` \r\nJoin `archive_db`.`Merchants.Company` As `merchant` \r\nLeft Join `archive_db`.`Order.Log2025` As `audit` \r\nRight Join `archive_db`.`Payment.Record` As `payment`", sql);
    }

    /// <summary>
    /// 测试目的：克隆后的 MySQL Join 子句必须继续将带点物理表名作为原子标识符。
    /// </summary>
    [Fact]
    public void Clone_WhenJoinUsesDottedPhysicalTable_ShouldPreserveMySqlStringTableStrategy()
    {
        _builder.Select("*").From("Orders").Join("Order.Log2025", "audit");

        var clone = _builder.Clone();
        clone.Join("Audit.Log2026", "history");

        Assert.Equal("Select * \r\nFrom `Orders` \r\nJoin `Order.Log2025` As `audit` \r\nJoin `Audit.Log2026` As `history`",
            clone.ToSql());
    }

    /// <summary>
    /// 测试目的：MySQL Join 必须拒绝 schema 与物理表名混合使用反引号的字符串引用。
    /// </summary>
    [Theory]
    [InlineData("`archive_db`.orders")]
    [InlineData("archive_db.`orders`")]
    [InlineData("`archive_db`.Merchants.Company")]
    [InlineData("archive_db.`Merchants.Company`")]
    public void Join_WhenTableNameUsesMixedQuotes_ShouldThrowArgumentException(string table)
    {
        Assert.Throws<ArgumentException>(() => _builder.Select("o.Id").From("Orders", "o").Join(table, "merchant"));
    }

    /// <summary>
    /// 测试 - 连接条件 - 属性表达式
    /// </summary>
    [Fact]
    public void Test_On_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select `a` ");
        result.AppendLine("From `Sample` As `b` ");
        result.Append("Join `Sample2` As `c` On `b`.`IntValue`<>`c`.`IntValue`");

        //执行
        _builder.Select("a")
            .From<Sample>("b")
            .Join<Sample2>("c").On<Sample, Sample2>(t => t.IntValue, t => t.IntValue, Operator.NotEqual);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }
}
