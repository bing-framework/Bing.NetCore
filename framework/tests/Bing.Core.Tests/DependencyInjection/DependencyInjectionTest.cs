using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tests.DependencyInjection;

/// <summary>
/// 依赖注入生命周期测试。
/// </summary>
[Collection("Module static compatibility")]
public class DependencyInjectionTest
{
    /// <summary>
    /// 验证单例解析瞬时服务不依赖当前作用域。
    /// </summary>
    [Fact]
    public void Singletons_Should_Resolve_Transients_Independent_From_Current_Scope()
    {
        //Arrange

        var services = new ServiceCollection();

        services
            .AddSingleton<MySingletonServiceUsesTransients>()
            .AddTransient<MyTransientServiceUsesSingleton>()
            .AddTransient<MyTransientService>();

        MySingletonServiceUsesTransients singletonService;

        using (var serviceProvider = services.BuildServiceProvider())
        {
            // Act: 创建多个作用域，并验证 Transient 服务的独立性
            using (var scope = serviceProvider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<MyTransientServiceUsesSingleton>().DoIt();
                scope.ServiceProvider.GetRequiredService<MyTransientServiceUsesSingleton>().DoIt();
            }

            using (var scope = serviceProvider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<MyTransientServiceUsesSingleton>().DoIt();
                scope.ServiceProvider.GetRequiredService<MyTransientServiceUsesSingleton>().DoIt();
                scope.ServiceProvider.GetRequiredService<MySingletonServiceUsesTransients>().ShouldNotBeDisposed();
            }

            singletonService = serviceProvider.GetRequiredService<MySingletonServiceUsesTransients>();
            singletonService.ShouldNotBeDisposed();
        }

        // Assert: 确保 Transient 实例在主服务释放时被正确释放
        singletonService.ShouldBeDisposed();
    }

    /// <summary>
    /// 验证作用域释放会清理已解析的瞬时服务。
    /// </summary>
    [Fact]
    public void Should_Release_Resolved_Services_When_Main_Service_Is_Disposed()
    {
        var services = new ServiceCollection();

        services
            .AddTransient<MyTransientServiceUsesTransients>()
            .AddTransient<MyTransientService>();

        using (var serviceProvider = services.BuildServiceProvider())
        {
            MyTransientServiceUsesTransients myTransientServiceUsesTransients;

            using (var scope = serviceProvider.CreateScope())
            {
                myTransientServiceUsesTransients = scope.ServiceProvider.GetRequiredService<MyTransientServiceUsesTransients>();

                myTransientServiceUsesTransients.DoIt();
                myTransientServiceUsesTransients.DoIt();

                myTransientServiceUsesTransients.ShouldNotBeDisposed();
            }

            // Assert: 确保在作用域被释放后，Transient 实例被释放
            myTransientServiceUsesTransients.ShouldBeDisposed();
        }
    }

    /// <summary>
    /// 验证内层作用域解析独立的作用域服务实例。
    /// </summary>
    [Fact]
    public void Inner_Scope_Should_Resolve_New_Scoped_Service()
    {
        var services = new ServiceCollection();

        services
            .AddScoped<ScopedServiceWithState>();

        using (var serviceProvider = services.BuildServiceProvider())
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var service1 = scope.ServiceProvider.GetRequiredService<ScopedServiceWithState>();
                var service2 = scope.ServiceProvider.GetRequiredService<ScopedServiceWithState>();

                // Assert: 在同一作用域内，Scoped 实例应相同
                service1.ShouldBe(service2);

                using (var innerScope = scope.ServiceProvider.CreateScope())
                {
                    var innserService1 = innerScope.ServiceProvider.GetRequiredService<ScopedServiceWithState>();
                    var innserService2 = innerScope.ServiceProvider.GetRequiredService<ScopedServiceWithState>();

                    // Assert: 在新的作用域内，Scoped 实例应不同
                    innserService1.ShouldBe(innserService2);
                    innserService1.ShouldNotBe(service1);
                }
            }
        }
    }

    /// <summary>
    /// 验证约定注册的作用域服务在作用域内复用。
    /// </summary>
    [Fact]
    public void AutoLoad_Scoped()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddBing();
        using var serviceProvider = serviceCollection.BuildBingServiceProvider();

        var scope1 = serviceProvider.CreateScope();
        var a1= scope1.ServiceProvider.GetService<IA>();
        Assert.True(a1 is D);

        var id1 = a1.Id;
        var a3 = scope1.ServiceProvider.GetRequiredService<IA>();
        Assert.True(a3 is D);

        var id3 = a3.Id;
        scope1.Dispose();
        Assert.Equal(id1, id3);

        var scope2 = serviceProvider.CreateScope();
        var a2 = scope2.ServiceProvider.GetRequiredService<IA>();
        Assert.True(a2 is D);

        var id2 = a2.Id;
        scope1.Dispose();
        Assert.NotEqual(id1, id2);
    }

    /// <summary>
    /// 验证约定注册可解析多个作用域服务实现。
    /// </summary>
    [Fact]
    public void AutoLoad_Scoped_MultiInjection()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddBing();
        using var serviceProvider = serviceCollection.BuildBingServiceProvider();

        var scope1 = serviceProvider.CreateScope();
        var aList = scope1.ServiceProvider.GetServices<IA>();
        Assert.Equal(2, aList.Count());
    }

    /// <summary>
    /// 验证约定注册的单例服务复用同一实例。
    /// </summary>
    [Fact]
    public void AutoLoad_Singleton()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddBing();
        using var serviceProvider = serviceCollection.BuildBingServiceProvider();

        var b1 = serviceProvider.GetRequiredService<IB>();
        Assert.True(b1 is B);

        var b2 = serviceProvider.GetRequiredService<IB>();
        Assert.True(b2 is B);

        Assert.Equal(b1.Id, b2.Id);
    }

    /// <summary>
    /// 验证约定注册的瞬时服务每次创建新实例。
    /// </summary>
    [Fact]
    public void AutoLoad_Transient()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddBing();
        using var serviceProvider = serviceCollection.BuildBingServiceProvider();

        var c1 = serviceProvider.GetRequiredService<IC>();
        Assert.True(c1 is C);

        var c2 = serviceProvider.GetRequiredService<IC>();
        Assert.True(c2 is C);

        Assert.NotEqual(c1.Id, c2.Id);
    }

    #region Samples

    /// <summary>
    /// 依赖单例的瞬时服务。
    /// </summary>
    private class MyTransientServiceUsesSingleton
    {
        /// <summary>
        /// 保存当前瞬时服务引用的单例服务。
        /// </summary>
        private readonly MySingletonServiceUsesTransients _singletonService;

        /// <summary>
        /// 初始化依赖单例的瞬时服务。
        /// </summary>
        /// <param name="singletonService">共享的单例服务。</param>
        public MyTransientServiceUsesSingleton(MySingletonServiceUsesTransients singletonService) => _singletonService = singletonService;

        /// <summary>
        /// 调用单例服务创建瞬时服务。
        /// </summary>
        public void DoIt() => _singletonService.DoIt();
    }

    /// <summary>
    /// 依赖多个瞬时服务的单例服务。
    /// </summary>
    private class MySingletonServiceUsesTransients
    {
        /// <summary>
        /// 保存用于解析瞬时服务的根服务提供程序。
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 记录当前单例创建的瞬时服务实例。
        /// </summary>
        private readonly List<MyTransientService> _instances;

        /// <summary>
        /// 初始化依赖多个瞬时服务的单例服务。
        /// </summary>
        /// <param name="serviceProvider">用于解析瞬时服务的提供程序。</param>
        public MySingletonServiceUsesTransients(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _instances = new List<MyTransientService>();
        }

        /// <summary>
        /// 解析并记录一个瞬时服务。
        /// </summary>
        public void DoIt() => _instances.Add(_serviceProvider.GetRequiredService<MyTransientService>());

        /// <summary>
        /// 断言已记录的瞬时服务尚未释放。
        /// </summary>
        public void ShouldNotBeDisposed()
        {
            foreach (var instance in _instances)
                instance.IsDisposed.ShouldBeFalse();
        }

        /// <summary>
        /// 断言已记录的瞬时服务已释放。
        /// </summary>
        public void ShouldBeDisposed()
        {
            foreach (var instance in _instances)
                instance.IsDisposed.ShouldBeTrue();
        }
    }

    /// <summary>
    /// 瞬时服务，支持 IDisposable 以便测试是否被释放。
    /// </summary>
    private class MyTransientService : IDisposable
    {
        /// <summary>
        /// 获取当前实例是否已释放。
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => IsDisposed = true;
    }

    /// <summary>
    /// 依赖多个瞬时服务的瞬时服务。
    /// </summary>
    private class MyTransientServiceUsesTransients
    {
        /// <summary>
        /// 保存用于解析嵌套瞬时服务的提供程序。
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 记录当前瞬时服务创建的子实例。
        /// </summary>
        private readonly List<MyTransientService> _instances;

        /// <summary>
        /// 初始化依赖多个瞬时服务的瞬时服务。
        /// </summary>
        /// <param name="serviceProvider">用于解析子实例的提供程序。</param>
        public MyTransientServiceUsesTransients(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _instances = new List<MyTransientService>();
        }

        /// <summary>
        /// 解析并记录一个子实例。
        /// </summary>
        public void DoIt() => _instances.Add(_serviceProvider.GetRequiredService<MyTransientService>());

        /// <summary>
        /// 断言已记录的子实例尚未释放。
        /// </summary>
        public void ShouldNotBeDisposed()
        {
            foreach (var instance in _instances)
                instance.IsDisposed.ShouldBeFalse();
        }

        /// <summary>
        /// 断言已记录的子实例已释放。
        /// </summary>
        public void ShouldBeDisposed()
        {
            foreach (var instance in _instances)
                instance.IsDisposed.ShouldBeTrue();
        }
    }

    /// <summary>
    /// 具有状态的 Scoped 服务。
    /// </summary>
    private class ScopedServiceWithState
    {
        /// <summary>
        /// 保存当前作用域实例中的键值数据。
        /// </summary>
        private readonly Dictionary<string, object> _items;

        /// <summary>
        /// 初始化具有独立状态的作用域服务。
        /// </summary>
        public ScopedServiceWithState() => _items = new Dictionary<string, object>();

        /// <summary>
        /// 设置当前作用域中的键值。
        /// </summary>
        /// <param name="name">状态名称。</param>
        /// <param name="value">对应的状态值。</param>
        public void Set(string name, object value) => _items[name] = value;

        /// <summary>
        /// 获取当前作用域中的状态值。
        /// </summary>
        /// <param name="name">状态名称。</param>
        /// <returns>对应的状态值。</returns>
        public object Get(string name) => _items[name];
    }

    /// <summary>
    /// 作用域自动注册服务契约。
    /// </summary>
    private interface IA : IScopedDependency
    {
        /// <summary>
        /// 获取当前服务实例的标识。
        /// </summary>
        string Id { get; }
    }

    /// <summary>
    /// 单例自动注册服务契约。
    /// </summary>
    private interface IB : ISingletonDependency
    {
        /// <summary>
        /// 获取当前服务实例的标识。
        /// </summary>
        string Id { get; }
    }

    /// <summary>
    /// 瞬时自动注册服务契约。
    /// </summary>
    private interface IC : ITransientDependency
    {
        /// <summary>
        /// 获取当前服务实例的标识。
        /// </summary>
        string Id { get; }
    }

    /// <summary>
    /// 作用域服务的第一个测试实现。
    /// </summary>
    private class A : IA
    {
        /// <inheritdoc />
        public string Id { get; } = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 单例服务的测试实现。
    /// </summary>
    private class B : IB
    {
        /// <inheritdoc />
        public string Id { get; } = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 瞬时服务的测试实现。
    /// </summary>
    private class C : IC
    {
        /// <inheritdoc />
        public string Id { get; } = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 作用域服务的第二个测试实现。
    /// </summary>
    private class D : IA
    {
        /// <inheritdoc />
        public string Id { get; } = Guid.NewGuid().ToString();
    }

    #endregion
}
