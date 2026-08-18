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
    [IntegrationFact("MySql")]
    public async Task Test_ExecuteQuery_1()
    {
        // 插入2条数据
        var id = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var code = "Test_ExecuteQuery_1";
        await InitProductDataAsync(id, code);
        await InitProductDataAsync(id2, code);

        // 获取对象
        var result = _sqlQuery.From<Product>()
            .ClearSelect()
            .Select(true)
            .Where<Product>(x => x.Id, new object[] { id, id2 }, Operator.In)
            .ToList();

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

    /// <summary>
    /// 初始化带点物理表名测试数据。
    /// </summary>
    /// <param name="id">公司标识。</param>
    /// <param name="name">公司名称。</param>
    /// <returns>表示异步写入操作的任务。</returns>
    private Task InitDottedCompanyDataAsync(Guid id, string name, Guid? merchantId = null) => _sqlExecutor.ExecuteSqlAsync(
        "Insert Into `Merchants.Company`(CompanyId,MerchantId,Name) Values(@CompanyId,@MerchantId,@Name)",
        new { CompanyId = id, MerchantId = merchantId, Name = name });

    /// <summary>
    /// 初始化带点物理表商户测试数据。
    /// </summary>
    /// <param name="id">商户标识。</param>
    /// <param name="name">商户名称。</param>
    /// <returns>表示异步写入操作的任务。</returns>
    private Task InitDottedMerchantDataAsync(Guid id, string name) => _sqlExecutor.ExecuteSqlAsync(
        "Insert Into `Merchants.Merchant`(MerchantId,Name) Values(@MerchantId,@Name)",
        new { MerchantId = id, Name = name });

    #endregion
}
