using Bing.Data;
using Bing.Data.Transaction;
using Bing.Datas.EntityFramework.Core;
using Bing.Datas.EntityFramework.Sqlite;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// SQLite 工作单元服务注册单元测试。
/// </summary>
public class SqliteUnitOfWorkRegistrationTest
{
    /// <summary>
    /// 测试目的：注册 SQLite 工作单元后，服务契约、实现和 IUnitOfWork 应解析为同一 Scoped 实例。
    /// </summary>
    [Fact]
    public void AddSqliteUnitOfWork_WhenResolvedInScope_ShouldShareImplementationInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
        services.AddSqliteUnitOfWork<ITestUnitOfWork, TestUnitOfWork>("Data Source=:memory:",
            dataConfigAction: config => config.AutoCommit = true);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var implementation = scope.ServiceProvider.GetRequiredService<TestUnitOfWork>();
        var service = scope.ServiceProvider.GetRequiredService<ITestUnitOfWork>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var dataConfig = scope.ServiceProvider.GetRequiredService<IOptions<DataConfig>>().Value;

        // Assert
        Assert.Same(implementation, service);
        Assert.Same(implementation, unitOfWork);
        Assert.True(dataConfig.AutoCommit);
    }

    /// <summary>
    /// 测试工作单元契约。
    /// </summary>
    private interface ITestUnitOfWork : IUnitOfWork
    {
    }

    /// <summary>
    /// 测试工作单元实现。
    /// </summary>
    private sealed class TestUnitOfWork : UnitOfWorkBase, ITestUnitOfWork
    {
        /// <summary>
        /// 初始化一个 <see cref="TestUnitOfWork"/> 类型的实例。
        /// </summary>
        /// <param name="options">数据库上下文配置。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public TestUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }
}