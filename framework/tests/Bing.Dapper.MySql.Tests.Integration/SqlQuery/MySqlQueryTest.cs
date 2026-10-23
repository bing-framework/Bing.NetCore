using Bing.Data.Sql;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

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
    /// 当前测试方法的 SQL 服务作用域。
    /// </summary>
    private readonly AsyncServiceScope _serviceScope;

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
    /// 初始化一个 <see cref="MySqlQueryTest"/> 类型的实例。
    /// </summary>
    public MySqlQueryTest(MySqlIntegrationDatabaseFixture fixture)
    {
        _fixture = fixture;
        _serviceScope = fixture.CreateScope();
        _sqlExecutor = fixture.CreateExecutor(_serviceScope.ServiceProvider);
        _sqlQuery = fixture.CreateQuery(_serviceScope.ServiceProvider);
        _sqlQueryFactory = fixture.GetQueryFactory(_serviceScope.ServiceProvider);
        _transactionScopeFactory = fixture.GetTransactionScopeFactory(_serviceScope.ServiceProvider);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 在每个测试类开始前清理测试数据。
    /// </remarks>
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    /// <remarks>
    /// 释放当前测试类创建的 SQL 对象。
    /// </remarks>
    public async Task DisposeAsync()
    {
        _sqlQuery?.Dispose();
        _sqlExecutor?.Dispose();
        await _serviceScope.DisposeAsync();
    }

}
