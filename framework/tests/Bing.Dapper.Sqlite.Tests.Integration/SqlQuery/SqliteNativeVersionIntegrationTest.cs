using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 验证 SQLite Provider 的默认依赖实际加载了满足最低版本要求的 SQLite。
/// </summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteNativeVersionIntegrationTest(SqliteIntegrationDatabaseFixture fixture)
{
    /// <summary>
    /// 通过框架读取 SQLite 引擎版本，防止旧版原生库被传递依赖重新引入。
    /// </summary>
    [Fact]
    public void Query_WhenUsingDefaultProviderDependencies_LoadsSupportedNativeVersion()
    {
        using var query = fixture.CreateQuery();
        var actual = Version.Parse(query.Sql("SELECT sqlite_version()").Scalar<string>());

        Assert.True(actual >= new Version(3, 50, 2), $"Expected SQLite 3.50.2 or later, loaded {actual}.");
    }
}
