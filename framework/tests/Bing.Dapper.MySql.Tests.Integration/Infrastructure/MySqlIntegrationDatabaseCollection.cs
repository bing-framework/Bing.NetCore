namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// MySQL 集成测试数据库集合定义。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MySqlIntegrationDatabaseCollection : ICollectionFixture<MySqlIntegrationDatabaseFixture>
{
    /// <summary>
    /// MySQL 集成测试数据库集合名称。
    /// </summary>
    public const string Name = "MySqlIntegrationDatabase";
}