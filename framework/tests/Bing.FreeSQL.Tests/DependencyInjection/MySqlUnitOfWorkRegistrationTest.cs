using Bing.FreeSQL;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FreeSQL.Tests.DependencyInjection;

/// <summary>
/// MySQL FreeSQL 工作单元注册单元测试。
/// </summary>
public class MySqlUnitOfWorkRegistrationTest
{
    /// <summary>
    /// 测试目的：注册 MySQL 工作单元时应注册单例 FreeSQL 包装器和 Scoped 工作单元实现。
    /// </summary>
    [Fact]
    public void AddMySqlUnitOfWork_WhenRegistered_ShouldUseExpectedServiceLifetimes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMySqlUnitOfWork<ITestUnitOfWork, TestUnitOfWork>("Server=mysql;Database=app;");

        // Assert
        var wrapper = Assert.Single(services.Where(item => item.ServiceType == typeof(FreeSqlWrapper)));
        Assert.Equal(ServiceLifetime.Singleton, wrapper.Lifetime);
        Assert.NotNull(wrapper.ImplementationFactory);

        var unitOfWork = Assert.Single(services.Where(item => item.ServiceType == typeof(ITestUnitOfWork)));
        Assert.Equal(ServiceLifetime.Scoped, unitOfWork.Lifetime);
        Assert.Equal(typeof(TestUnitOfWork), unitOfWork.ImplementationType);
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
    private sealed class TestUnitOfWork : UnitOfWork, ITestUnitOfWork
    {
        /// <summary>
        /// 初始化一个 <see cref="TestUnitOfWork"/> 类型的实例。
        /// </summary>
        /// <param name="wrapper">FreeSQL 包装器。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public TestUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider)
            : base(wrapper, serviceProvider)
        {
        }
    }
}