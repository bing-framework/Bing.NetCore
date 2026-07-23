using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// 原始 Append SQL 合同测试。
/// </summary>
public class AppendRawSqlContractTest
{
    /// <summary>
    /// 测试 - 原始 From 和三种 Join 应保留调用方提供的完整文本。
    /// </summary>
    [Fact]
    public void AppendRawSql_ShouldPreserveTextAcrossFromAndJoinVariants()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.Select("o.Id")
            .AppendFrom("Orders o WITH (INDEX(IX_Orders)) /* raw */")
            .AppendFrom(", [Audit.Log] a /* @tenant */")
            .AppendJoin("(Select 1 As Id) As j On j.Id=o.Id /* ? {0} */")
            .AppendLeftJoin("`Archive.Log` l On l.OrderId=o.Id")
            .AppendRightJoin("\"Payments\" p On p.OrderId=o.Id")
            .Where("o.TenantId", "tenant")
            .ToSql();

        // Assert
        Assert.Equal("Select [o].[Id] \r\nFrom Orders o WITH (INDEX(IX_Orders)) /* raw */, [Audit.Log] a /* @tenant */ \r\nJoin (Select 1 As Id) As j On j.Id=o.Id /* ? {0} */ \r\nLeft Join `Archive.Log` l On l.OrderId=o.Id \r\nRight Join \"Payments\" p On p.OrderId=o.Id \r\nWhere [o].[TenantId]=@_p_0", sql);
        Assert.Equal("tenant", builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - Append 条件 overload 仅在条件为真时追加对应原始文本。
    /// </summary>
    [Fact]
    public void AppendRawSql_WhenConditionIsFalse_ShouldLeaveBuilderUnchanged()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.Select("o.Id")
            .AppendFrom("Ignored o", false)
            .AppendFrom("Orders o", true)
            .AppendJoin("IgnoredItems i On i.OrderId=o.Id", false)
            .AppendJoin("Items i On i.OrderId=o.Id", true)
            .AppendLeftJoin("IgnoredAudit a On a.OrderId=o.Id", false)
            .AppendLeftJoin("Audit a On a.OrderId=o.Id", true)
            .AppendRightJoin("IgnoredPayments p On p.OrderId=o.Id", false)
            .AppendRightJoin("Payments p On p.OrderId=o.Id", true)
            .ToSql();

        // Assert
        Assert.Equal("Select [o].[Id] \r\nFrom Orders o \r\nJoin Items i On i.OrderId=o.Id \r\nLeft Join Audit a On a.OrderId=o.Id \r\nRight Join Payments p On p.OrderId=o.Id", sql);
    }

    /// <summary>
    /// 测试 - 空白原始文本应被忽略，结构化 From 的首次原始追加应替换原表。
    /// </summary>
    [Fact]
    public void AppendFrom_WhenRawTextIsBlankOrStructuredFromExists_ShouldApplyDocumentedBehavior()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.Select("*")
            .From("StructuredOrders", "o")
            .AppendFrom(null)
            .AppendFrom(string.Empty)
            .AppendFrom("   ")
            .AppendFrom("RawOrders r")
            .AppendJoin(null)
            .AppendLeftJoin(string.Empty)
            .AppendRightJoin("  ")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom RawOrders r", sql);
    }

    /// <summary>
    /// 测试 - 连续原始 From 追加不应自动插入分隔符。
    /// </summary>
    [Fact]
    public void AppendFrom_WhenCalledRepeatedly_ShouldNotInsertSeparator()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var separatedSql = builder.AppendFrom("Orders o").AppendFrom(", Customers c").ToSql();
        var unseparatedSql = new TestSqlBuilder().AppendFrom("Orders o").AppendFrom("Customers c").ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o, Customers c", separatedSql);
        Assert.Equal("Select * \r\nFrom Orders oCustomers c", unseparatedSql);
    }

    /// <summary>
    /// 测试 - 原始 Append 在 Clone、New、Clear 与重复渲染后应保持独立且稳定。
    /// </summary>
    [Fact]
    public void AppendRawSql_ShouldRemainStableAcrossCloneNewClearAndRepeatedRendering()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.Select("o.Id").AppendFrom("Orders o").AppendJoin("Items i On i.OrderId=o.Id");
        var expected = "Select [o].[Id] \r\nFrom Orders o \r\nJoin Items i On i.OrderId=o.Id";

        // Act
        var firstSql = builder.ToSql();
        var secondSql = builder.ToSql();
        var clone = builder.Clone();
        clone.AppendFrom(", Audit a").AppendLeftJoin("Logs l On l.OrderId=o.Id");
        var cloneSql = clone.ToSql();
        var newBuilder = builder.New().Select("n.Id").AppendFrom("NewOrders n");
        var newSql = newBuilder.ToSql();
        var cleared = builder.Clone();
        cleared.ClearFrom().AppendFrom("ResetOrders r");
        var clearFromSql = cleared.ToSql();
        cleared.ClearJoin();
        var clearJoinSql = cleared.ToSql();

        // Assert
        Assert.Equal(expected, firstSql);
        Assert.Equal(expected, secondSql);
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal("Select [o].[Id] \r\nFrom Orders o, Audit a \r\nJoin Items i On i.OrderId=o.Id \r\nLeft Join Logs l On l.OrderId=o.Id", cloneSql);
        Assert.Equal("Select [n].[Id] \r\nFrom NewOrders n", newSql);
        Assert.Equal("Select [o].[Id] \r\nFrom ResetOrders r \r\nJoin Items i On i.OrderId=o.Id", clearFromSql);
        Assert.Equal("Select [o].[Id] \r\nFrom ResetOrders r", clearJoinSql);
    }
}