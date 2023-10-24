using Bing.Data.Sql;
using Bing.Tests.Models;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySql Sql查询对象测试 - 查询测试
/// </summary>
public partial class MySqlQueryTest
{
    #region ExecuteQuery

    /// <summary>
    /// 测试 - 获取实体集合
    /// </summary>
    [Fact]
    public async Task Test_ExecuteQuery_1()
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

        //断言
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == id);
        Assert.Contains(result, t => t.Id == id2);
        Assert.Contains(result, t => t.Code == code);
    }

    /// <summary>
    /// 初始化数据
    /// </summary>
    private async Task InitProductDataAsync(Guid id, string code)
    {
        var sql = "Insert Product(ProductId,Code) Values(@ProductId,@Code)";
        await _sqlExecutor.ExecuteSqlAsync(sql, new { ProductId = id, Code = code });
    }

    #endregion
}
