using Bing.Dapper.Tests.Infrastructure;
using Bing.Data.Sql;
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
        var databaseName = Environment.GetEnvironmentVariable(MySqlCrossDatabaseFactAttribute.DatabaseNameEnvironmentVariable);
        Assert.True(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName(databaseName),
            "跨数据库测试库必须符合专用测试库命名约定。");
        var escapedDatabaseName = databaseName.Replace("`", "``", StringComparison.Ordinal);
        var tableName = $"`{escapedDatabaseName}`.`Merchants.Company`";
        var connectionString = new MySqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            Database = databaseName
        }.ConnectionString;
        IntegrationDatabaseSafetyValidator.EnsureResetAllowed(connectionString, "MySql");
        var companyId = Guid.NewGuid();
        await using var connection = new MySqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"Create Database If Not Exists `{escapedDatabaseName}`;");
        await ExecuteAsync(connection, $"Drop Table If Exists {tableName};");
        await ExecuteAsync(connection, $@"
Create Table {tableName}(
    CompanyId char(36) Not Null Primary Key,
    Name varchar(100) Not Null
);");
        await using (var command = new MySqlCommand(
                         $"Insert Into {tableName}(CompanyId, Name) Values (@CompanyId, @Name);", connection))
        {
            command.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            command.Parameters.AddWithValue("@Name", "cross-database-company");
            await command.ExecuteNonQueryAsync();
        }

        try
        {
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
    }
}