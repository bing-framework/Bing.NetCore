using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// 验证业务工作单元范围的 DI 注册契约。
/// </summary>
public class UnitOfWorkScopeRegistrationTest
{
    /// <summary>
    /// 验证注册幂等、保留预注册实现，并且默认管理器按 DI scope 隔离。
    /// </summary>
    [Fact]
    public void Registration_ShouldPreserveCustomServicesAndBeScoped()
    {
        var customManager = Mock.Of<IUnitOfWorkScopeManager>();
        var customLegacyManager = Mock.Of<IUnitOfWorkManager>();
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWorkScopeManager>(_ => customManager);
        services.AddScoped<IUnitOfWorkManager>(_ => customLegacyManager);
        services.AddUnitOfWorkScopes();
        services.AddUnitOfWorkScopes();

        services.Count(descriptor => descriptor.ServiceType == typeof(IUnitOfWorkScopeManager)).ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        ReferenceEquals(customManager, firstScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>())
            .ShouldBeTrue();
        ReferenceEquals(customLegacyManager, firstScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>())
            .ShouldBeTrue();
        firstScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>()
            .ShouldBeSameAs(secondScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>());
    }

    /// <summary>
    /// 验证已有类型注册不会被默认 scope 管理器覆盖。
    /// </summary>
    [Fact]
    public void Registration_ShouldPreserveImplementationType()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWorkScopeManager, TestScopeManager>();

        services.AddUnitOfWorkScopes();

        var descriptor = services.Single(item => item.ServiceType == typeof(IUnitOfWorkScopeManager));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        descriptor.ImplementationType.ShouldBe(typeof(TestScopeManager));
    }

    /// <summary>
    /// 验证默认管理器在单个 DI scope 内复用，在不同 scope 间隔离。
    /// </summary>
    [Fact]
    public void Registration_DefaultManager_ShouldBeScoped()
    {
        var services = new ServiceCollection();
        services.AddUnitOfWorkScopes();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();

        ReferenceEquals(first, firstScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>())
            .ShouldBeTrue();
        ReferenceEquals(first, secondScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>())
            .ShouldBeFalse();
    }

    /// <summary>
    /// 验证显式操作注册不覆盖旧自定义管理器，并在使用新入口时明确拒绝缺少的新契约。
    /// </summary>
    [Fact]
    public void Registration_BingOperations_ShouldRejectLegacyOnlyCustomManager()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWorkScopeManager, TestScopeManager>();
        services.AddBingOperations();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Should.Throw<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>());

        exception.Message.ShouldContain(nameof(IUnitOfWorkOperationManager));
        scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>()
            .ShouldBeOfType<TestScopeManager>();
    }

    /// <summary>
    /// 验证空服务集合会明确抛出参数异常。
    /// </summary>
    [Fact]
    public void Registration_WithNullServices_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() =>
            UnitOfWorkScopeServiceCollectionExtensions.AddUnitOfWorkScopes(null));
    }

    private sealed class TestScopeManager : IUnitOfWorkScopeManager
    {
        public IUnitOfWorkScope Current => null;
        public IUnitOfWorkScope Begin(UnitOfWorkScopeOptions options = null, bool requiresNew = false) => null;
    }
}
