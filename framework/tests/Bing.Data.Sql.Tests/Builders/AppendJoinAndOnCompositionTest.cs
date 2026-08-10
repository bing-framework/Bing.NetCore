using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Append Join 与 On 组合测试。
/// </summary>
public class AppendJoinAndOnCompositionTest
{
    /// <summary>
    /// 测试 - 原始内连接后追加 On 条件应组合为有效 SQL。
    /// </summary>
    [Fact]
    public void AppendJoin_WhenFollowedByAppendOn_ShouldComposeSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.Select("o.Id")
            .AppendFrom("Orders o")
            .AppendJoin("Items i")
            .AppendOn("i.OrderId=o.Id")
            .ToSql();

        // Assert
        Assert.Equal("Select [o].[Id] \r\nFrom Orders o \r\nJoin Items i On i.OrderId=o.Id", sql);
    }

    /// <summary>
    /// 测试 - 原始左连接后追加 On 条件应组合为有效 SQL。
    /// </summary>
    [Fact]
    public void AppendLeftJoin_WhenFollowedByAppendOn_ShouldComposeSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendLeftJoin("Items i")
            .AppendOn("i.OrderId=o.Id")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nLeft Join Items i On i.OrderId=o.Id", sql);
    }

    /// <summary>
    /// 测试 - 原始右连接后追加 On 条件应组合为有效 SQL。
    /// </summary>
    [Fact]
    public void AppendRightJoin_WhenFollowedByAppendOn_ShouldComposeSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendRightJoin("Items i")
            .AppendOn("i.OrderId=o.Id")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nRight Join Items i On i.OrderId=o.Id", sql);
    }

    /// <summary>
    /// 测试 - 多个原始连接的 On 条件应仅绑定到最近添加的连接。
    /// </summary>
    [Fact]
    public void AppendJoin_WhenMultipleJoinsExist_ShouldBindOnToLatestJoin()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendJoin("Items i")
            .AppendOn("i.OrderId=o.Id")
            .AppendLeftJoin("Products p")
            .AppendOn("p.Id=i.ProductId")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nJoin Items i On i.OrderId=o.Id \r\nLeft Join Products p On p.Id=i.ProductId", sql);
    }

    /// <summary>
    /// 测试 - 原始连接自带 On 后继续追加 On 条件应追加到同一连接。
    /// </summary>
    [Fact]
    public void AppendJoin_WhenJoinAlreadyContainsOn_ShouldAppendAdditionalCondition()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendJoin("Items i On i.OrderId=o.Id")
            .AppendOn("i.IsActive=1")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nJoin Items i On i.OrderId=o.Id And i.IsActive=1", sql);
    }

    /// <summary>
    /// 测试目的：原始 Join 的 ON 使用换行分隔时，追加条件应识别既有 ON 并使用 And。
    /// </summary>
    [Fact]
    public void AppendJoin_WhenExistingOnUsesNewline_ShouldAppendAndCondition()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendJoin("Items i\nOn i.OrderId=o.Id")
            .AppendOn("i.IsActive=1")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nJoin Items i\nOn i.OrderId=o.Id And i.IsActive=1", sql);
    }

    /// <summary>
    /// 测试目的：注释中的 ON 不得被识别为原始 Join 的连接条件。
    /// </summary>
    [Fact]
    public void AppendJoin_WhenCommentContainsOn_ShouldAppendOnCondition()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendJoin("Items i /* On is documentation only */")
            .AppendOn("i.OrderId=o.Id")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nJoin Items i /* On is documentation only */ On i.OrderId=o.Id", sql);
    }

    /// <summary>
    /// 测试 - 前一个原始连接已有 On 时，后续 AppendOn 应绑定到最新连接。
    /// </summary>
    [Fact]
    public void AppendJoin_WhenPreviousJoinContainsOn_ShouldBindAppendOnToLatestJoin()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendJoin("Items i On i.OrderId=o.Id")
            .AppendLeftJoin("Products p")
            .AppendOn("p.Id=i.ProductId")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nJoin Items i On i.OrderId=o.Id \r\nLeft Join Products p On p.Id=i.ProductId", sql);
    }

    /// <summary>
    /// 测试 - 原始连接后的结构化 On 值条件应绑定到最后一个连接。
    /// </summary>
    [Fact]
    public void AppendLeftJoin_WhenFollowedByOnValue_ShouldAddParameterToLatestJoin()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendLeftJoin("Items i")
            .On("i.Enabled", true)
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nLeft Join Items i On [i].[Enabled]=@_p_0", sql);
        Assert.Equal(new[] { "@_p_0" }, builder.GetParams().Keys);
        Assert.True((bool)builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - 原始连接后使用结构化 On 值条件应生成参数化 SQL。
    /// </summary>
    [Fact]
    public void AppendJoin_WhenFollowedByOnValue_ShouldAddParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendJoin("Items i")
            .On("i.TenantId", "tenant")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nJoin Items i On [i].[TenantId]=@_p_0", sql);
        Assert.Equal(new[] { "@_p_0" }, builder.GetParams().Keys);
        Assert.Equal("tenant", builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - 没有连接时追加非空 On 条件应抛出异常且不会延迟应用到后续连接。
    /// </summary>
    [Fact]
    public void AppendOn_WhenNoJoinExists_ShouldThrowWithoutChangingFollowingJoin()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.AppendFrom("Orders o");
        var exception = Assert.Throws<InvalidOperationException>(() => builder.AppendOn("o.Id=i.OrderId"));
        var sql = builder.AppendJoin("Items i").ToSql();

        // Assert
        Assert.Equal("当前不存在可追加 On 条件的 Join。", exception.Message);
        Assert.Equal("Select * \r\nFrom Orders o \r\nJoin Items i", sql);
    }

    /// <summary>
    /// 测试 - 没有连接时追加空白 On 条件应保持无操作。
    /// </summary>
    [Fact]
    public void AppendOn_WhenNoJoinExistsAndSqlIsWhitespace_ShouldDoNothing()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendOn("  ")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o", sql);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>
    /// 测试 - 没有连接时结构化值 On 条件应先抛出异常且不创建参数。
    /// </summary>
    [Fact]
    public void OnValue_WhenNoJoinExists_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.On("o.Enabled", true));

        // Assert
        Assert.Equal("当前不存在可追加 On 条件的 Join。", exception.Message);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>
    /// 测试 - 没有连接时表达式 On 条件应先抛出异常且不创建参数。
    /// </summary>
    [Fact]
    public void OnExpression_WhenNoJoinExists_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.On<Sample, Sample2>((left, right) => left.IntValue == right.IntValue));

        // Assert
        Assert.Equal("当前不存在可追加 On 条件的 Join。", exception.Message);
        Assert.Empty(builder.GetParams());
    }
}