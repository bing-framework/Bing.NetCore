using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// 其他 Append API 语义测试。
/// </summary>
public class AppendSqlSemanticTest
{
    /// <summary>
    /// 测试 - AppendSelect 应按方言解析方括号标识符。
    /// </summary>
    [Fact]
    public void AppendSelect_WhenSqlContainsBracketIdentifiers_ShouldResolveDialectIdentifiers()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var sql = builder.AppendSelect("[o].[Id]").AppendFrom("Orders o").ToSql();

        // Assert
        Assert.Equal("Select `o`.`Id` \r\nFrom Orders o", sql);
    }

    /// <summary>
    /// 测试 - AppendWhere 应按方言解析标识符且调用方显式参数应被保留。
    /// </summary>
    [Fact]
    public void AppendWhere_WhenSqlContainsParameter_ShouldResolveIdentifiersAndKeepExplicitParameter()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendWhere("[o].[Status]=@Status")
            .AddParam("Status", 2)
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nWhere `o`.`Status`=@Status", sql);
        Assert.Equal(new[] { "@Status" }, builder.GetParams().Keys);
        Assert.Equal(2, builder.GetParam("Status"));
    }

    /// <summary>
    /// 测试 - AppendGroupBy 应按方言解析方括号标识符。
    /// </summary>
    [Fact]
    public void AppendGroupBy_WhenSqlContainsBracketIdentifiers_ShouldResolveDialectIdentifiers()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var sql = builder.AppendSelect("Count(*)")
            .AppendFrom("Orders o")
            .AppendGroupBy("[o].[TenantId]")
            .ToSql();

        // Assert
        Assert.Equal("Select Count(*) \r\nFrom Orders o \r\nGroup By `o`.`TenantId`", sql);
    }

    /// <summary>
    /// 测试 - AppendOrderBy 应按方言解析方括号标识符。
    /// </summary>
    [Fact]
    public void AppendOrderBy_WhenSqlContainsBracketIdentifiers_ShouldResolveDialectIdentifiers()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var sql = builder.AppendSelect("*")
            .AppendFrom("Orders o")
            .AppendOrderBy("[o].[Name] Desc")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nOrder By `o`.`Name` Desc", sql);
    }

    /// <summary>
    /// 测试 - AppendOn 应按方言解析方括号标识符并附加到最后一个连接。
    /// </summary>
    [Fact]
    public void AppendOn_WhenSqlContainsBracketIdentifiers_ShouldResolveDialectIdentifiers()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var sql = builder.AppendFrom("Orders o")
            .AppendJoin("Items i")
            .AppendOn("[i].[OrderId]=[o].[Id]")
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom Orders o \r\nJoin Items i On `i`.`OrderId`=`o`.`Id`", sql);
    }

    /// <summary>
    /// 创建使用反引号标识符的测试生成器。
    /// </summary>
    /// <returns>测试 SQL 生成器。</returns>
    private static TestSqlBuilder CreateBuilder() => new(new BacktickDialect());

    /// <summary>
    /// 用于验证方言标识符替换的测试方言。
    /// </summary>
    private sealed class BacktickDialect : DialectBase
    {
        /// <inheritdoc />
        public override char OpeningIdentifier => '`';

        /// <inheritdoc />
        public override char ClosingIdentifier => '`';

        /// <inheritdoc />
        public override string GetPrefix() => "@";
    }
}