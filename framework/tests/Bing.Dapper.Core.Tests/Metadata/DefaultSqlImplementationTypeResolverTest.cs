using Bing.Data.Sql;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Core.Tests.Metadata;

/// <summary>
/// <see cref="DefaultSqlImplementationTypeResolver"/> 单元测试。
/// </summary>
public class DefaultSqlImplementationTypeResolverTest
{
    /// <summary>
    /// 测试目的：Provider Key 映射存在时应返回对应实现，避免相同数据库类型的 Provider 相互覆盖。
    /// </summary>
    [Fact]
    public void Resolve_WhenProviderMappingExists_ShouldPreferProviderMapping()
    {
        // Arrange
        var options = new SqlImplementationTypeOptions();
        options.Map(typeof(ITestService), typeof(DefaultService), "custom.sqlserver.first");
        options.Map(typeof(ITestService), typeof(SqlServerService), "custom.sqlserver.second");
        var resolver = new DefaultSqlImplementationTypeResolver(options);

        // Act
        var result = resolver.Resolve(typeof(ITestService), "custom.sqlserver.second");

        // Assert
        Assert.Equal(typeof(SqlServerService), result);
    }

    /// <summary>
    /// 测试目的：未配置当前 Provider 映射时不得回退到其他 Provider 的默认实现。
    /// </summary>
    [Fact]
    public void Resolve_WhenOnlyDefaultMappingExists_ShouldUseDefaultMapping()
    {
        // Arrange
        var options = new SqlImplementationTypeOptions();
        options.Map(typeof(ITestService), typeof(DefaultService), "custom.mysql");
        var resolver = new DefaultSqlImplementationTypeResolver(options);

        // Act
        var result = resolver.Resolve(typeof(ITestService), "custom.postgresql");

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// 测试目的：未映射的具体类型应作为自身返回，抽象类型和空类型应返回 null。
    /// </summary>
    [Fact]
    public void Resolve_WhenTypeIsConcreteAbstractOrNull_ShouldReturnExpectedResult()
    {
        // Arrange
        var resolver = new DefaultSqlImplementationTypeResolver();

        // Act and Assert
        Assert.Equal(typeof(ConcreteService), resolver.Resolve(typeof(ConcreteService), "custom.provider"));
        Assert.Null(resolver.Resolve(typeof(AbstractService), "custom.provider"));
        Assert.Null(resolver.Resolve(null, "custom.provider"));
    }

    /// <summary>
    /// 测试目的：映射应登记服务类型和实现类型，并拒绝同一 Provider Key 下的不同实现。
    /// </summary>
    [Fact]
    public void Map_WhenCalledRepeatedly_ShouldRegisterBothTypesAndUseLatestImplementation()
    {
        // Arrange
        var options = new SqlImplementationTypeOptions();

        // Act
        options.Map(typeof(ITestService), typeof(SqlServerService), "custom.sqlserver");

        // Assert
        Assert.Equal(typeof(SqlServerService), options.ProviderMappings[
            SqlImplementationTypeOptions.GetKey(typeof(ITestService), "CUSTOM.SQLSERVER")]);
        Assert.Equal(typeof(SqlServerService), options.ProviderMappings[
            SqlImplementationTypeOptions.GetKey(typeof(SqlServerService), "custom.sqlserver")]);
        Assert.Throws<InvalidOperationException>(() =>
            options.Map(typeof(ITestService), typeof(DefaultService), "custom.sqlserver"));
    }

    /// <summary>
    /// 测试目的：服务集合中存在多个实现类型 Options 注册时不得依赖最后注册项，应明确拒绝歧义配置。
    /// </summary>
    [Fact]
    public void AddSqlImplementationType_WhenOptionsRegisteredMultipleTimes_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton(new SqlImplementationTypeOptions());
        services.AddSingleton(new SqlImplementationTypeOptions());

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() =>
            Bing.Dapper.DapperCoreServiceCollectionExtensions.AddSqlImplementationType<ITestService, DefaultService>(
                services, "custom.provider"));
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

    /// <summary>
    /// 未映射的抽象测试服务。
    /// </summary>
    private abstract class AbstractService
    {
    }
}