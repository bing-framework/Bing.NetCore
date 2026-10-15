using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tests.DependencyInjection;

/// <summary>
/// 验证约定注册的接口选择与实例所有权。
/// </summary>
public class DependencyRegistrationBehaviorTest
{
    /// <summary>
    /// 验证异步释放接口不会被注册为业务服务。
    /// </summary>
    [Fact]
    public void Register_ShouldExcludeAsyncDisposableFromServiceInterfaces()
    {
        var services = new ServiceCollection();
        new TestDependencyModule().Register(services, typeof(AsyncResource));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IResource));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IAsyncDisposable));
    }

    /// <summary>
    /// 验证开放泛型多接口会在写入描述符前被拒绝。
    /// </summary>
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    public void Register_OpenGenericAliases_ShouldFailBeforePartialRegistration(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        var implementation = lifetime == ServiceLifetime.Singleton
            ? typeof(SingletonGenericAliases<>) : typeof(ScopedGenericAliases<>);

        var error = Assert.Throws<InvalidOperationException>(() =>
            new TestDependencyModule().Register(services, implementation));

        Assert.Contains(implementation.Name.Split('`')[0], error.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    /// <summary>
    /// 验证单接口开放泛型仍由容器按封闭类型解析。
    /// </summary>
    [Fact]
    public void Register_OpenGenericSingleInterface_ShouldResolveClosedTypes()
    {
        var services = new ServiceCollection();
        new TestDependencyModule().Register(services, typeof(SingletonGenericSingle<>));
        using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IGenericPrimary<int>>(),
            provider.GetRequiredService<IGenericPrimary<int>>());
        Assert.NotSame(provider.GetRequiredService<IGenericPrimary<int>>(),
            provider.GetRequiredService<IGenericPrimary<string>>());
    }

    /// <summary>
    /// 验证开放泛型 TryAdd 跳过主接口后仍可直接注册次接口。
    /// </summary>
    [Fact]
    public void Register_OpenGenericTryAdd_WithExistingPrimary_ShouldRegisterSecondaryDirectly()
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        var serviceTypes = module.ServicesFor(typeof(TryAddGenericAliases<>));
        services.AddSingleton(serviceTypes[0], typeof(ExistingGenericAlias<>));
        module.Register(services, typeof(TryAddGenericAliases<>));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<ExistingGenericAlias<int>>(provider.GetRequiredService(serviceTypes[0].MakeGenericType(typeof(int))));
        Assert.IsType<TryAddGenericAliases<int>>(provider.GetRequiredService(serviceTypes[1].MakeGenericType(typeof(int))));
    }

    /// <summary>
    /// 验证作用域开放泛型 TryAdd 跳过主接口后仍可直接注册次接口。
    /// </summary>
    [Fact]
    public void Register_ScopedOpenGenericTryAdd_WithExistingPrimary_ShouldRegisterSecondaryDirectly()
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        var serviceTypes = module.ServicesFor(typeof(ScopedTryAddGenericAliases<>));
        services.AddScoped(serviceTypes[0], typeof(ExistingGenericAlias<>));

        module.Register(services, typeof(ScopedTryAddGenericAliases<>));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<ExistingGenericAlias<int>>(
            scope.ServiceProvider.GetRequiredService(serviceTypes[0].MakeGenericType(typeof(int))));
        Assert.IsType<ScopedTryAddGenericAliases<int>>(
            scope.ServiceProvider.GetRequiredService(serviceTypes[1].MakeGenericType(typeof(int))));
    }

    /// <summary>
    /// 验证开放泛型 TryAdd 在两个接口已有注册时不产生新描述符。
    /// </summary>
    [Fact]
    public void Register_OpenGenericTryAdd_WithBothInterfacesExisting_ShouldSkipRegistration()
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        var serviceTypes = module.ServicesFor(typeof(TryAddGenericAliases<>));
        services.AddSingleton(serviceTypes[0], typeof(ExistingGenericAlias<>));
        services.AddSingleton(serviceTypes[1], typeof(ExistingGenericAlias<>));
        var count = services.Count;

        module.Register(services, typeof(TryAddGenericAliases<>));

        Assert.Equal(count, services.Count);
    }

    /// <summary>
    /// 验证作用域开放泛型 TryAdd 在两个接口已有注册时不产生新描述符。
    /// </summary>
    [Fact]
    public void Register_ScopedOpenGenericTryAdd_WithBothInterfacesExisting_ShouldSkipRegistration()
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        var serviceTypes = module.ServicesFor(typeof(ScopedTryAddGenericAliases<>));
        services.AddScoped(serviceTypes[0], typeof(ExistingGenericAlias<>));
        services.AddScoped(serviceTypes[1], typeof(ExistingGenericAlias<>));
        var count = services.Count;

        module.Register(services, typeof(ScopedTryAddGenericAliases<>));

        Assert.Equal(count, services.Count);
    }

    /// <summary>
    /// 验证开放泛型 TryAdd 在首接口跳过后仍会拒绝后续共享工厂。
    /// </summary>
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    public void Register_OpenGenericTryAdd_WithExistingPrimaryAndTwoPendingInterfaces_ShouldFailBeforeMutation(
        ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        var implementation = lifetime == ServiceLifetime.Singleton
            ? typeof(TryAddGenericTriples<>) : typeof(ScopedTryAddGenericTriples<>);
        var serviceTypes = module.ServicesFor(implementation);
        ((IServiceCollection)services).Add(
            ServiceDescriptor.Describe(serviceTypes[0], typeof(ExistingGenericTriple<>), lifetime));
        var count = services.Count;

        var error = Assert.Throws<InvalidOperationException>(() => module.Register(services, implementation));

        Assert.Contains(implementation.Name.Split('`')[0], error.Message, StringComparison.Ordinal);
        Assert.Equal(count, services.Count);
        Assert.Equal(serviceTypes[0], services[0].ServiceType);
    }

    /// <summary>
    /// 验证开放泛型 TryAdd 在所有接口已有注册时保持原描述符集合。
    /// </summary>
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    public void Register_OpenGenericTryAdd_WithAllInterfacesExisting_ShouldSkipRegistration(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        var implementation = lifetime == ServiceLifetime.Singleton
            ? typeof(TryAddGenericTriples<>) : typeof(ScopedTryAddGenericTriples<>);
        var serviceTypes = module.ServicesFor(implementation);
        foreach (var serviceType in serviceTypes)
            ((IServiceCollection)services).Add(
                ServiceDescriptor.Describe(serviceType, typeof(ExistingGenericTriple<>), lifetime));
        var count = services.Count;

        module.Register(services, implementation);

        Assert.Equal(count, services.Count);
    }

    /// <summary>
    /// 验证开放泛型 TryAdd 仅剩一个普通接口时可直接注册该接口。
    /// </summary>
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    public void Register_OpenGenericTryAdd_WithOnlyOnePendingInterface_ShouldRegisterDirectly(
        ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        var implementation = lifetime == ServiceLifetime.Singleton
            ? typeof(TryAddGenericTriples<>) : typeof(ScopedTryAddGenericTriples<>);
        var serviceTypes = module.ServicesFor(implementation);
        ((IServiceCollection)services).Add(
            ServiceDescriptor.Describe(serviceTypes[0], typeof(ExistingGenericTriple<>), lifetime));
        ((IServiceCollection)services).Add(
            ServiceDescriptor.Describe(serviceTypes[1], typeof(ExistingGenericTriple<>), lifetime));

        module.Register(services, implementation);

        Assert.Equal(3, services.Count);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = lifetime == ServiceLifetime.Scoped ? provider.CreateScope() : null;
        var serviceProvider = scope?.ServiceProvider ?? provider;
        Assert.IsType<ExistingGenericTriple<int>>(
            serviceProvider.GetRequiredService(serviceTypes[0].MakeGenericType(typeof(int))));
        Assert.IsType<ExistingGenericTriple<int>>(
            serviceProvider.GetRequiredService(serviceTypes[1].MakeGenericType(typeof(int))));
        Assert.IsType(implementation.MakeGenericType(typeof(int)),
            serviceProvider.GetRequiredService(serviceTypes[2].MakeGenericType(typeof(int))));
    }

    /// <summary>
    /// 验证开放泛型首接口为多重注入时，在生成共享工厂前拒绝注册。
    /// </summary>
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    public void Register_OpenGenericMultipleDependencyFirst_ShouldFailBeforeMutation(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        var implementation = lifetime == ServiceLifetime.Singleton
            ? typeof(MultipleFirstGenericAliases<>) : typeof(ScopedMultipleFirstGenericAliases<>);
        var serviceTypes = module.ServicesFor(implementation);

        Assert.Equal(typeof(IGenericMultiplePrimary<>), serviceTypes[0]);
        Assert.Equal(typeof(IGenericMultipleSecondary<>), serviceTypes[1]);

        var error = Assert.Throws<InvalidOperationException>(() => module.Register(services, implementation));

        Assert.Contains(implementation.Name.Split('`')[0], error.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    /// <summary>
    /// 验证 TryAdd 跳过首接口后，中间多重接口不会建立共享入口。
    /// </summary>
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    public void Register_OpenGenericTryAdd_WithMultipleDependencyMiddle_ShouldRegisterRemainingDirectly(
        ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        var implementation = lifetime == ServiceLifetime.Singleton
            ? typeof(TryAddGenericMultipleMiddle<>) : typeof(ScopedTryAddGenericMultipleMiddle<>);
        var serviceTypes = module.ServicesFor(implementation);

        Assert.Equal(typeof(IGenericPrimary<>), serviceTypes[0]);
        Assert.Equal(typeof(IGenericMultipleMiddle<>), serviceTypes[1]);
        Assert.Equal(typeof(IGenericTertiary<>), serviceTypes[2]);
        ((IServiceCollection)services).Add(ServiceDescriptor.Describe(
            serviceTypes[0], typeof(ExistingGenericAlias<>), lifetime));

        module.Register(services, implementation);

        Assert.Equal(3, services.Count);
        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationFactory != null);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = lifetime == ServiceLifetime.Scoped ? provider.CreateScope() : null;
        var serviceProvider = scope?.ServiceProvider ?? provider;
        Assert.IsType(implementation.MakeGenericType(typeof(int)), serviceProvider.GetRequiredService(
            serviceTypes[1].MakeGenericType(typeof(int))));
        Assert.IsType(implementation.MakeGenericType(typeof(int)), serviceProvider.GetRequiredService(
            serviceTypes[2].MakeGenericType(typeof(int))));
    }

    /// <summary>
    /// 验证开放泛型替换主接口且需要共享次接口时快速失败。
    /// </summary>
    [Fact]
    public void Register_OpenGenericReplaceExisting_WhenSharedFactoryRequired_ShouldFailBeforeMutation()
    {
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new TestDependencyModule().Register(services, typeof(ReplaceGenericAliases<>)));

        Assert.Contains(typeof(ReplaceGenericAliases<>).Name.Split('`')[0], error.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    /// <summary>
    /// 验证作用域开放泛型替换注册需要共享工厂时在写入前失败。
    /// </summary>
    [Fact]
    public void Register_ScopedOpenGenericReplaceExisting_WhenSharedFactoryRequired_ShouldFailBeforeMutation()
    {
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new TestDependencyModule().Register(services, typeof(ScopedReplaceGenericAliases<>)));

        Assert.Contains(typeof(ScopedReplaceGenericAliases<>).Name.Split('`')[0], error.Message,
            StringComparison.Ordinal);
        Assert.Empty(services);
    }

    /// <summary>
    /// 验证主接口尝试添加失败时，次接口仍解析当前实现。
    /// </summary>
    [Fact]
    public void Register_TryAddPrimaryExists_ShouldNotForwardSecondaryToOtherImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPrimary, ExistingPrimary>();
        new TestDependencyModule().Register(services, typeof(SecondaryImplementation));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<ExistingPrimary>(provider.GetRequiredService<IPrimary>());
        Assert.IsType<SecondaryImplementation>(provider.GetRequiredService<ISecondary>());
    }

    /// <summary>
    /// 验证多个接口在单一作用域内解析同一实例。
    /// </summary>
    [Fact]
    public void Register_ScopedInterfaces_ShouldShareWithinScope()
    {
        var services = new ServiceCollection();
        new TestDependencyModule().Register(services, typeof(ScopedImplementation));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<IPrimary>();
        Assert.Same(first, firstScope.ServiceProvider.GetRequiredService<ISecondary>());
        Assert.NotSame(first, secondScope.ServiceProvider.GetRequiredService<IPrimary>());
    }

    /// <summary>
    /// 验证接口转发时容器的实际释放次数。
    /// </summary>
    [Fact]
    public async Task Register_AsyncResourceAliases_ShouldRecordActualDisposalCount()
    {
        var services = new ServiceCollection();
        new TestDependencyModule().Register(services, typeof(AsyncResource));
        var provider = services.BuildServiceProvider();
        var resource = (AsyncResource)provider.GetRequiredService<IResource>();

        await provider.DisposeAsync();

        Assert.Equal(1, resource.AsyncDisposeCount);
    }

    /// <summary>
    /// 验证异步可释放资源通过两个业务接口解析时的实际释放次数。
    /// </summary>
    [Fact]
    public async Task Register_AsyncBusinessAliases_ShouldExposeDuplicateDisposalLimitation()
    {
        var services = new ServiceCollection();
        new TestDependencyModule().Register(services, typeof(AsyncBusinessAliases));
        var provider = services.BuildServiceProvider();
        var primary = provider.GetRequiredService<IPrimary>();
        Assert.Same(primary, provider.GetRequiredService<ISecondary>());

        await provider.DisposeAsync();

        Assert.Equal(2, ((AsyncBusinessAliases)primary).DisposeCount);
    }

    /// <summary>
    /// 验证显式注册自身与接口形成各自的单例。
    /// </summary>
    [Fact]
    public void Register_AddSelf_ShouldExposeSelfAndInterface()
    {
        var services = new ServiceCollection();
        new TestDependencyModule().Register(services, typeof(SelfRegistered));
        var provider = services.BuildServiceProvider();
        var byInterface = Assert.IsType<SelfRegistered>(provider.GetRequiredService<IResource>());
        var bySelf = provider.GetRequiredService<SelfRegistered>();
        Assert.NotSame(byInterface, bySelf);
        Assert.Same(byInterface, provider.GetRequiredService<IResource>());
        Assert.Same(bySelf, provider.GetRequiredService<SelfRegistered>());

        provider.Dispose();
        Assert.Equal(1, byInterface.DisposeCount);
        Assert.Equal(1, bySelf.DisposeCount);
    }

    /// <summary>
    /// 验证多重注入保留同一接口下的各个实现。
    /// </summary>
    [Fact]
    public void Register_MultipleDependency_ShouldKeepAllImplementations()
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        module.Register(services, typeof(MultipleA));
        module.Register(services, typeof(MultipleB));
        using var provider = services.BuildServiceProvider();

        Assert.Collection(provider.GetServices<IMultiple>(),
            item => Assert.IsType<MultipleA>(item), item => Assert.IsType<MultipleB>(item));
    }

    /// <summary>
    /// 验证替换注册仅保留新的默认实现。
    /// </summary>
    [Fact]
    public void Register_ReplaceExisting_ShouldReplacePrimary()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPrimary, ExistingPrimary>();
        new TestDependencyModule().Register(services, typeof(ReplacingPrimary));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<ReplacingPrimary>(provider.GetRequiredService<IPrimary>());
    }

    /// <summary>
    /// 验证瞬时服务的尝试添加和替换标记。
    /// </summary>
    [Fact]
    public void Register_TransientFlags_ShouldHonorTryAddAndReplaceExisting()
    {
        var services = new ServiceCollection();
        services.AddTransient<ITransientSlot, ExistingTransient>();
        var module = new TestDependencyModule();
        module.Register(services, typeof(TryAddTransient));
        Assert.Single(services, item => item.ServiceType == typeof(ITransientSlot));

        module.Register(services, typeof(ReplaceTransient));
        using var provider = services.BuildServiceProvider();
        Assert.IsType<ReplaceTransient>(provider.GetRequiredService<ITransientSlot>());
        Assert.NotSame(provider.GetRequiredService<ITransientSlot>(),
            provider.GetRequiredService<ITransientSlot>());
        Assert.Single(provider.GetServices<ITransientSlot>());
    }

    /// <summary>
    /// 验证后续替换主接口时，次接口不会静默返回其他实现。
    /// </summary>
    [Fact]
    public void Register_PrimaryReplacedLater_ShouldRejectStaleAlias()
    {
        var services = new ServiceCollection();
        var module = new TestDependencyModule();
        module.Register(services, typeof(SecondaryImplementation));
        module.Register(services, typeof(ReplacingPrimary));
        using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ISecondary>());
        Assert.Contains(nameof(SecondaryImplementation), error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证调用方传入的实例由调用方释放。
    /// </summary>
    [Fact]
    public void SuppliedInstance_ShouldRemainCallerOwned()
    {
        var resource = new SyncResource();
        var services = new ServiceCollection();
        services.AddSingleton<IResource>(resource);
        using (var provider = services.BuildServiceProvider())
            Assert.Same(resource, provider.GetRequiredService<IResource>());

        Assert.Equal(0, resource.DisposeCount);
        resource.Dispose();
        Assert.Equal(1, resource.DisposeCount);
    }

    /// <summary>
    /// 验证同时实现两种释放接口的容器服务仅执行异步路径。
    /// </summary>
    [Fact]
    public async Task OwnedDualDisposable_ShouldUseAsyncPathOnce()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DualResource>();
        var provider = services.BuildServiceProvider();
        var resource = provider.GetRequiredService<DualResource>();

        await provider.DisposeAsync();

        Assert.Equal(0, resource.DisposeCount);
        Assert.Equal(1, resource.AsyncDisposeCount);
    }

    /// <summary>
    /// 验证同步容器释放只调用双路径资源的同步方法。
    /// </summary>
    [Fact]
    public void OwnedDualDisposable_ShouldUseSyncPathOnce()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DualResource>();
        var provider = services.BuildServiceProvider();
        var resource = provider.GetRequiredService<DualResource>();

        provider.Dispose();

        Assert.Equal(1, resource.DisposeCount);
        Assert.Equal(0, resource.AsyncDisposeCount);
    }

    /// <summary>
    /// 记录底层容器将两个接口工厂返回的同一实例释放两次。
    /// </summary>
    [Fact]
    public async Task Register_DisposableBusinessAliases_ShouldExposeDuplicateDisposalLimitation()
    {
        var services = new ServiceCollection();
        new TestDependencyModule().Register(services, typeof(DisposableAliases));
        var provider = services.BuildServiceProvider();
        var primary = provider.GetRequiredService<IPrimary>();
        Assert.Same(primary, provider.GetRequiredService<ISecondary>());

        await provider.DisposeAsync();

        Assert.Equal(2, ((DisposableAliases)primary).DisposeCount);
    }

    /// <summary>
    /// 验证非释放适配器使共享资源只由一个服务描述符拥有。
    /// </summary>
    [Fact]
    public void ManualAdapters_ShouldDisposeSharedOwnerOnce()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AdapterOwner>();
        services.AddSingleton<IPrimary>(provider => new PrimaryAdapter(provider.GetRequiredService<AdapterOwner>()));
        services.AddSingleton<ISecondary>(provider => new SecondaryAdapter(provider.GetRequiredService<AdapterOwner>()));
        var container = services.BuildServiceProvider();
        var primary = (PrimaryAdapter)container.GetRequiredService<IPrimary>();
        var secondary = (SecondaryAdapter)container.GetRequiredService<ISecondary>();
        Assert.Same(primary.Owner, secondary.Owner);

        container.Dispose();

        Assert.Equal(1, primary.Owner.DisposeCount);
    }

    /// <summary>
    /// 测试约定注册入口。
    /// </summary>
    private sealed class TestDependencyModule : DependencyModule
    {
        /// <summary>
        /// 注册指定实现。
        /// </summary>
        public void Register(IServiceCollection services, Type implementationType) =>
            AddToServices(services, implementationType);

        /// <summary>
        /// 获取实现类型的可注册接口顺序。
        /// </summary>
        public Type[] ServicesFor(Type implementationType) => GetImplementedInterfaces(implementationType);

        /// <inheritdoc />
        protected override ServiceLifetime? GetLifetimeOrNull(Type type) => type == typeof(SingletonGenericAliases<>) ||
            type == typeof(SingletonGenericSingle<>) ? ServiceLifetime.Singleton :
            type == typeof(ScopedGenericAliases<>) ? ServiceLifetime.Scoped : base.GetLifetimeOrNull(type);
    }

    /// <summary>
    /// 测试资源契约。
    /// </summary>
    private interface IResource { }

    /// <summary>
    /// 测试主接口。
    /// </summary>
    private interface IPrimary { }

    /// <summary>
    /// 测试次接口。
    /// </summary>
    private interface ISecondary { }

    /// <summary>
    /// 瞬时服务插槽。
    /// </summary>
    private interface ITransientSlot { }

    /// <summary>
    /// 已有瞬时实现。
    /// </summary>
    private sealed class ExistingTransient : ITransientSlot { }

    /// <summary>
    /// 只在插槽为空时注册的瞬时实现。
    /// </summary>
    [Dependency(ServiceLifetime.Transient, TryAdd = true)]
    private sealed class TryAddTransient : ITransientSlot { }

    /// <summary>
    /// 替换已有插槽的瞬时实现。
    /// </summary>
    [Dependency(ServiceLifetime.Transient, ReplaceExisting = true)]
    private sealed class ReplaceTransient : ITransientSlot { }

    /// <summary>
    /// 开放泛型主接口。
    /// </summary>
    private interface IGenericPrimary<T> { }

    /// <summary>
    /// 开放泛型次接口。
    /// </summary>
    private interface IGenericSecondary<T> { }

    /// <summary>
    /// 开放泛型第三接口。
    /// </summary>
    private interface IGenericTertiary<T> { }

    /// <summary>
    /// 开放泛型多重注入主接口。
    /// </summary>
    [MultipleDependency]
    private interface IGenericMultiplePrimary<T> { }

    /// <summary>
    /// 开放泛型多重注入次接口。
    /// </summary>
    private interface IGenericMultipleSecondary<T> { }

    /// <summary>
    /// 开放泛型中间多重注入接口。
    /// </summary>
    [MultipleDependency]
    private interface IGenericMultipleMiddle<T> { }

    /// <summary>
    /// 已有开放泛型主接口实现。
    /// </summary>
    /// <summary>
    /// 已有开放泛型多接口实现。
    /// </summary>
    [IgnoreDependency]
    private sealed class ExistingGenericAlias<T> : IGenericPrimary<T>, IGenericSecondary<T> { }

    /// <summary>
    /// 已有开放泛型三接口实现。
    /// </summary>
    [IgnoreDependency]
    private sealed class ExistingGenericTriple<T> : IGenericPrimary<T>, IGenericSecondary<T>, IGenericTertiary<T> { }

    /// <summary>
    /// 单例开放泛型多接口实现。
    /// </summary>
    private sealed class SingletonGenericAliases<T> : IGenericPrimary<T>, IGenericSecondary<T> { }

    /// <summary>
    /// 作用域开放泛型多接口实现。
    /// </summary>
    private sealed class ScopedGenericAliases<T> : IGenericPrimary<T>, IGenericSecondary<T> { }

    /// <summary>
    /// 单接口开放泛型单例实现。
    /// </summary>
    private sealed class SingletonGenericSingle<T> : IGenericPrimary<T> { }

    /// <summary>
    /// 主接口采用尝试添加的开放泛型实现。
    /// </summary>
    [IgnoreDependency]
    [Dependency(ServiceLifetime.Singleton, TryAdd = true)]
    private sealed class TryAddGenericAliases<T> : IGenericPrimary<T>, IGenericSecondary<T> { }

    /// <summary>
    /// 主接口采用尝试添加的作用域开放泛型实现。
    /// </summary>
    [IgnoreDependency]
    [Dependency(ServiceLifetime.Scoped, TryAdd = true)]
    private sealed class ScopedTryAddGenericAliases<T> : IGenericPrimary<T>, IGenericSecondary<T> { }

    /// <summary>
    /// 主接口采用尝试添加的单例开放泛型三接口实现。
    /// </summary>
    [IgnoreDependency]
    [Dependency(ServiceLifetime.Singleton, TryAdd = true)]
    private sealed class TryAddGenericTriples<T> : IGenericPrimary<T>, IGenericSecondary<T>, IGenericTertiary<T> { }

    /// <summary>
    /// 主接口采用尝试添加的作用域开放泛型三接口实现。
    /// </summary>
    [IgnoreDependency]
    [Dependency(ServiceLifetime.Scoped, TryAdd = true)]
    private sealed class ScopedTryAddGenericTriples<T> : IGenericPrimary<T>, IGenericSecondary<T>, IGenericTertiary<T> { }

    /// <summary>
    /// 首接口为多重注入契约的单例开放泛型实现。
    /// </summary>
    [IgnoreDependency]
    [Dependency(ServiceLifetime.Singleton)]
    private sealed class MultipleFirstGenericAliases<T> : IGenericMultiplePrimary<T>, IGenericMultipleSecondary<T> { }

    /// <summary>
    /// 首接口为多重注入契约的作用域开放泛型实现。
    /// </summary>
    [IgnoreDependency]
    [Dependency(ServiceLifetime.Scoped)]
    private sealed class ScopedMultipleFirstGenericAliases<T> : IGenericMultiplePrimary<T>, IGenericMultipleSecondary<T> { }

    /// <summary>
    /// 中间接口为多重注入契约的单例开放泛型实现。
    /// </summary>
    [IgnoreDependency]
    [Dependency(ServiceLifetime.Singleton, TryAdd = true)]
    private sealed class TryAddGenericMultipleMiddle<T> : IGenericPrimary<T>, IGenericMultipleMiddle<T>, IGenericTertiary<T> { }

    /// <summary>
    /// 中间接口为多重注入契约的作用域开放泛型实现。
    /// </summary>
    [IgnoreDependency]
    [Dependency(ServiceLifetime.Scoped, TryAdd = true)]
    private sealed class ScopedTryAddGenericMultipleMiddle<T> : IGenericPrimary<T>, IGenericMultipleMiddle<T>, IGenericTertiary<T> { }

    /// <summary>
    /// 替换主接口的开放泛型实现。
    /// </summary>
    [IgnoreDependency]
    [Dependency(ServiceLifetime.Singleton, ReplaceExisting = true)]
    private sealed class ReplaceGenericAliases<T> : IGenericPrimary<T>, IGenericSecondary<T> { }

    /// <summary>
    /// 替换主接口的作用域开放泛型实现。
    /// </summary>
    [IgnoreDependency]
    [Dependency(ServiceLifetime.Scoped, ReplaceExisting = true)]
    private sealed class ScopedReplaceGenericAliases<T> : IGenericPrimary<T>, IGenericSecondary<T> { }

    /// <summary>
    /// 多重注入契约。
    /// </summary>
    [MultipleDependency]
    private interface IMultiple { }

    /// <summary>
    /// 既有主接口实现。
    /// </summary>
    private sealed class ExistingPrimary : IPrimary { }

    /// <summary>
    /// 同时实现主次接口的作用域服务。
    /// </summary>
    [Dependency(ServiceLifetime.Scoped)]
    private sealed class ScopedImplementation : IPrimary, ISecondary { }

    /// <summary>
    /// 同时注册自身的服务。
    /// </summary>
    [Dependency(ServiceLifetime.Singleton, AddSelf = true)]
    private sealed class SelfRegistered : IResource, IDisposable
    {
        /// <summary>
        /// 获取释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }

    /// <summary>
    /// 首个多重注入实现。
    /// </summary>
    [Dependency(ServiceLifetime.Singleton)]
    private sealed class MultipleA : IMultiple { }

    /// <summary>
    /// 第二个多重注入实现。
    /// </summary>
    [Dependency(ServiceLifetime.Singleton)]
    private sealed class MultipleB : IMultiple { }

    /// <summary>
    /// 替换已有主接口的实现。
    /// </summary>
    [Dependency(ServiceLifetime.Singleton, ReplaceExisting = true)]
    private sealed class ReplacingPrimary : IPrimary { }

    /// <summary>
    /// 主接口采用尝试添加的实现。
    /// </summary>
    [Dependency(ServiceLifetime.Singleton, TryAdd = true)]
    private sealed class SecondaryImplementation : IPrimary, ISecondary { }

    /// <summary>
    /// 同时提供业务接口与异步释放的资源。
    /// </summary>
    [Dependency(ServiceLifetime.Singleton)]
    private sealed class AsyncResource : IResource, IAsyncDisposable
    {
        /// <summary>
        /// 获取异步释放次数。
        /// </summary>
        public int AsyncDisposeCount { get; private set; }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            AsyncDisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 调用方拥有的同步资源。
    /// </summary>
    private sealed class SyncResource : IResource, IDisposable
    {
        /// <summary>
        /// 获取同步释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }

    /// <summary>
    /// 容器拥有的双路径资源。
    /// </summary>
    private sealed class DualResource : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 获取同步释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <summary>
        /// 获取异步释放次数。
        /// </summary>
        public int AsyncDisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            AsyncDisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 共享两个业务接口的同步资源。
    /// </summary>
    [Dependency(ServiceLifetime.Singleton)]
    private sealed class DisposableAliases : IPrimary, ISecondary, IDisposable
    {
        /// <summary>
        /// 获取释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }

    /// <summary>
    /// 共享两个业务接口的异步资源。
    /// </summary>
    [Dependency(ServiceLifetime.Singleton)]
    private sealed class AsyncBusinessAliases : IPrimary, ISecondary, IAsyncDisposable
    {
        /// <summary>
        /// 获取释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 唯一拥有共享资源的容器服务。
    /// </summary>
    private sealed class AdapterOwner : IDisposable
    {
        /// <summary>
        /// 获取释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }

    /// <summary>
    /// 不拥有资源的主接口适配器。
    /// </summary>
    private sealed class PrimaryAdapter(AdapterOwner owner) : IPrimary
    {
        /// <summary>
        /// 共享资源所有者。
        /// </summary>
        public AdapterOwner Owner { get; } = owner;
    }

    /// <summary>
    /// 不拥有资源的次接口适配器。
    /// </summary>
    private sealed class SecondaryAdapter(AdapterOwner owner) : ISecondary
    {
        /// <summary>
        /// 共享资源所有者。
        /// </summary>
        public AdapterOwner Owner { get; } = owner;
    }
}
