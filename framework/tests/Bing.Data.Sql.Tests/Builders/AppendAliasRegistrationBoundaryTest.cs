using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// 原始 Append 别名注册边界测试。
/// </summary>
public class AppendAliasRegistrationBoundaryTest
{
    /// <summary>
    /// 测试 - 原始 raw_source 文本不应占用结构化别名。
    /// </summary>
    [Fact]
    public void AppendRawSql_WhenTextContainsRawSourceAlias_ShouldNotRegisterAlias()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.AppendFrom("(Select 1) As raw_source").AppendJoin("(Select 2) As raw_join");
        builder.Join("Orders", "raw_source");

        // Assert
        Assert.Equal("Select * \r\nFrom (Select 1) As raw_source \r\nJoin (Select 2) As raw_join \r\nJoin [Orders] As [raw_source]", builder.ToSql());
    }

    /// <summary>
    /// 测试 - 原始 a 和 b 别名不应阻止结构化 Join 使用相同别名。
    /// </summary>
    [Fact]
    public void AppendRawSql_WhenTextContainsMultipleAliases_ShouldNotRegisterAliases()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.AppendFrom("(Select 1) As a").AppendJoin("(Select 2) As b");
        builder.Join("Orders", "a").Join("OrderItems", "b");

        // Assert
        Assert.Equal("Select * \r\nFrom (Select 1) As a \r\nJoin (Select 2) As b \r\nJoin [Orders] As [a] \r\nJoin [OrderItems] As [b]", builder.ToSql());
    }

    /// <summary>
    /// 测试 - 结构化 From 和 Join 使用重复别名时应拒绝注册。
    /// </summary>
    [Fact]
    public void StructuredSql_WhenAliasDuplicates_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.From("Orders", "o");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Join("OrderItems", "o"));

        // Assert
        Assert.Equal("查询中已存在表别名 \"o\"。", exception.Message);
    }
}