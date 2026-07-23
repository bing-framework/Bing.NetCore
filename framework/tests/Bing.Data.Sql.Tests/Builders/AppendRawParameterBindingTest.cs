using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// 原始 Append SQL 参数绑定测试。
/// </summary>
public class AppendRawParameterBindingTest
{
    /// <summary>
    /// 测试 - 原始 From 占位符应保留且显式参数应加入参数集合。
    /// </summary>
    [Fact]
    public void AppendFrom_WithParameter_ShouldPreserveSqlAndBindValue()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.Select("o.Id")
            .AppendFrom("(Select * From Orders Where TenantId=@TenantId) o")
            .AddParam("TenantId", "tenant-1")
            .ToSql();

        // Assert
        Assert.Equal("Select [o].[Id] \r\nFrom (Select * From Orders Where TenantId=@TenantId) o", sql);
        Assert.Equal(new[] { "@TenantId" }, builder.GetParams().Keys);
        Assert.Equal("tenant-1", builder.GetParam("TenantId"));
    }

    /// <summary>
    /// 测试 - 原始 Join 占位符应保留且显式参数应加入参数集合。
    /// </summary>
    [Fact]
    public void AppendJoin_WithParameter_ShouldPreserveSqlAndBindValue()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendJoin("Items i On i.OrderId=o.Id And i.TenantId=@TenantId")
            .AddParam("TenantId", "tenant-1")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nJoin Items i On i.OrderId=o.Id And i.TenantId=@TenantId", sql);
        Assert.Equal(new[] { "@TenantId" }, builder.GetParams().Keys);
        Assert.Equal("tenant-1", builder.GetParam("@TenantId"));
    }

    /// <summary>
    /// 测试 - 多个原始参数应保持调用顺序、名称和值。
    /// </summary>
    [Fact]
    public void AppendFrom_WithMultipleParameters_ShouldBindAllValuesInOrder()
    {
        // Arrange
        var startTime = new DateTime(2026, 7, 23, 8, 30, 0, DateTimeKind.Utc);
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("(Select * From Orders Where TenantId=@TenantId And Status=@Status And Created>=@StartTime) o")
            .AddParam("TenantId", "tenant-1")
            .AddParam("Status", 2)
            .AddParam("StartTime", startTime)
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom (Select * From Orders Where TenantId=@TenantId And Status=@Status And Created>=@StartTime) o", sql);
        Assert.Equal(new[] { "@TenantId", "@Status", "@StartTime" }, builder.GetParams().Keys);
        Assert.Equal("tenant-1", builder.GetParam("TenantId"));
        Assert.Equal(2, builder.GetParam("Status"));
        Assert.Equal(startTime, builder.GetParam("StartTime"));
    }

    /// <summary>
    /// 测试 - 原始参数与结构化 Where 参数应使用不同名称且共同保留。
    /// </summary>
    [Fact]
    public void AppendFrom_WithRawAndStructuredParameters_ShouldNotConflict()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("(Select * From Orders Where TenantId=@TenantId) o")
            .AddParam("TenantId", "tenant-1")
            .Where("o.Status", 2)
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom (Select * From Orders Where TenantId=@TenantId) o \r\nWhere [o].[Status]=@_p_0", sql);
        Assert.Equal(new[] { "@TenantId", "@_p_0" }, builder.GetParams().Keys);
        Assert.Equal("tenant-1", builder.GetParam("TenantId"));
        Assert.Equal(2, builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - 重复渲染原始参数 SQL 不应重复注册参数。
    /// </summary>
    [Fact]
    public void AppendRawSql_WhenToSqlCalledRepeatedly_ShouldNotDuplicateParameters()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .AppendFrom("(Select * From Orders Where TenantId=@TenantId) o")
            .AddParam("TenantId", "tenant-1");

        // Act
        var firstSql = builder.ToSql();
        var secondSql = builder.ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom (Select * From Orders Where TenantId=@TenantId) o", firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(new[] { "@TenantId" }, builder.GetParams().Keys);
        Assert.Equal("tenant-1", builder.GetParam("TenantId"));
    }

    /// <summary>
    /// 测试 - Clone 后的原始参数修改不应污染父生成器。
    /// </summary>
    [Fact]
    public void AppendRawSql_WhenCloned_ShouldKeepParameterChangesIsolated()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .AppendFrom("(Select * From Orders Where TenantId=@TenantId) o")
            .AddParam("TenantId", "tenant-1");

        // Act
        var clone = builder.Clone();
        clone.AddParam("TenantId", "tenant-2").AddParam("Status", 2);

        // Assert
        Assert.Equal("tenant-1", builder.GetParam("TenantId"));
        Assert.Equal(new[] { "@TenantId" }, builder.GetParams().Keys);
        Assert.Equal("tenant-2", clone.GetParam("TenantId"));
        Assert.Equal(2, clone.GetParam("Status"));
        Assert.Equal(new[] { "@TenantId", "@Status" }, clone.GetParams().Keys);
    }

    /// <summary>
    /// 测试 - 重复添加同名原始参数应覆盖值而不重复注册。
    /// </summary>
    [Fact]
    public void AppendRawSql_WhenParameterNameIsRepeated_ShouldReplaceValueWithoutDuplicate()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("(Select * From Orders Where TenantId=@TenantId) o")
            .AddParam("TenantId", "tenant-1")
            .AddParam("@TenantId", "tenant-2")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom (Select * From Orders Where TenantId=@TenantId) o", sql);
        Assert.Equal(new[] { "@TenantId" }, builder.GetParams().Keys);
        Assert.Equal("tenant-2", builder.GetParam("TenantId"));
    }

    /// <summary>
    /// 测试 - 原始参数与多个结构化条件参数应按独立名称共同保留。
    /// </summary>
    [Fact]
    public void AppendRawSql_WithMultipleStructuredParameters_ShouldKeepAllParameters()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var sql = builder.AppendFrom("(Select * From Orders Where TenantId=@TenantId) o")
            .AddParam("TenantId", "tenant-1")
            .Where("o.Status", 2)
            .Where("o.Name", "order")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom (Select * From Orders Where TenantId=@TenantId) o \r\nWhere [o].[Status]=@_p_0 And [o].[Name]=@_p_1", sql);
        Assert.Equal(new[] { "@TenantId", "@_p_0", "@_p_1" }, builder.GetParams().Keys);
        Assert.Equal("tenant-1", builder.GetParam("TenantId"));
        Assert.Equal(2, builder.GetParam("@_p_0"));
        Assert.Equal("order", builder.GetParam("@_p_1"));
    }
}