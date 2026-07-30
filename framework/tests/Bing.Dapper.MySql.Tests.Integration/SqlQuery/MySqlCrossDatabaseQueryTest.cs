using Bing.Dapper.Tests.Infrastructure;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Test.Shared;
using MySqlConnector;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySQL 跨数据库带点物理表真实执行测试。
/// </summary>
[Collection(MySqlIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "MySql")]
public sealed class MySqlCrossDatabaseQueryTest
{
    private readonly MySqlIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个<see cref="MySqlCrossDatabaseQueryTest"/>类型的实例。
    /// </summary>
    /// <param name="fixture">MySQL 集成测试数据库固定装置。</param>
    public MySqlCrossDatabaseQueryTest(MySqlIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 测试 - 显式启用跨数据库环境时应执行带点物理表查询。
    /// </summary>
    [MySqlCrossDatabaseFact]
    public async Task AppendFrom_WhenCrossDatabaseIsEnabled_ShouldExecuteDottedPhysicalTableQuery()
    {
        // Arrange
        var (databaseName, _) = GetCrossDatabaseConfiguration();
        var tableName = GetQualifiedTableName(databaseName, "Merchants.Company");
        var companyId = Guid.NewGuid();
        await using var connection = new MySqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await CreateCompanyTableAsync(connection, tableName);
            await InsertCompanyAsync(connection, tableName, companyId, null, "cross-database-company");
            using var query = _fixture.CreateQuery();
            query.AppendSelect("c.CompanyId,c.Name")
                .AppendFrom($"{tableName} As `c`")
                .Where("c.CompanyId", companyId);

            // Act
            var sql = query.GetBuilder().ToSql();
            var result = query.ExecuteSingle<CrossDatabaseCompanyResult>();

            // Assert
            Assert.Equal($"Select c.CompanyId,c.Name \r\nFrom {tableName} As `c` \r\nWhere `c`.`CompanyId`=@_p_0", sql);
            Assert.Equal(companyId, result.CompanyId);
            Assert.Equal("cross-database-company", result.Name);
        }
        finally
        {
            await ExecuteAsync(connection, $"Drop Table If Exists {tableName};");
        }
    }

    /// <summary>
    /// 测试 - 公开字符串 From 应解析带反引号的跨数据库带点物理表并真实执行。
    /// </summary>
    [MySqlCrossDatabaseFact]
    public async Task From_WhenUsingQualifiedDottedPhysicalTable_ShouldExecuteQuery()
    {
        // Arrange
        var (databaseName, _) = GetCrossDatabaseConfiguration();
        var companyId = Guid.NewGuid();
        var tableName = GetQualifiedTableName(databaseName, "Merchants.Company");
        await using var connection = new MySqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await CreateCompanyTableAsync(connection, tableName);
            await InsertCompanyAsync(connection, tableName, companyId, null, "string-cross-database-company");
            using var query = _fixture.CreateQuery();
            query.Select("c.CompanyId,c.Name")
                .From(tableName, "c")
                .Where("c.CompanyId", companyId);

            // Act
            var sql = query.GetBuilder().ToSql();
            var parameterNames = query.GetParams().Keys.ToArray();
            var result = query.ExecuteSingle<CrossDatabaseCompanyResult>();

            // Assert
            Assert.Equal($"Select `c`.`CompanyId`,`c`.`Name` \r\nFrom {tableName} As `c` \r\nWhere `c`.`CompanyId`=@_p_0", sql);
            Assert.Equal(new[] { "@_p_0" }, parameterNames);
            Assert.Equal(companyId, result.CompanyId);
            Assert.Equal("string-cross-database-company", result.Name);
        }
        finally
        {
            await ExecuteAsync(connection, $"Drop Table If Exists {tableName};");
        }
    }

    /// <summary>
    /// 测试 - 结构化 From 应在预创建的跨数据库中执行带点物理表查询。
    /// </summary>
    [MySqlCrossDatabaseFact]
    public async Task From_WhenUsingStructuredCrossDatabaseReference_ShouldExecuteDottedPhysicalTableQuery()
    {
        // Arrange
        var (databaseName, _) = GetCrossDatabaseConfiguration();
        var companyId = Guid.NewGuid();
        var tableName = GetQualifiedTableName(databaseName, "Merchants.Company");
        await using var connection = new MySqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await CreateCompanyTableAsync(connection, tableName);
            await InsertCompanyAsync(connection, tableName, companyId, null, "structured-cross-database-company");
            using var query = _fixture.CreateQuery();
            query.Select("c.CompanyId,c.Name");
            ((ISqlQueryClauseAccessor)query.GetBuilder()).FromClause.From(new SqlTableReference
            {
                Schema = databaseName,
                TableName = "Merchants.Company",
                Alias = "c"
            });
            query.Where("c.CompanyId", companyId);

            // Act
            var sql = query.GetBuilder().ToSql();
            var parameterNames = query.GetParams().Keys.ToArray();
            var result = query.ExecuteSingle<CrossDatabaseCompanyResult>();

            // Assert
            Assert.Equal($"Select `c`.`CompanyId`,`c`.`Name` \r\nFrom {tableName} As `c` \r\nWhere `c`.`CompanyId`=@_p_0", sql);
            Assert.Equal(new[] { "@_p_0" }, parameterNames);
            Assert.Equal(companyId, result.CompanyId);
            Assert.Equal("structured-cross-database-company", result.Name);
        }
        finally
        {
            await ExecuteAsync(connection, $"Drop Table If Exists {tableName};");
        }
    }

    /// <summary>
    /// 测试 - 结构化 LeftJoin 应跨预创建安全库执行，且无匹配记录仍返回左表数据。
    /// </summary>
    [MySqlCrossDatabaseFact]
    public async Task LeftJoin_WhenUsingStructuredCrossDatabaseReference_ShouldExecuteAndPreserveUnmatchedRows()
    {
        // Arrange
        var (databaseName, _) = GetCrossDatabaseConfiguration();
        var merchantTable = GetQualifiedTableName(databaseName, "Merchants.Merchant");
        var merchantId = Guid.NewGuid();
        var matchedCompanyId = Guid.NewGuid();
        var unmatchedCompanyId = Guid.NewGuid();
        await using var connection = new MySqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await CreateMerchantTableAsync(connection, merchantTable);
            await InsertMerchantAsync(connection, merchantTable, merchantId, "cross-database-merchant");
            await InsertCompanyAsync(connection, "`Merchants.Company`", matchedCompanyId, merchantId,
                "cross-database-company");
            await InsertCompanyAsync(connection, "`Merchants.Company`", unmatchedCompanyId, Guid.NewGuid(),
                "cross-database-company-without-merchant");

            using var matchedQuery = CreateStructuredCrossDatabaseJoinQuery(databaseName, matchedCompanyId);

            // Act
            var sql = matchedQuery.GetBuilder().ToSql();
            var parameterNames = matchedQuery.GetParams().Keys.ToArray();
            var matchedResult = matchedQuery.ExecuteSingle<CrossDatabaseCompanyResult>();
            using var unmatchedQuery = CreateStructuredCrossDatabaseJoinQuery(databaseName, unmatchedCompanyId);
            var unmatchedResult = unmatchedQuery.ExecuteSingle<CrossDatabaseCompanyResult>();

            // Assert
            Assert.Equal($"Select `c`.`CompanyId`,`c`.`Name`,`m`.`Name` As `MerchantName` \r\nFrom `Merchants.Company` As `c` \r\nLeft Join {merchantTable} As `m` On c.MerchantId=m.MerchantId \r\nWhere `c`.`CompanyId`=@_p_0", sql);
            Assert.Equal(new[] { "@_p_0" }, parameterNames);
            Assert.Equal(matchedCompanyId, matchedResult.CompanyId);
            Assert.Equal("cross-database-company", matchedResult.Name);
            Assert.Equal("cross-database-merchant", matchedResult.MerchantName);
            Assert.Equal(unmatchedCompanyId, unmatchedResult.CompanyId);
            Assert.Null(unmatchedResult.MerchantName);
        }
        finally
        {
            try
            {
                await DeleteCompaniesAsync(connection, matchedCompanyId, unmatchedCompanyId);
            }
            finally
            {
                await ExecuteAsync(connection, $"Drop Table If Exists {merchantTable};");
            }
        }
    }

    /// <summary>
    /// 测试 - 公开字符串 Join 应解析跨数据库带点物理表并返回匹配记录。
    /// </summary>
    [MySqlCrossDatabaseFact]
    public async Task Join_WhenUsingQualifiedDottedPhysicalTable_ShouldExecuteQuery()
    {
        // Arrange
        var (databaseName, _) = GetCrossDatabaseConfiguration();
        var merchantTable = GetQualifiedTableName(databaseName, "Merchants.Merchant");
        var merchantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var connection = new MySqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await CreateMerchantTableAsync(connection, merchantTable);
            await InsertMerchantAsync(connection, merchantTable, merchantId, "string-cross-database-merchant");
            await InsertCompanyAsync(connection, "`Merchants.Company`", companyId, merchantId,
                "string-cross-database-company");
            using var query = _fixture.CreateQuery();
            query.Select("c.CompanyId,c.Name,m.Name As MerchantName")
                .From("Merchants.Company", "c")
                .Join(merchantTable, "m")
                .AppendOn("c.MerchantId=m.MerchantId")
                .Where("c.CompanyId", companyId);

            // Act
            var sql = query.GetBuilder().ToSql();
            var parameterNames = query.GetParams().Keys.ToArray();
            var result = query.ExecuteSingle<CrossDatabaseCompanyResult>();

            // Assert
            Assert.Equal($"Select `c`.`CompanyId`,`c`.`Name`,`m`.`Name` As `MerchantName` \r\nFrom `Merchants.Company` As `c` \r\nJoin {merchantTable} As `m` On c.MerchantId=m.MerchantId \r\nWhere `c`.`CompanyId`=@_p_0", sql);
            Assert.Equal(new[] { "@_p_0" }, parameterNames);
            Assert.Equal(companyId, result.CompanyId);
            Assert.Equal("string-cross-database-company", result.Name);
            Assert.Equal("string-cross-database-merchant", result.MerchantName);
        }
        finally
        {
            try
            {
                await DeleteCompaniesAsync(connection, companyId);
            }
            finally
            {
                await ExecuteAsync(connection, $"Drop Table If Exists {merchantTable};");
            }
        }
    }

    /// <summary>
    /// 测试 - 公开字符串 LeftJoin 应解析跨数据库带点物理表并保留无匹配左表记录。
    /// </summary>
    [MySqlCrossDatabaseFact]
    public async Task LeftJoin_WhenUsingQualifiedDottedPhysicalTable_ShouldPreserveUnmatchedRow()
    {
        // Arrange
        var (databaseName, _) = GetCrossDatabaseConfiguration();
        var merchantTable = GetQualifiedTableName(databaseName, "Merchants.Merchant");
        var companyId = Guid.NewGuid();
        await using var connection = new MySqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await CreateMerchantTableAsync(connection, merchantTable);
            await InsertCompanyAsync(connection, "`Merchants.Company`", companyId, Guid.NewGuid(),
                "string-cross-database-company-without-merchant");
            using var query = _fixture.CreateQuery();
            query.Select("c.CompanyId,c.Name,m.Name As MerchantName")
                .From("Merchants.Company", "c")
                .LeftJoin(merchantTable, "m")
                .AppendOn("c.MerchantId=m.MerchantId")
                .Where("c.CompanyId", companyId);

            // Act
            var sql = query.GetBuilder().ToSql();
            var parameterNames = query.GetParams().Keys.ToArray();
            var result = query.ExecuteSingle<CrossDatabaseCompanyResult>();

            // Assert
            Assert.Equal($"Select `c`.`CompanyId`,`c`.`Name`,`m`.`Name` As `MerchantName` \r\nFrom `Merchants.Company` As `c` \r\nLeft Join {merchantTable} As `m` On c.MerchantId=m.MerchantId \r\nWhere `c`.`CompanyId`=@_p_0", sql);
            Assert.Equal(new[] { "@_p_0" }, parameterNames);
            Assert.Equal(companyId, result.CompanyId);
            Assert.Equal("string-cross-database-company-without-merchant", result.Name);
            Assert.Null(result.MerchantName);
        }
        finally
        {
            try
            {
                await DeleteCompaniesAsync(connection, companyId);
            }
            finally
            {
                await ExecuteAsync(connection, $"Drop Table If Exists {merchantTable};");
            }
        }
    }

    /// <summary>
    /// 获取已预创建的跨数据库测试配置。
    /// </summary>
    private (string DatabaseName, string ConnectionString) GetCrossDatabaseConfiguration()
    {
        var databaseName = Environment.GetEnvironmentVariable(MySqlCrossDatabaseFactAttribute.DatabaseNameEnvironmentVariable);
        Assert.True(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName(databaseName),
            "跨数据库测试库必须符合专用测试库命名约定。");
        var connectionString = new MySqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            Database = databaseName
        }.ConnectionString;
        IntegrationDatabaseSafetyValidator.EnsureResetAllowed(connectionString, "MySql");
        return (databaseName, connectionString);
    }

    /// <summary>
    /// 创建结构化跨数据库 LeftJoin 查询。
    /// </summary>
    private ISqlQuery CreateStructuredCrossDatabaseJoinQuery(string databaseName, Guid companyId)
    {
        var query = _fixture.CreateQuery();
        query.Select("c.CompanyId,c.Name,m.Name As MerchantName");
        var builder = (ISqlQueryClauseAccessor)query.GetBuilder();
        builder.FromClause.From(new SqlTableReference { TableName = "Merchants.Company", Alias = "c" });
        builder.JoinClause.LeftJoin(new SqlTableReference
        {
            Schema = databaseName,
            TableName = "Merchants.Merchant",
            Alias = "m"
        });
        builder.JoinClause.AppendOn("c.MerchantId=m.MerchantId");
        query.Where("c.CompanyId", companyId);
        return query;
    }

    /// <summary>
    /// 创建跨数据库专属公司表。
    /// </summary>
    private static async Task CreateCompanyTableAsync(MySqlConnection connection, string tableName)
    {
        await ExecuteAsync(connection, $"Drop Table If Exists {tableName};");
        await ExecuteAsync(connection, $@"
Create Table {tableName}(
    CompanyId char(36) Not Null Primary Key,
    MerchantId char(36) Null,
    Name varchar(100) Not Null
);");
    }

    /// <summary>
    /// 创建跨数据库专属商户表。
    /// </summary>
    private static async Task CreateMerchantTableAsync(MySqlConnection connection, string tableName)
    {
        await ExecuteAsync(connection, $"Drop Table If Exists {tableName};");
        await ExecuteAsync(connection, $@"
Create Table {tableName}(
    MerchantId char(36) Not Null Primary Key,
    Name varchar(100) Not Null
);");
    }

    /// <summary>
    /// 插入公司测试数据。
    /// </summary>
    private static async Task InsertCompanyAsync(MySqlConnection connection, string tableName, Guid companyId,
        Guid? merchantId, string name)
    {
        await using var command = new MySqlCommand(
            $"Insert Into {tableName}(CompanyId, MerchantId, Name) Values (@CompanyId, @MerchantId, @Name);", connection);
        command.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        command.Parameters.AddWithValue("@MerchantId", merchantId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Name", name);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 插入商户测试数据。
    /// </summary>
    private static async Task InsertMerchantAsync(MySqlConnection connection, string tableName, Guid merchantId,
        string name)
    {
        await using var command = new MySqlCommand(
            $"Insert Into {tableName}(MerchantId, Name) Values (@MerchantId, @Name);", connection);
        command.Parameters.AddWithValue("@MerchantId", merchantId.ToString());
        command.Parameters.AddWithValue("@Name", name);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 删除主测试库中的专属公司数据。
    /// </summary>
    private static async Task DeleteCompaniesAsync(MySqlConnection connection, params Guid[] companyIds)
    {
        var parameterNames = companyIds.Select((_, index) => $"@CompanyId{index}").ToArray();
        await using var command = new MySqlCommand(
            $"Delete From `Merchants.Company` Where CompanyId In ({string.Join(", ", parameterNames)});", connection);
        for (var index = 0; index < companyIds.Length; index++)
            command.Parameters.AddWithValue(parameterNames[index], companyIds[index].ToString());
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 获取带反引号的跨数据库专属表名。
    /// </summary>
    private static string GetQualifiedTableName(string databaseName, string tableName) =>
        $"`{databaseName.Replace("`", "``", StringComparison.Ordinal)}`.`{tableName}`";

    /// <summary>
    /// 执行跨数据库测试专用的 MySQL SQL。
    /// </summary>
    /// <param name="connection">已打开的 MySQL 连接。</param>
    /// <param name="sql">SQL 语句。</param>
    private static async Task ExecuteAsync(MySqlConnection connection, string sql)
    {
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 跨数据库带点物理表查询结果。
    /// </summary>
    private sealed class CrossDatabaseCompanyResult
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