namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// Oracle 连接集成测试。
/// </summary>
public sealed class OracleConnectionIntegrationTest
{
    /// <summary>
    /// 用于 Oracle 连接集成测试的 SQL 查询实例。
    /// </summary>
    private readonly ISqlQuery _query;

    /// <summary>
    /// 初始化一个 <see cref="OracleConnectionIntegrationTest"/> 类型的实例。
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
        var result = await _query.Query().Select("1").From("DUAL").ScalarAsync<int>();

        Assert.Equal(1, result);
    }
}
