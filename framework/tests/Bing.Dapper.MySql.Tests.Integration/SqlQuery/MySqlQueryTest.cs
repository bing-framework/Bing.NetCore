using Bing.Data.Sql;
using Bing.Tests.Models;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySql Sql查询对象测试
/// </summary>
public partial class MySqlQueryTest
{
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
    public MySqlQueryTest(ISqlExecutor sqlExecutor, ISqlQuery sqlQuery, ISqlQueryFactory sqlQueryFactory,
        ISqlTransactionScopeFactory transactionScopeFactory)
    {
        _sqlExecutor = sqlExecutor;
        _sqlQuery = sqlQuery;
        _sqlQueryFactory = sqlQueryFactory;
        _transactionScopeFactory = transactionScopeFactory;
    }

    /// <summary>
    /// 测试 - 临时禁用调试日志
    /// </summary>
    [IntegrationFact]
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
