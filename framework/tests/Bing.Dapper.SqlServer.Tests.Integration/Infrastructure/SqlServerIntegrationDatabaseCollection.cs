namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// SQL Server 集成测试数据库集合定义。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerIntegrationDatabaseCollection : ICollectionFixture<SqlServerIntegrationDatabaseFixture>
{
    /// <summary>
    /// SQL Server 集成测试数据库集合名称。
    /// </summary>
    public const string Name = "SqlServerIntegrationDatabase";
}