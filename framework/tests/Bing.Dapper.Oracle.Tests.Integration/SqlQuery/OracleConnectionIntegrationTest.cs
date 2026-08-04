namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// Oracle 连接集成测试。
/// </summary>
public sealed class OracleConnectionIntegrationTest
{
    private readonly ISqlQuery _query;

    /// <summary>
    /// 初始化 Oracle 连接集成测试。
    /// </summary>
    /// <param name="query">Oracle SQL 查询对象。</param>
    public OracleConnectionIntegrationTest(ISqlQuery query) => _query = query;

    /// <summary>
    /// 测试目的：Oracle Provider 启用且连接有效时，应执行 DUAL 标量查询。
    /// </summary>
    [IntegrationFact("Oracle")]
    [Trait("Category", "Integration")]
    [Trait("Database", "Oracle")]
    public async Task ExecuteScalar_ShouldReturnOneFromDual()
    {
        var result = await _query.Sql<int>().Select("1").From("DUAL").ScalarAsync();

        Assert.Equal(1, result);
    }
}