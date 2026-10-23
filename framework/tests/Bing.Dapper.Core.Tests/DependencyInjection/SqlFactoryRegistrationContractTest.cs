using System.Data;
using Bing.Data.Sql;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Core.Tests.DependencyInjection;

/// <summary>
/// SQL 工厂兼容注册契约。
/// </summary>
public sealed class SqlFactoryRegistrationContractTest
{
    private static readonly Type[] FactoryServiceTypes =
    {
        typeof(ISqlQueryFactory),
        typeof(ISqlExecutorFactory),
        typeof(ISqlMultipleQueryExecutorFactory),
        typeof(ISqlTransactionScopeFactory)
    };

    /// <summary>
    /// 测试目的：AddSqlCore 应保留四类工厂预注册的实现类型、委托、实例及其 Singleton 生命周期。
    /// </summary>
    [Fact]
    public void AddSqlCore_WhenEachFactoryIsPreRegistered_ShouldPreserveDescriptorAndResolvedInstance()
    {
        foreach (var serviceType in FactoryServiceTypes)
        {
            AssertTypeRegistrationIsPreserved(serviceType);
            AssertFactoryRegistrationIsPreserved(serviceType);
            AssertInstanceRegistrationIsPreserved(serviceType);
        }
    }

    /// <summary>
    /// 测试目的：AddSqlCore 后追加的四类自定义工厂应成为最后解析项，并在集合中保留两项注册。
    /// </summary>
    [Fact]
    public void AddSqlCore_WhenFactoriesAreRegisteredAfterCore_ShouldResolveLastAndRetainCollectionEntries()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSqlCore();
        var custom = new TestSqlFactories();
        foreach (var serviceType in FactoryServiceTypes)
            services.Add(ServiceDescriptor.Singleton(serviceType, custom));

        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = root.CreateScope();
        foreach (var serviceType in FactoryServiceTypes)
        {
            Assert.Same(custom, scope.ServiceProvider.GetRequiredService(serviceType));
            Assert.Equal(2, scope.ServiceProvider.GetServices(serviceType).Count());
        }
    }

    private static void AssertTypeRegistrationIsPreserved(Type serviceType)
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.Singleton(serviceType, typeof(TestSqlFactories)));
        services.AddSqlCore();

        var descriptor = Assert.Single(services, item => item.ServiceType == serviceType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(TestSqlFactories), descriptor.ImplementationType);

        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        var instanceA = scopeA.ServiceProvider.GetRequiredService(serviceType);
        Assert.IsType<TestSqlFactories>(instanceA);
        Assert.Same(instanceA, scopeB.ServiceProvider.GetRequiredService(serviceType));
    }

    private static void AssertFactoryRegistrationIsPreserved(Type serviceType)
    {
        IServiceCollection services = new ServiceCollection();
        var instance = new TestSqlFactories();
        Func<IServiceProvider, object> factory = _ => instance;
        services.Add(ServiceDescriptor.Singleton(serviceType, factory));
        services.AddSqlCore();

        var descriptor = Assert.Single(services, item => item.ServiceType == serviceType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(factory, descriptor.ImplementationFactory);

        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = root.CreateScope();
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService(serviceType));
    }

    private static void AssertInstanceRegistrationIsPreserved(Type serviceType)
    {
        IServiceCollection services = new ServiceCollection();
        var instance = new TestSqlFactories();
        services.Add(ServiceDescriptor.Singleton(serviceType, instance));
        services.AddSqlCore();

        var descriptor = Assert.Single(services, item => item.ServiceType == serviceType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(instance, descriptor.ImplementationInstance);

        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = root.CreateScope();
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService(serviceType));
    }

    /// <summary>
    /// 用于兼容注册测试的无资源工厂实现。
    /// </summary>
    public sealed class TestSqlFactories : ISqlQueryFactory, ISqlExecutorFactory,
        ISqlMultipleQueryExecutorFactory, ISqlTransactionScopeFactory
    {
        ISqlQuery ISqlQueryFactory.Create(string dbKey) => throw new NotSupportedException();

        ISqlExecutor ISqlExecutorFactory.Create(string dbKey) => throw new NotSupportedException();

        ISqlMultipleQueryExecutor ISqlMultipleQueryExecutorFactory.Create(string dbKey) =>
            throw new NotSupportedException();

        public ISqlTransactionScope Begin() => throw new NotSupportedException();

        public ISqlTransactionScope Begin(string dataSourceKey) => throw new NotSupportedException();

        public ISqlTransactionScope Begin(string dataSourceKey, IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        public Task<ISqlTransactionScope> BeginAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ISqlTransactionScope> BeginAsync(string dataSourceKey,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ISqlTransactionScope> BeginAsync(string dataSourceKey, IsolationLevel isolationLevel,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
