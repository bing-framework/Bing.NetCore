using Bing.Data.Sql;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Core.Tests.Metadata;

/// <summary>
/// <see cref="SqlProviderRuntimeRegistration"/> 单元测试。
/// </summary>
public class SqlProviderRuntimeRegistrationTest
{
    /// <summary>
    /// 测试目的：不同 Provider 注册应保存各自服务实现，避免相同数据库类型的 Provider 相互覆盖。
    /// </summary>
    [Fact]
    public void Resolve_WhenProviderRegistrationsDiffer_ShouldKeepProviderSpecificImplementations()
    {
        // Arrange
        var first = new SqlProviderRuntimeRegistration("custom.sqlserver.first");
        var second = new SqlProviderRuntimeRegistration("custom.sqlserver.second");
        first.Map(typeof(ITestService), typeof(DefaultService));
        second.Map(typeof(ITestService), typeof(SqlServerService));

        // Act
        var firstResult = first.Resolve(typeof(ITestService));
        var secondResult = second.Resolve(typeof(ITestService));

        // Assert
        Assert.Equal(typeof(DefaultService), firstResult);
        Assert.Equal(typeof(SqlServerService), secondResult);
    }

    /// <summary>
    /// 测试目的：未注册的服务契约不应回退到任意具体实现。
    /// </summary>
    [Fact]
    public void Resolve_WhenServiceIsNotRegistered_ShouldReturnNull()
    {
        // Arrange
        var registration = new SqlProviderRuntimeRegistration("custom.mysql");
        registration.Map(typeof(ITestService), typeof(DefaultService));

        // Act
        var result = registration.Resolve(typeof(ConcreteService));

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// 测试目的：注册时必须验证服务实现关系，避免 Factory 在运行时创建不兼容的类型。
    /// </summary>
    [Fact]
    public void Map_WhenImplementationDoesNotImplementService_ShouldThrowArgumentException()
    {
        // Arrange
        var registration = new SqlProviderRuntimeRegistration("custom.provider");

        // Act and Assert
        Assert.Throws<ArgumentException>(() => registration.Map(typeof(ITestService), typeof(ConcreteService)));
    }

    /// <summary>
    /// 测试目的：同一 Provider 和服务契约只能登记一个实现，重复相同实现保持幂等。
    /// </summary>
    [Fact]
    public void Map_WhenServiceIsRegisteredRepeatedly_ShouldBeIdempotentOrRejectConflict()
    {
        // Arrange
        var registration = new SqlProviderRuntimeRegistration("custom.sqlserver");

        // Act
        registration.Map(typeof(ITestService), typeof(SqlServerService));
        registration.Map(typeof(ITestService), typeof(SqlServerService));

        // Assert
        Assert.Equal(typeof(SqlServerService), registration.Resolve(typeof(ITestService)));
        Assert.Throws<InvalidOperationException>(() =>
            registration.Map(typeof(ITestService), typeof(DefaultService)));
    }

    /// <summary>
    /// 测试目的：内部服务注册应按 Provider Key 规范化，并拒绝相同 Key 的冲突实现。
    /// </summary>
    [Fact]
    public void AddSqlProviderRuntime_WhenProviderKeyConflicts_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlProviderRuntime(typeof(ITestService), typeof(DefaultService), " custom.provider ");

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() =>
            services.AddSqlProviderRuntime(typeof(ITestService), typeof(SqlServerService), "CUSTOM.PROVIDER"));
    }

    /// <summary>
    /// 测试服务契约。
    /// </summary>
    private interface ITestService
    {
    }

    /// <summary>
    /// 默认测试服务实现。
    /// </summary>
    private sealed class DefaultService : ITestService
    {
    }

    /// <summary>
    /// SQL Server 测试服务实现。
    /// </summary>
    private sealed class SqlServerService : ITestService
    {
    }

    /// <summary>
    /// 未映射的具体测试服务。
    /// </summary>
    private sealed class ConcreteService
    {
    }

}