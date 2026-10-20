using Bing.Data.Sql;
using Bing.Test.Shared;
using Xunit.Abstractions;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 验证 SQL Server 查询执行行为。
/// </summary>
/// <remarks>测试由 <see cref="IntegrationFactAttribute"/> 管控，并需要配置 SQL Server 连接及专用集成测试开关。</remarks>
public class SqlServerQueryTest
{
    /// <summary>
    /// 写入 SQL Server 查询测试输出的测试输出助手。
    /// </summary>
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// 用于 SQL Server 查询测试的查询实例。
    /// </summary>
    private readonly ISqlQuery _sqlQuery;

    /// <summary>
    /// 初始化一个 <see cref="SqlServerQueryTest"/> 类型的实例。
    /// </summary>
    /// <param name="output">测试输出写入器。</param>
    /// <param name="sqlQuery">SQL 查询服务。</param>
    public SqlServerQueryTest(ITestOutputHelper output, ISqlQuery sqlQuery)
    {
        _output = output;
        _sqlQuery = sqlQuery;
    }

    /// <summary>
    /// 测试目的：验证 SQL Server 连接可用，SELECT 1 应返回 1。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task GetValue_SelectOne_ShouldReturnOne()
    {
        // Arrange
        // Act
        var result = await _sqlQuery.Query().AppendSelect("1").AppendFrom("(Select 1 as Value) t").ScalarAsync<int>();

        // Assert
        _output.WriteLine($"SELECT 1 = {result}");
        Assert.Equal(1, result);
    }
}
