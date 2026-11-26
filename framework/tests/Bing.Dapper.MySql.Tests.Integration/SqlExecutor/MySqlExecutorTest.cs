using Bing.Data.Sql;
using Bing.Dapper.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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
    /// 当前测试方法的 SQL 服务作用域。
    /// </summary>
    private readonly AsyncServiceScope _serviceScope;

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
    /// 初始化一个 <see cref="MySqlExecutorTest" /> 类型的实例。
    /// </summary>
    /// <param name="output">用于输出当前测试日志的辅助对象。</param>
    /// <param name="fixture">为集成测试提供数据库及相关资源的测试夹具。</param>
    public MySqlExecutorTest(ITestOutputHelper output, MySqlIntegrationDatabaseFixture fixture)
    {
        _output = output;
        _fixture = fixture;
        _serviceScope = fixture.CreateScope();
        _sqlExecutor = fixture.CreateExecutor(_serviceScope.ServiceProvider);
        _sqlExecutor2 = fixture.CreateExecutor(_serviceScope.ServiceProvider);
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
    public async Task DisposeAsync()
    {
        _sqlExecutor?.Dispose();
        _sqlExecutor2?.Dispose();
        await _serviceScope.DisposeAsync();
    }
}
