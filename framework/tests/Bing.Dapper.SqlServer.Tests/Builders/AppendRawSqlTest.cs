using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQL Server 原始 Append SQL 测试。
/// </summary>
public class AppendRawSqlTest
{
    /// <summary>
    /// 测试 - SQL Server 原始 From 应保留反引号、Hint 和参数占位符。
    /// </summary>
    [Fact]
    public void AppendFrom_ShouldPreserveRawSql()
    {
        // Arrange
        var builder = new SqlServerBuilder();

        // Act
        var sql = builder.Select("u.Id")
            .AppendFrom("`archive`.`Users` u WITH (INDEX(IX_Users)) /* @TenantId */")
            .ToSql();

        // Assert
        Assert.Equal("Select [u].[Id] \r\nFrom `archive`.`Users` u WITH (INDEX(IX_Users)) /* @TenantId */", sql);
    }

    /// <summary>
    /// 测试 - SQL Server 原始 Join 应保留双引号表名。
    /// </summary>
    [Fact]
    public void AppendJoin_ShouldPreserveRawSql()
    {
        var sql = new SqlServerBuilder().Select("u.Id").AppendFrom("Users u")
            .AppendJoin("\"Audit.Log\" a On a.UserId=u.Id").ToSql();

        Assert.Equal("Select [u].[Id] \r\nFrom Users u \r\nJoin \"Audit.Log\" a On a.UserId=u.Id", sql);
    }

    /// <summary>
    /// 测试 - SQL Server 原始左连接应保留调用方提供的文本。
    /// </summary>
    [Fact]
    public void AppendLeftJoin_ShouldPreserveRawSql()
    {
        var sql = new SqlServerBuilder().Select("u.Id").AppendFrom("Users u")
            .AppendLeftJoin("Users l On l.Id=u.Id").ToSql();

        Assert.Equal("Select [u].[Id] \r\nFrom Users u \r\nLeft Join Users l On l.Id=u.Id", sql);
    }

    /// <summary>
    /// 测试 - SQL Server 原始右连接应保留方括号带点表名。
    /// </summary>
    [Fact]
    public void AppendRightJoin_ShouldPreserveRawSql()
    {
        var sql = new SqlServerBuilder().Select("u.Id").AppendFrom("Users u")
            .AppendRightJoin("[Payments.Log] p On p.UserId=u.Id").ToSql();

        Assert.Equal("Select [u].[Id] \r\nFrom Users u \r\nRight Join [Payments.Log] p On p.UserId=u.Id", sql);
    }
}