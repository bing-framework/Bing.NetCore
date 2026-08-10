using Bing.Data.Sql;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Tests.Models;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySql Sql查询对象测试
/// </summary>
[Collection(MySqlIntegrationDatabaseCollection.Name)]
public partial class MySqlQueryTest : IAsyncLifetime
{
    /// <summary>
    /// MySQL 集成测试数据库固定装置。
    /// </summary>
    private readonly MySqlIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// Sql执行器
    /// </summary>
    private readonly ISqlExecutor _sqlExecutor;

    /// <summary>
    /// Sql查询对象
    /// </summary>
    private readonly ISqlQuery _sqlQuery;

    /// <summary>
    /// Sql 查询工厂。
    /// </summary>
    private readonly ISqlQueryFactory _sqlQueryFactory;

    /// <summary>
    /// SQL 事务作用域工厂。
    /// </summary>
    private readonly ISqlTransactionScopeFactory _transactionScopeFactory;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public MySqlQueryTest(MySqlIntegrationDatabaseFixture fixture)
    {
        _fixture = fixture;
        _sqlExecutor = fixture.CreateExecutor();
        _sqlQuery = fixture.CreateQuery();
        _sqlQueryFactory = fixture.GetQueryFactory();
        _transactionScopeFactory = fixture.GetTransactionScopeFactory();
    }

    /// <summary>
    /// 在每个测试类开始前清理测试数据。
    /// </summary>
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <summary>
    /// 释放当前测试类创建的 SQL 对象。
    /// </summary>
    public Task DisposeAsync()
    {
        _sqlQuery?.Dispose();
        _sqlExecutor?.Dispose();
        return Task.CompletedTask;
    }

}
