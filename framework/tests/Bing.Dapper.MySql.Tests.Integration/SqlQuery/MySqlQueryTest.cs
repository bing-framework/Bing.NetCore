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
    /// 初始化一个 <see cref="MySqlQueryTest"/> 类型的实例。
    /// </summary>
    public MySqlQueryTest(MySqlIntegrationDatabaseFixture fixture)
    {
        _fixture = fixture;
        _sqlExecutor = fixture.CreateExecutor();
        _sqlQuery = fixture.CreateQuery();
        _sqlQueryFactory = fixture.GetQueryFactory();
        _transactionScopeFactory = fixture.GetTransactionScopeFactory();
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
    public Task DisposeAsync()
    {
        _sqlQuery?.Dispose();
        _sqlExecutor?.Dispose();
        return Task.CompletedTask;
    }

}
