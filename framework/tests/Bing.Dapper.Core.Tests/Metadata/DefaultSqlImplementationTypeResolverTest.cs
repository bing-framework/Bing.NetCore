using Bing.Data.Enums;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests.Metadata;

/// <summary>
/// <see cref="DefaultSqlImplementationTypeResolver"/> 单元测试。
/// </summary>
public class DefaultSqlImplementationTypeResolverTest
{
    /// <summary>
    /// 测试目的：数据库特定映射存在时应优先于默认映射返回。
    /// </summary>
    [Fact]
    public void Resolve_WhenProviderMappingExists_ShouldPreferProviderMapping()
    {
        // Arrange
        var options = new SqlImplementationTypeOptions();
        options.Map(typeof(ITestService), typeof(DefaultService));
        options.DatabaseMappings[SqlImplementationTypeOptions.GetKey(typeof(ITestService), DatabaseType.SqlServer)] = typeof(SqlServerService);
        var resolver = new DefaultSqlImplementationTypeResolver(options);

        // Act
        var result = resolver.Resolve(typeof(ITestService), DatabaseType.SqlServer);

        // Assert
        Assert.Equal(typeof(SqlServerService), result);
    }

    /// <summary>
    /// 测试目的：未配置当前 Provider 映射时应返回默认实现。
    /// </summary>
    [Fact]
    public void Resolve_WhenOnlyDefaultMappingExists_ShouldUseDefaultMapping()
    {
        // Arrange
        var options = new SqlImplementationTypeOptions();
        options.Map(typeof(ITestService), typeof(DefaultService));
        var resolver = new DefaultSqlImplementationTypeResolver(options);

        // Act
        var result = resolver.Resolve(typeof(ITestService), DatabaseType.MySql);

        // Assert
        Assert.Equal(typeof(DefaultService), result);
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
        Assert.Equal(typeof(ConcreteService), resolver.Resolve(typeof(ConcreteService)));
        Assert.Null(resolver.Resolve(typeof(AbstractService)));
        Assert.Null(resolver.Resolve(null));
    }

    /// <summary>
    /// 测试目的：映射时应同时登记服务类型和实现类型，并允许后注册覆盖。
    /// </summary>
    [Fact]
    public void Map_WhenCalledRepeatedly_ShouldRegisterBothTypesAndUseLatestImplementation()
    {
        // Arrange
        var options = new SqlImplementationTypeOptions();

        // Act
        options.Map(typeof(ITestService), typeof(DefaultService));
        options.Map(typeof(ITestService), typeof(SqlServerService), DatabaseType.SqlServer);

        // Assert
        Assert.Equal(typeof(SqlServerService), options.Mappings[typeof(ITestService)]);
        Assert.Equal(typeof(SqlServerService), options.Mappings[typeof(SqlServerService)]);
        Assert.Equal(typeof(SqlServerService), options.DatabaseMappings[SqlImplementationTypeOptions.GetKey(typeof(ITestService), DatabaseType.SqlServer)]);
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