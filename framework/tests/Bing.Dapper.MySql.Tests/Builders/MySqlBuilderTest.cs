using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// MySql Sql生成器
    /// </summary>
    private MySqlBuilder _builder;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public MySqlBuilderTest()
    {
        _builder = new MySqlBuilder(new SqlBuilderServices(options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities
            {
                Cte = SqlQueryCapabilityState.Supported,
                Intersect = SqlQueryCapabilityState.Supported,
                Except = SqlQueryCapabilityState.Supported
            }
        }));
    }

    /// <summary>
    /// 测试目的：未确认 MySQL 服务器版本时，CTE 必须在 SQL 渲染前被拒绝。
    /// </summary>
    [Fact]
    public void ToSql_WhenMySqlCteVersionIsNotConfirmed_ShouldReject()
    {
        // Arrange
        var builder = new MySqlBuilder();
        var cte = (MySqlBuilder)builder.New();
        cte.Select("Id").From("users");
        builder.Select("Id").From("active_users").With("active_users", cte);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.mysql 的当前查询能力配置不支持 CTE。", exception.Message);
    }
}
