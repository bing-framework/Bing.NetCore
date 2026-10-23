using Bing.Data.Sql;
using Bing.Test.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 同容器多 Provider 集成测试。
/// </summary>
/// <remarks>
/// 该兼容性测试只在本地显式设置 <c>RUN_INTEGRATION_TESTS=true</c> 时运行，并需要 配置 MySQL、PostgreSQL 和 SQL Server 的 Provider 专属连接字符串。
/// </remarks>
public class MultiProviderQueryTest
{
    /// <summary>
    /// 数据库作用域管理器。
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// 初始化一个 <see cref="MultiProviderQueryTest"/> 类型的实例。
    /// </summary>
    /// <param name="scopeFactory">SQL 服务作用域工厂。</param>
    public MultiProviderQueryTest(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

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
        using var serviceScope = _scopeFactory.CreateScope();
        var serviceProvider = serviceScope.ServiceProvider;
        var databaseScopeManager = serviceProvider.GetRequiredService<IDatabaseScopeManager>();
        var queryFactory = serviceProvider.GetRequiredService<ISqlQueryFactory>();
        using (databaseScopeManager.Use(dbKey))
        using (var query = queryFactory.Create())
        {
            return await query.Query().AppendSelect("1").AppendFrom("(Select 1 as Value) t").ScalarAsync<int>();
        }
    }
}
