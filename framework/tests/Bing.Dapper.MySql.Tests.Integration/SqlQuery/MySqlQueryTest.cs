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

    /// <summary>
    /// 测试 - 临时禁用调试日志
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task Test_DisableDebugLog()
    {
        // 插入2条数据
        var id = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var code = "Test_ExecuteQuery_1";
        await InitProductDataAsync(id, code);
        await InitProductDataAsync(id2, code);

        // 获取对象
        var result = _sqlQuery
            .Select<Product>(true)
            .From<Product>()
            .In<Product>(x => x.Id, new object[] { id, id2 })
            .ExecuteQuery<Product>();

        result = _sqlQuery
            .Select<Product>(true)
            .From<Product>()
            .In<Product>(x => x.Id, new object[] { id, id2 })
            .DisableDebugLog()
            .ExecuteQuery<Product>();

        result = _sqlQuery
            .Select<Product>(true)
            .From<Product>()
            .In<Product>(x => x.Id, new object[] { id, id2 })
            .ExecuteQuery<Product>();

        //断言
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == id);
        Assert.Contains(result, t => t.Id == id2);
        Assert.Contains(result, t => t.Code == code);
    }
}
