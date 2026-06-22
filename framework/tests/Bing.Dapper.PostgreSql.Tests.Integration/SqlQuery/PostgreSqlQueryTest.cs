using Bing.Data.Sql;
using Bing.Test.Shared;
using Xunit.Abstractions;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// PostgreSQL SQL 查询集成测试骨架。
/// 所有测试方法使用 <see cref="IntegrationFactAttribute"/> 标注，
/// 在未设置 RUN_INTEGRATION_TESTS=true 时自动跳过。
///
/// 运行前提：
/// - 设置环境变量 RUN_INTEGRATION_TESTS=true
/// - 设置环境变量 ConnectionStrings__DefaultConnection（或创建 appsettings.Development.json）
/// - PostgreSQL 实例可访问，数据库和账号已准备好
/// </summary>
public class PostgreSqlQueryTest
{
    private readonly ITestOutputHelper _output;
    private readonly ISqlQuery _sqlQuery;

    /// <summary>
    /// 初始化测试
    /// </summary>
    public PostgreSqlQueryTest(ITestOutputHelper output, ISqlQuery sqlQuery)
    {
        _output = output;
        _sqlQuery = sqlQuery;
    }

    /// <summary>
    /// 测试目的：验证 PostgreSQL 连接可用，SELECT 1 应返回 1。
    /// </summary>
    [IntegrationFact]
    public async Task GetValue_SelectOne_ShouldReturnOne()
    {
        // Arrange & Act
        var result = await _sqlQuery.AppendLine("SELECT 1").ToIntAsync();

        // Assert
        _output.WriteLine($"SELECT 1 = {result}");
        Assert.Equal(1, result);
    }
}
