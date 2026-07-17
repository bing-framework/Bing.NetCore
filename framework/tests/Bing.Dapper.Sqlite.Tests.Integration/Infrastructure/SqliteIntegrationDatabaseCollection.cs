namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// SQLite 集成测试数据库集合定义。
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqliteIntegrationDatabaseCollection : ICollectionFixture<SqliteIntegrationDatabaseFixture>
{
    /// <summary>
    /// SQLite 集成测试数据库集合名称。
    /// </summary>
    public const string Name = "SqliteIntegrationDatabase";
}