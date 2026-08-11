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
    [IntegrationFact("MySql")]
    public async Task Test_ExecuteAsync()
    {
        var id = Guid.NewGuid();
        var sql = "Insert Product(ProductId,Code) Values(@ProductId,@Code)";
        await _sqlExecutor.ExecuteSqlAsync(sql, new { ProductId = id, Code = "abc" });
        using var query = _fixture.CreateQuery();
        var result = await query.Query<string>().Select("Code").From("Product").Where("ProductId", id).ScalarAsync();
        Assert.Equal(TestConfig.Value, result);
    }

    /// <summary>
    /// 测试 - MySQL 执行器应返回 Insert、Update、Delete 与无匹配更新的实际影响行数。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ExecuteSqlAsync_ShouldReturnAffectedRowsForInsertUpdateAndDelete()
    {
        // Arrange
        var id = Guid.NewGuid();
        using var executor = _fixture.CreateExecutor();

        // Act
        var inserted = await executor.ExecuteSqlAsync("Insert Product(ProductId,Code,Name) Values(@id,@code,@name)",
            new { id, code = "executor", name = "before" });
        var updated = await executor.ExecuteSqlAsync("Update Product Set Name=@name Where ProductId=@id",
            new { id, name = "after" });
        var deleted = await executor.ExecuteSqlAsync("Delete From Product Where ProductId=@id", new { id });
        var unmatched = await executor.ExecuteSqlAsync("Update Product Set Name=@name Where ProductId=@id",
            new { id = Guid.NewGuid(), name = "missing" });

        // Assert
        Assert.Equal(1, inserted);
        Assert.Equal(1, updated);
        Assert.Equal(1, deleted);
        Assert.Equal(0, unmatched);
    }
}
