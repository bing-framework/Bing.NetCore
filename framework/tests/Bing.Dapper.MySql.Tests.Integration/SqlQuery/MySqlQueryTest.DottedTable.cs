using Bing.Data.Sql;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySQL SQL 查询对象带点物理表名真实执行测试。
/// </summary>
public partial class MySqlQueryTest
{
    /// <summary>
    /// 测试 - 结构化字符串 From 应查询带点物理表名并绑定参数。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteScalar_WhenStructuredFromUsesDottedPhysicalTable_ShouldReturnRowCount()
    {
        // Arrange
        var id = Guid.NewGuid();
        await InitDottedCompanyDataAsync(id, "structured-company");

        // Act
        var result = _sqlQuery.AppendSelect("Count(*)")
            .From("Merchants.Company", "c")
            .Where("c.CompanyId", id)
            .ExecuteScalar<int>();

        // Assert
        Assert.Equal(1, result);
    }

    /// <summary>
    /// 测试 - 原始 AppendFrom 应执行反引号包围的带点物理表名。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task ExecuteScalar_WhenAppendFromUsesDottedPhysicalTable_ShouldReturnRowCount()
    {
        // Arrange
        var id = Guid.NewGuid();
        await InitDottedCompanyDataAsync(id, "raw-company");

        // Act
        var result = _sqlQuery.AppendSelect("Count(*)")
            .AppendFrom("`Merchants.Company` As `c`")
            .Where("c.CompanyId", id)
            .ExecuteScalar<int>();

        // Assert
        Assert.Equal(1, result);
    }
}