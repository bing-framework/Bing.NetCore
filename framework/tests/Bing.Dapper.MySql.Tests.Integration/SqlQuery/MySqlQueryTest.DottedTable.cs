using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
using Bing.Dapper.Tests.Infrastructure;

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
        var result = _sqlQuery.Query<int>().AppendSelect("Count(*)")
            .From("Merchants.Company", "c")
            .Where("c.CompanyId", id)
            .Scalar();

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
        var result = _sqlQuery.Query<int>().AppendSelect("Count(*)")
            .AppendFrom("`Merchants.Company` As `c`")
            .Where("c.CompanyId", id)
            .Scalar();

        // Assert
        Assert.Equal(1, result);
    }

    /// <summary>
    /// 测试 - 带点物理表的结构化左连接应真实执行并返回关联商户。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task LeftJoin_WithDottedPhysicalTables_ShouldExecuteSuccessfully()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        await InitDottedMerchantDataAsync(merchantId, "merchant-structured");
        await InitDottedCompanyDataAsync(companyId, "company-structured", merchantId);
        using var query = _fixture.CreateQuery();
        var description = query.Query<DottedCompanyJoinResult>().Select("c.CompanyId,c.Name,m.Name As MerchantName")
            .From("Merchants.Company", "c")
            .LeftJoin("Merchants.Merchant", "m")
            .AppendOn("c.MerchantId=m.MerchantId")
            .Where("c.CompanyId", companyId);

        // Act
        var sql = description.ToSql();
        var result = description.FirstOrDefault();

        // Assert
        Assert.Equal("Select `c`.`CompanyId`,`c`.`Name`,`m`.`Name` As `MerchantName` \r\nFrom `Merchants.Company` As `c` \r\nLeft Join `Merchants.Merchant` As `m` On c.MerchantId=m.MerchantId \r\nWhere `c`.`CompanyId`=@_p_0", sql);
        Assert.Equal(companyId, result.CompanyId);
        Assert.Equal("company-structured", result.Name);
        Assert.Equal("merchant-structured", result.MerchantName);
    }

    /// <summary>
    /// 测试 - 带点物理表的类型化左连接应通过元数据映射真实执行。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task LeftJoin_WithTypedDottedPhysicalTables_ShouldExecuteSuccessfully()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        await InitDottedMerchantDataAsync(merchantId, "merchant-typed");
        await InitDottedCompanyDataAsync(companyId, "company-typed", merchantId);
        using var query = _fixture.CreateQuery();
        var description = query.From<MySqlDottedCompany>()
            .ClearSelect()
            .Select("c.CompanyId,c.Name,m.Name As MerchantName")
            .From(new SqlTableReference
            {
                EntityType = typeof(MySqlDottedCompany),
                TableName = "Merchants.Company",
                Alias = "c"
            })
            .LeftJoin<MySqlDottedMerchant>("m")
            .On<MySqlDottedCompany, MySqlDottedMerchant>((company, merchant) => company.MerchantId == merchant.MerchantId)
            .Where("c.CompanyId", companyId);

        // Act
        var sql = description.ToSql();
        var result = description.FirstOrDefault<DottedCompanyJoinResult>();

        // Assert
        Assert.Equal("Select `c`.`CompanyId`,`c`.`Name`,`m`.`Name` As `MerchantName` \r\nFrom `Merchants.Company` As `c` \r\nLeft Join `Merchants.Merchant` As `m` On `c`.`MerchantId`=`m`.`MerchantId` \r\nWhere `c`.`CompanyId`=@_p_0", sql);
        Assert.Equal(companyId, result.CompanyId);
        Assert.Equal("company-typed", result.Name);
        Assert.Equal("merchant-typed", result.MerchantName);
    }

    /// <summary>
    /// 测试 - 原始 Append 带点物理表左连接应真实执行。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task AppendLeftJoin_WithDottedPhysicalTables_ShouldExecuteSuccessfully()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        await InitDottedMerchantDataAsync(merchantId, "merchant-raw");
        await InitDottedCompanyDataAsync(companyId, "company-raw", merchantId);
        using var query = _fixture.CreateQuery();
        var description = query.Query<DottedCompanyJoinResult>().AppendSelect("c.CompanyId,c.Name,m.Name As MerchantName")
            .AppendFrom("`Merchants.Company` As `c`")
            .AppendLeftJoin("`Merchants.Merchant` As `m`")
            .AppendOn("`m`.`MerchantId`=`c`.`MerchantId`")
            .Where("c.CompanyId", companyId);

        // Act
        var sql = description.ToSql();
        var result = description.FirstOrDefault();

        // Assert
        Assert.Equal("Select c.CompanyId,c.Name,m.Name As MerchantName \r\nFrom `Merchants.Company` As `c` \r\nLeft Join `Merchants.Merchant` As `m` On `m`.`MerchantId`=`c`.`MerchantId` \r\nWhere `c`.`CompanyId`=@_p_0", sql);
        Assert.Equal(companyId, result.CompanyId);
        Assert.Equal("company-raw", result.Name);
        Assert.Equal("merchant-raw", result.MerchantName);
    }

    /// <summary>
    /// 测试 - 带点物理表左连接无匹配商户时仍应返回公司。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task LeftJoin_WhenDottedMerchantDoesNotMatch_ShouldReturnCompanyWithNullMerchant()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        await InitDottedCompanyDataAsync(companyId, "company-without-merchant", Guid.NewGuid());
        using var query = _fixture.CreateQuery();
        var description = query.Query<DottedCompanyJoinResult>().Select("c.CompanyId,c.Name,m.Name As MerchantName")
            .From("Merchants.Company", "c")
            .LeftJoin("Merchants.Merchant", "m")
            .AppendOn("c.MerchantId=m.MerchantId")
            .Where("c.CompanyId", companyId);

        // Act
        var result = description.FirstOrDefault();

        // Assert
        Assert.Equal(companyId, result.CompanyId);
        Assert.Equal("company-without-merchant", result.Name);
        Assert.Null(result.MerchantName);
    }

    /// <summary>
    /// 测试 - MySQL 原始带点表子查询应绑定显式参数并真实执行。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task AppendFrom_WithDottedPhysicalTableAndParameter_ShouldExecuteSuccessfully()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        await InitDottedCompanyDataAsync(companyId, "company-raw-parameter");
        using var query = _fixture.CreateQuery();
        var description = query.Query<DottedCompanyJoinResult>().Select("c.CompanyId,c.Name")
            .AppendFrom("(Select * From `Merchants.Company` Where `CompanyId`=@Id) As `c`")
            .AddParam("Id", companyId);

        // Act
        var firstSql = description.ToSql();
        var secondSql = description.ToSql();

        // Assert
        Assert.Equal("Select `c`.`CompanyId`,`c`.`Name` \r\nFrom (Select * From `Merchants.Company` Where `CompanyId`=@Id) As `c`", firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(new[] { "@Id" }, description.GetParams().Keys);
        Assert.Equal(companyId, description.GetParam("Id"));

        // Act
        var result = description.FirstOrDefault();

        // Assert
        Assert.Equal(companyId, result.CompanyId);
        Assert.Equal("company-raw-parameter", result.Name);
    }

    /// <summary>
    /// 测试 - MySQL 原始 Join 中的参数应与带点物理表真实绑定并执行。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task AppendJoin_WithDottedPhysicalTableAndParameter_ShouldExecuteSuccessfully()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        await InitDottedMerchantDataAsync(merchantId, "merchant-raw-parameter");
        await InitDottedCompanyDataAsync(companyId, "company-raw-parameter", merchantId);
        using var query = _fixture.CreateQuery();
        var description = query.Query<DottedCompanyJoinResult>().Select("c.CompanyId,c.Name,m.Name As MerchantName")
            .AppendFrom("`Merchants.Company` As `c`")
            .AppendJoin("`Merchants.Merchant` As `m` On `m`.`MerchantId`=`c`.`MerchantId` And `m`.`MerchantId`=@MerchantId")
            .AddParam("MerchantId", merchantId);

        // Act
        var sql = description.ToSql();
        var result = description.FirstOrDefault();

        // Assert
        Assert.Equal("Select `c`.`CompanyId`,`c`.`Name`,`m`.`Name` As `MerchantName` \r\nFrom `Merchants.Company` As `c` \r\nJoin `Merchants.Merchant` As `m` On `m`.`MerchantId`=`c`.`MerchantId` And `m`.`MerchantId`=@MerchantId", sql);
        Assert.Equal(companyId, result.CompanyId);
        Assert.Equal("merchant-raw-parameter", result.MerchantName);
    }

    /// <summary>
    /// 带点物理表左连接查询结果。
    /// </summary>
    private sealed class DottedCompanyJoinResult
    {
        /// <summary>
        /// 公司标识。
        /// </summary>
        public Guid CompanyId { get; set; }

        /// <summary>
        /// 公司名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 商户名称。
        /// </summary>
        public string MerchantName { get; set; }
    }
}