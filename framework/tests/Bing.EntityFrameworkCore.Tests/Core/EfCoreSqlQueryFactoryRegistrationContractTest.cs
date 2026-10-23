using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.Datas.EntityFramework.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// EF Core SQL 查询工厂的兼容注册契约。
/// </summary>
public sealed class EfCoreSqlQueryFactoryRegistrationContractTest
{
    /// <summary>
    /// 测试目的：EF 适配器注册应保留调用方预注册的实现类型、委托、实例和 Singleton 生命周期。
    /// </summary>
    [Fact]
    public void AddEfCoreSqlQueryFactory_WhenFactoryIsPreRegistered_ShouldPreserveDescriptorAndResolution()
    {
        AssertTypeRegistrationIsPreserved();
        AssertFactoryRegistrationIsPreserved();
        AssertInstanceRegistrationIsPreserved();
    }

    /// <summary>
    /// 测试目的：调用方后注册 EF 适配器时应按 last-service 规则解析并保留集合数量。
    /// </summary>
    [Fact]
    public void AddEfCoreSqlQueryFactory_WhenFactoryIsRegisteredAfterDefault_ShouldResolveLast()
    {
        var services = CreateCoreServices();
        services.AddEfCoreSqlQueryFactory();
        var custom = new TestEfCoreSqlQueryFactory();
        services.AddSingleton<IEfCoreSqlQueryFactory>(custom);

        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = root.CreateScope();
        Assert.Same(custom, scope.ServiceProvider.GetRequiredService<IEfCoreSqlQueryFactory>());
        Assert.Equal(2, scope.ServiceProvider.GetServices<IEfCoreSqlQueryFactory>().Count());
    }

    private static void AssertTypeRegistrationIsPreserved()
    {
        var services = CreateCoreServices();
        services.Add(ServiceDescriptor.Singleton(typeof(IEfCoreSqlQueryFactory),
            typeof(TestEfCoreSqlQueryFactory)));
        services.AddEfCoreSqlQueryFactory();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IEfCoreSqlQueryFactory));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(TestEfCoreSqlQueryFactory), descriptor.ImplementationType);
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        var factoryA = scopeA.ServiceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();
        Assert.IsType<TestEfCoreSqlQueryFactory>(factoryA);
        Assert.Same(factoryA, scopeB.ServiceProvider.GetRequiredService<IEfCoreSqlQueryFactory>());
    }

    private static void AssertFactoryRegistrationIsPreserved()
    {
        var services = CreateCoreServices();
        var instance = new TestEfCoreSqlQueryFactory();
        Func<IServiceProvider, object> factory = _ => instance;
        services.Add(ServiceDescriptor.Singleton(typeof(IEfCoreSqlQueryFactory), factory));
        services.AddEfCoreSqlQueryFactory();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IEfCoreSqlQueryFactory));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(factory, descriptor.ImplementationFactory);
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = root.CreateScope();
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<IEfCoreSqlQueryFactory>());
    }

    private static void AssertInstanceRegistrationIsPreserved()
    {
        var services = CreateCoreServices();
        var instance = new TestEfCoreSqlQueryFactory();
        services.Add(ServiceDescriptor.Singleton(typeof(IEfCoreSqlQueryFactory), instance));
        services.AddEfCoreSqlQueryFactory();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IEfCoreSqlQueryFactory));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(instance, descriptor.ImplementationInstance);
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = root.CreateScope();
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<IEfCoreSqlQueryFactory>());
    }

    private static IServiceCollection CreateCoreServices()
    {
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=ef-registration-contract.db");
        return services;
    }

    /// <summary>
    /// 无资源的自定义适配器，用于验证 DI 注册语义。
    /// </summary>
    public sealed class TestEfCoreSqlQueryFactory : IEfCoreSqlQueryFactory
    {
        public ISqlQuery Create(UnitOfWorkBase unitOfWork,
            EfCoreSqlConnectionMode mode = EfCoreSqlConnectionMode.Shared, string dbKey = null) =>
            throw new NotSupportedException();
    }
}
