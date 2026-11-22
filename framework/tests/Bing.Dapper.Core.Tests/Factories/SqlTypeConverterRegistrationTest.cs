using System;
using System.Data;
using System.Linq;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Tests.Factories;

/// <summary>验证通用数据库类型转换器注册的裁剪契约与依赖注入行为。</summary>
public class SqlTypeConverterRegistrationTest
{
    [Fact]
    public void GenericRegistration_PreservesPublicConstructorsForTrimming()
    {
        var parameter = typeof(DapperCoreServiceCollectionExtensions)
            .GetMethod(nameof(DapperCoreServiceCollectionExtensions.AddDatabaseTypeConverter))
            .GetGenericArguments().Single();
        var attribute = Assert.Single(parameter.GetCustomAttributesData(), item =>
            item.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute");
        Assert.Equal(3, Convert.ToInt32(attribute.ConstructorArguments.Single().Value));
    }

    [Fact]
    public void GenericRegistration_UsesConstructorInjectionAndSharesSingleton()
    {
        var services = new ServiceCollection();
        var dependency = new ConverterDependency();
        services.AddSingleton(dependency);
        services.AddSqlCore();
        services.AddDatabaseTypeConverter<InjectedConverter>(DatabaseType.SqlServer);
        services.AddDatabaseTypeConverter<InjectedConverter>(DatabaseType.Oracle);
        using var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<ITypeConverterResolver>();
        var converter = Assert.IsType<InjectedConverter>(resolver.Resolve(DatabaseType.SqlServer));
        Assert.Same(dependency, converter.Dependency);
        Assert.Same(converter, resolver.Resolve(DatabaseType.Oracle));
        Assert.Equal(DbType.Guid, converter.ToDbType("probe"));
    }

    [Fact]
    public void GenericRegistration_PreservesPreRegisteredInstance()
    {
        var services = new ServiceCollection();
        var converter = new InjectedConverter(new ConverterDependency());
        services.AddSingleton(converter);
        services.AddSqlCore();
        services.AddDatabaseTypeConverter<InjectedConverter>(DatabaseType.SqlServer);
        using var provider = services.BuildServiceProvider();

        Assert.Same(converter, provider.GetRequiredService<ITypeConverterResolver>().Resolve(DatabaseType.SqlServer));
    }

    public sealed class ConverterDependency { }

    public sealed class InjectedConverter : ITypeConverter
    {
        public InjectedConverter(ConverterDependency dependency) => Dependency = dependency;
        public ConverterDependency Dependency { get; }
        public DbType? ToDbType(string dataType, int? length = null) => dataType == "probe" ? DbType.Guid : null;
    }
}
