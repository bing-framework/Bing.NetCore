using Bing.Data.Sql;
using Bing.Test.Shared;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 同容器多 Provider 集成测试。
/// 运行前需要设置 <c>RUN_INTEGRATION_TESTS=true</c>，并配置
/// <c>ConnectionStrings__MySqlConnection</c>、<c>ConnectionStrings__PostgreSqlConnection</c>
/// 和 <c>ConnectionStrings__DefaultConnection</c>。
/// </summary>
public class MultiProviderQueryTest
{
    /// <summary>
    /// 数据库作用域管理器。
    /// </summary>
    private readonly IDatabaseScopeManager _databaseScopeManager;

    /// <summary>
    /// SQL 查询工厂。
    /// </summary>
    private readonly ISqlQueryFactory _queryFactory;

    /// <summary>
    /// 初始化一个<see cref="MultiProviderQueryTest"/>类型的实例。
    /// </summary>
    /// <param name="databaseScopeManager">数据库作用域管理器。</param>
    /// <param name="queryFactory">SQL 查询工厂。</param>
    public MultiProviderQueryTest(IDatabaseScopeManager databaseScopeManager, ISqlQueryFactory queryFactory)
    {
        _databaseScopeManager = databaseScopeManager;
        _queryFactory = queryFactory;
    }

    /// <summary>
    /// 测试 - 同一个容器应根据 dbKey 在 MySQL、PostgreSQL 和 SQL Server 之间切换查询 Provider。
    /// </summary>
    [IntegrationFact]
    public async Task ExecuteScalar_WhenSwitchingProviders_ShouldReturnOneForEachDataSource()
    {
        // Arrange
        var keys = new[] { "mysql", "pgsql", "sqlserver" };

        // Act
        var results = new List<int>();
        foreach (var key in keys)
            results.Add(await ExecuteSelectOneAsync(key));

        // Assert
        Assert.Equal(new[] { 1, 1, 1 }, results);
    }

    /// <summary>
    /// 在指定数据库作用域中执行 SELECT 1。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>查询结果。</returns>
    private async Task<int> ExecuteSelectOneAsync(string dbKey)
    {
        using (_databaseScopeManager.Use(dbKey))
        using (var query = _queryFactory.Create<ISqlQuery>())
        {
            return await query.Sql<int>().AppendSelect("1").AppendFrom("(Select 1 as Value) t").ScalarAsync();
        }
    }
}