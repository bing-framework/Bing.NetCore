using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Events.Cap;
using Bing.Events.Messages;
using Bing.Uow;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Bing.EventBus.Tests.Cap;

/// <summary>
/// 验证事务型 CAP 发布入口的服务注册和参数契约。
/// </summary>
public sealed class CapUnitOfWorkPublisherContractTest
{
    [Fact]
    public void AddCapUnitOfWorkMessagePublisher_ShouldRegisterScopedPublisherOnce()
    {
        var services = CreateServices();
        Action<CapOptions, string> configureCap = ConfigureCap;

        services.AddCapUnitOfWorkMessagePublisher(builder => builder
            .AddDataSource("orders", DatabaseType.MySql, "Server=localhost;Database=orders;", configureCap));
        services.AddCapUnitOfWorkMessagePublisher(builder => builder
            .AddDataSource("orders", DatabaseType.MySql, "Server=localhost;Database=orders;", configureCap));

        Assert.Single(services, item => item.ServiceType == typeof(IUnitOfWorkMessagePublisher));
        Assert.Single(services, item => item.ServiceType == typeof(IHostedService) &&
                                        item.ImplementationType?.Name == "CapUnitOfWorkRuntimeHostedService");
    }

    [Fact]
    public void AddCapUnitOfWorkMessagePublisher_ShouldRegisterIdentityResolverForMinimalConsumerHost()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCapUnitOfWorkMessagePublisher(builder => builder.AddDataSource("orders",
            DatabaseType.MySql, "Server=127.0.0.1;Port=13307;Database=orders;", ConfigureCap));

        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<ISqlDatabaseIdentityResolver>();
        var identity = resolver.Resolve(DatabaseType.MySql,
            "Server=127.0.0.1;Port=13307;Database=orders;");

        Assert.IsType<DefaultSqlDatabaseIdentityResolver>(resolver);
        Assert.True(identity.IsComparable);
        Assert.Equal("orders", identity.Database);
        Assert.Contains(provider.GetServices<ISqlDatabaseIdentityContributor>(),
            contributor => contributor is DefaultSqlDatabaseIdentityContributor);
    }

    [Fact]
    public void AddCapUnitOfWorkMessagePublisher_ShouldPreserveHostIdentityResolver()
    {
        var services = new ServiceCollection();
        var resolver = new DefaultSqlDatabaseIdentityResolver();
        services.AddSingleton<ISqlDatabaseIdentityResolver>(resolver);
        services.AddCapUnitOfWorkMessagePublisher(builder => builder.AddDataSource("orders",
            DatabaseType.MySql, "Server=127.0.0.1;Port=13307;Database=orders;", ConfigureCap));

        using var provider = services.BuildServiceProvider();

        Assert.Same(resolver, provider.GetRequiredService<ISqlDatabaseIdentityResolver>());
    }

    [Fact]
    public void AddCapUnitOfWorkMessagePublisher_WhenSameSourceHasDifferentConfiguration_ShouldReject()
    {
        var services = CreateServices();
        services.AddCapUnitOfWorkMessagePublisher(builder => builder.AddDataSource("orders",
            DatabaseType.MySql, "Server=localhost;Database=orders;", ConfigureCap));

        Assert.Throws<InvalidOperationException>(() => services.AddCapUnitOfWorkMessagePublisher(builder =>
            builder.AddDataSource("orders", DatabaseType.MySql, "Server=localhost;Database=other;", ConfigureCap)));
    }

    [Fact]
    public async Task PublishAsync_WhenSendIsTrue_ShouldRejectBeforeResolvingAmbientUnitOfWork()
    {
        var manager = new Mock<IUnitOfWorkScopeManager>(MockBehavior.Strict);
        await using var provider = BuildProvider(manager.Object);
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync("orders",
            new Bing.Events.Messages.MessageEvent { Name = "sample.changed", Send = true }));

        manager.VerifyGet(item => item.Current, Times.Never);
    }

    [Fact]
    public async Task PublishAsync_WithoutAmbientUnitOfWork_ShouldReject()
    {
        var manager = new Mock<IUnitOfWorkScopeManager>();
        manager.SetupGet(item => item.Current).Returns((IUnitOfWorkScope)null);
        await using var provider = BuildProvider(manager.Object);
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync("orders",
            new Bing.Events.Messages.MessageEvent { Name = "sample.changed", Send = false }));

        Assert.Contains("事务型业务工作单元", exception.Message);
    }

    [Fact]
    public async Task PublishAsync_WithUnknownSource_ShouldRejectBeforeResolvingSqlResource()
    {
        var manager = new Mock<IUnitOfWorkScopeManager>();
        var root = new Mock<IUnitOfWorkScope>();
        root.SetupGet(item => item.IsActive).Returns(true);
        root.SetupGet(item => item.IsDisposed).Returns(false);
        root.SetupGet(item => item.Options).Returns(new UnitOfWorkScopeOptions { IsTransactional = true });
        manager.SetupGet(item => item.Current).Returns(root.Object);
        await using var provider = BuildProvider(manager.Object);
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync("missing",
            new Bing.Events.Messages.MessageEvent { Name = "sample.changed", Send = false }));

        Assert.Contains("未注册数据源 missing", exception.Message);
        Mock.Get(scope.ServiceProvider.GetRequiredService<ISqlUnitOfWorkResourceAccessor>())
            .Verify(item => item.GetResource(It.IsAny<IUnitOfWorkScope>(), It.IsAny<DatabaseContext>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_NonTransactionalOrTerminalRoot_ShouldRejectBeforeSqlResource()
    {
        foreach (var state in new[] { (Transactional: false, Active: true), (Transactional: true, Active: false) })
        {
            var root = new Mock<IUnitOfWorkScope>();
            root.SetupGet(item => item.IsActive).Returns(state.Active);
            root.SetupGet(item => item.IsDisposed).Returns(!state.Active);
            root.SetupGet(item => item.Options).Returns(new UnitOfWorkScopeOptions
                { IsTransactional = state.Transactional });
            var manager = new Mock<IUnitOfWorkScopeManager>();
            manager.SetupGet(item => item.Current).Returns(root.Object);
            await using var provider = BuildProvider(manager.Object);
            await using var scope = provider.CreateAsyncScope();
            var resourceAccessor = Mock.Get(scope.ServiceProvider.GetRequiredService<ISqlUnitOfWorkResourceAccessor>());
            var publisher = scope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync("orders",
                new Bing.Events.Messages.MessageEvent { Name = "sample.changed", Send = false }));
            resourceAccessor.Verify(item => item.GetResource(It.IsAny<IUnitOfWorkScope>(),
                It.IsAny<DatabaseContext>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        }
    }

    [Fact]
    public async Task PublishAsync_WhenPreCancelled_ShouldMarkRollbackOnlyBeforeResolvingSqlResource()
    {
        var root = new Mock<IUnitOfWorkScope>();
        root.SetupGet(item => item.IsActive).Returns(true);
        root.SetupGet(item => item.IsDisposed).Returns(false);
        root.SetupGet(item => item.Options).Returns(new UnitOfWorkScopeOptions { IsTransactional = true });
        var manager = new Mock<IUnitOfWorkScopeManager>();
        manager.SetupGet(item => item.Current).Returns(root.Object);
        await using var provider = BuildProvider(manager.Object);
        await using var scope = provider.CreateAsyncScope();
        var resourceAccessor = Mock.Get(scope.ServiceProvider.GetRequiredService<ISqlUnitOfWorkResourceAccessor>());
        var publisher = scope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublishAsync("orders",
            new Bing.Events.Messages.MessageEvent { Name = "sample.changed", Send = false }, cancellation.Token));

        root.Verify(item => item.SetRollbackOnly(), Times.Once);
        resourceAccessor.Verify(item => item.GetResource(It.IsAny<IUnitOfWorkScope>(),
            It.IsAny<DatabaseContext>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<ISqlUnitOfWorkResourceAccessor>());
        services.AddSingleton(Mock.Of<Bing.Data.Sql.IDatabaseContextAccessor>());
        services.AddSingleton(Mock.Of<Bing.Data.Sql.ISqlDatabaseIdentityResolver>());
        return services;
    }

    private static ServiceProvider BuildProvider(IUnitOfWorkScopeManager manager)
    {
        var services = CreateServices();
        services.AddSingleton(manager);
        services.AddCapUnitOfWorkMessagePublisher(builder => builder.AddDataSource("orders",
            DatabaseType.MySql, "Server=localhost;Database=orders;", ConfigureCap));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static void ConfigureCap(CapOptions options, string connectionString)
    {
    }
}
