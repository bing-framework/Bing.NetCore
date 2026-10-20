using Bing.Data.Sql;
using Bing.Dapper.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Bing.Dapper.Tests.SqlExecutor;

/// <summary>
/// MySql Sql执行器测试
/// </summary>
[Collection(MySqlIntegrationDatabaseCollection.Name)]
public partial class MySqlExecutorTest : IAsyncLifetime
{
    /// <summary>
    /// MySQL 集成测试数据库固定装置。
    /// </summary>
    private readonly MySqlIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 测试输出工具
    /// </summary>
    private readonly ITestOutputHelper _output;
    /// <summary>
    /// Sql执行器
    /// </summary>
    private readonly ISqlExecutor _sqlExecutor;
    /// <summary>
    /// Sql执行器2
    /// </summary>
    private readonly ISqlExecutor _sqlExecutor2;

    /// <summary>
    /// 初始化一个 <see cref="MySqlExecutorTest"/> 类型的实例。
    /// </summary>
    public MySqlExecutorTest(ITestOutputHelper output, MySqlIntegrationDatabaseFixture fixture)
    {
        _output = output;
        _fixture = fixture;
        _sqlExecutor = fixture.CreateExecutor();
        _sqlExecutor2 = fixture.CreateExecutor();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 在每个测试类开始前清理测试数据。
    /// </remarks>
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    /// <remarks>
    /// 释放当前测试类创建的 SQL 执行器。
    /// </remarks>
    public Task DisposeAsync()
    {
        _sqlExecutor?.Dispose();
        _sqlExecutor2?.Dispose();
        return Task.CompletedTask;
    }
}
