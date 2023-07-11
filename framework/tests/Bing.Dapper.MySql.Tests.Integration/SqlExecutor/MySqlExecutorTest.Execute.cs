using Bing.Data.Sql;
using Bing.Tests.Configs;

namespace Bing.Dapper.Tests.SqlExecutor;

/// <summary>
/// MySql Sql执行器测试 - 执行Sql测试
/// </summary>
public partial class MySqlExecutorTest
{
    /// <summary>
    /// 测试 - 执行Sql增删改操作
    /// </summary>
    [Fact]
    public async Task Test_ExecuteAsync()
    {
        var id = Guid.NewGuid();
        var sql = "Insert Product(ProductId,Code) Values(@ProductId,@Code)";
        await _sqlExecutor.ExecuteSqlAsync(sql, new { ProductId = id, Code = "abc" });
        var result = await _sqlExecutor.Select("Code").From("Product").Where("ProductId", id).ToStringAsync();
        Assert.Equal(TestConfig.Value, result);
    }
}
