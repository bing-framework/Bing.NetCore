namespace Bing.Dapper.Doris.Tests.Integration;

/// <summary>
/// Doris 只读协议集成测试。
/// </summary>
/// <remarks>
/// 测试仅在 <c>RUN_DORIS_INTEGRATION_TESTS=true</c> 或全局集成测试开关启用时执行，
/// 且仅运行常量与参数化读取，不创建、修改或清理任何数据库对象。
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Doris")]
public sealed class DorisReadOnlyIntegrationTest : IDisposable
{
    /// <summary>
    /// 用于执行 Doris 只读查询的根查询对象。
    /// </summary>
    private readonly ISqlQuery _query;

    /// <summary>
    /// 当前测试使用的服务提供程序。
    /// </summary>
    private readonly ServiceProvider _serviceProvider;

    /// <summary>
    /// 初始化 Doris 只读集成测试的服务容器。
    /// </summary>
    public DorisReadOnlyIntegrationTest()
    {
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        var connectionString = IntegrationTestGate.IsProviderEnabled("Doris")
            ? IntegrationTestConnectionStringResolver.Resolve("Doris")
            : string.Empty;
        services.AddSqlDataSource("doris", DatabaseType.Doris, connectionString);
        _serviceProvider = services.BuildServiceProvider();
        _query = _serviceProvider.GetRequiredService<ISqlQueryFactory>().Create("doris");
    }

    /// <summary>
    /// 测试目的：Doris 通过 MySQL 协议执行只读标量探针时，应返回常量结果。
    /// </summary>
    [IntegrationFact("Doris")]
    public async Task ExecuteScalar_WhenSelectingConstant_ShouldReturnOne()
    {
        // Act
        var value = await _query.Sql("Select 1").ScalarAsync<int>();

        // Assert
        Assert.Equal(1, value);
    }

    /// <summary>
    /// 测试目的：Doris 只读查询必须使用 MySQL 参数绑定并返回调用方提供的值。
    /// </summary>
    [IntegrationFact("Doris")]
    public async Task ExecuteScalar_WhenParameterIsBound_ShouldReturnBoundValue()
    {
        // Arrange
        const int expected = 7;

        // Act
        var value = await _query.Sql("Select @value", new { value = expected }).ScalarAsync<int>();

        // Assert
        Assert.Equal(expected, value);
    }

    /// <summary>
    /// 测试目的：Doris 应执行 MySQL Limit/Offset 分页语法，且常量派生集无需测试表。
    /// </summary>
    [IntegrationFact("Doris")]
    public async Task ExecuteList_WhenUsingLimitOffset_ShouldReturnExpectedPage()
    {
        // Act
        var values = await _query.Sql("Select Value From (Select 1 As Value Union All Select 2 As Value) t " +
                                           "Order By Value Limit @limit OFFSET @offset",
                new { limit = 1, offset = 1 })
            .ToListAsync<int>();

        // Assert
        Assert.Equal(new[] { 2 }, values);
    }

    /// <summary>
    /// 释放当前测试创建的查询和服务容器。
    /// </summary>
    public void Dispose()
    {
        _query?.Dispose();
        _serviceProvider?.Dispose();
    }
}
