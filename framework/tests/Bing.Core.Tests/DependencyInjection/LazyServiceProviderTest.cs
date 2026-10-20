using System.Runtime.CompilerServices;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bing.Tests.DependencyInjection;

/// <summary>
/// LazyServiceProvider 延迟加载服务提供程序测试
/// </summary>
public class LazyServiceProviderTest
{
    // ==================== 辅助 ====================

    /// <summary>
    /// 根据注册配置创建根服务提供程序和独立作用域。
    /// </summary>
    /// <param name="configure">用于配置服务集合的委托。</param>
    /// <returns>根服务提供程序及其创建的作用域。</returns>
    private static (IServiceProvider root, IServiceScope scope) BuildScope(
        Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        var root = services.BuildServiceProvider();
        var scope = root.CreateScope();
        return (root, scope);
    }

    /// <summary>
    /// 创建延迟服务提供程序。
    /// </summary>
    /// <param name="sp">底层服务提供程序。</param>
    /// <returns>封装指定服务提供程序的延迟访问器。</returns>
    private static LazyServiceProvider CreateProvider(IServiceProvider sp) =>
        new LazyServiceProvider(sp);

    // ==================== LazyGetService ====================

    /// <summary>
    /// 测试目的：服务未注册时，LazyGetService&lt;T&gt; 应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void LazyGetService_Unregistered_ReturnsNull()
    {
        // Arrange
        var (root, _) = BuildScope(_ => { });
        var lazy = CreateProvider(root);

        // Act
        var result = lazy.LazyGetService<IDisposable>();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：服务已注册时，LazyGetService&lt;T&gt; 应返回正确实例。
    /// </summary>
    [Fact]
    public void LazyGetService_Registered_ReturnsInstance()
    {
        // Arrange
        var (root, _) = BuildScope(s => s.AddSingleton<IMyService, MyService>());
        var lazy = CreateProvider(root);

        // Act
        var result = lazy.LazyGetService<IMyService>();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeAssignableTo<MyService>();
    }

    /// <summary>
    /// 测试目的：连续两次调用 LazyGetService&lt;T&gt; 应返回缓存的相同实例（幂等性）。
    /// </summary>
    [Fact]
    public void LazyGetService_CalledTwice_ReturnsCachedInstance()
    {
        // Arrange
        var (root, _) = BuildScope(s => s.AddTransient<IMyService, MyService>());
        var lazy = CreateProvider(root);

        // Act
        var first = lazy.LazyGetService<IMyService>();
        var second = lazy.LazyGetService<IMyService>();

        // Assert：LazyServiceProvider 内部有 ConcurrentDictionary 缓存，两次返回同一实例
        first.ShouldBeSameAs(second);
    }

    // ==================== LazyGetService with default ====================

    /// <summary>
    /// 测试目的：服务未注册时，传入 defaultValue 的重载应返回 defaultValue。
    /// </summary>
    [Fact]
    public void LazyGetService_WithDefault_UnregisteredService_ReturnsDefault()
    {
        // Arrange
        var (root, _) = BuildScope(_ => { });
        var lazy = CreateProvider(root);
        var fallback = new MyService();

        // Act
        var result = lazy.LazyGetService<IMyService>(fallback);

        // Assert
        result.ShouldBeSameAs(fallback);
    }

    /// <summary>
    /// 测试目的：服务已注册时，传入 defaultValue 的重载应返回注册的服务（不是默认值）。
    /// </summary>
    [Fact]
    public void LazyGetService_WithDefault_RegisteredService_ReturnsService()
    {
        // Arrange
        var (root, _) = BuildScope(s => s.AddSingleton<IMyService, MyService>());
        var lazy = CreateProvider(root);
        var fallback = new MyService();

        // Act
        var result = lazy.LazyGetService<IMyService>(fallback);

        // Assert
        result.ShouldNotBeSameAs(fallback);
        result.ShouldBeAssignableTo<MyService>();
    }

    // ==================== LazyGetService with factory ====================

    /// <summary>
    /// 测试目的：使用工厂委托的重载，应通过工厂创建实例。
    /// </summary>
    [Fact]
    public void LazyGetService_WithFactory_UsesFactoryToCreateInstance()
    {
        // Arrange
        var (root, _) = BuildScope(_ => { });
        var lazy = CreateProvider(root);
        var expected = new MyService();

        // Act
        var result = lazy.LazyGetService<IMyService>(_ => expected);

        // Assert
        result.ShouldBeSameAs(expected);
    }

    /// <summary>
    /// 测试目的：工厂委托重载调用两次应返回缓存的同一实例。
    /// </summary>
    [Fact]
    public void LazyGetService_WithFactory_CalledTwice_ReturnsCachedInstance()
    {
        // Arrange
        var (root, _) = BuildScope(_ => { });
        var lazy = CreateProvider(root);
        var callCount = 0;

        // Act
        var first = lazy.LazyGetService<IMyService>(_ => { callCount++; return new MyService(); });
        var second = lazy.LazyGetService<IMyService>(_ => { callCount++; return new MyService(); });

        // Assert：工厂只被调用一次（Lazy<> 缓存）
        callCount.ShouldBe(1);
        first.ShouldBeSameAs(second);
    }

    /// <summary>
    /// 验证工厂重载会缓存作用域服务，并在原作用域释放后保持缓存实例。
    /// </summary>
    [Fact]
    public void LazyGetService_WithFactory_ShouldCacheScopedDisposableAcrossScopeRelease()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedFactoryDisposable>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var firstScope = provider.CreateScope();
        var cache = new LazyServiceProvider(firstScope.ServiceProvider);
        var expected = firstScope.ServiceProvider.GetRequiredService<ScopedFactoryDisposable>();
        var first = (ScopedFactoryDisposable)cache.LazyGetService(typeof(ScopedFactoryDisposable),
            _ => expected);

        first.ShouldBeSameAs(expected);
        firstScope.Dispose();

        first.DisposeCount.ShouldBe(1);
        var cached = (ScopedFactoryDisposable)cache.LazyGetService(typeof(ScopedFactoryDisposable),
            _ => throw new InvalidOperationException("缓存实例不应再次调用工厂。"));
        cached.ShouldBeSameAs(first);
        first.DisposeCount.ShouldBe(1);

        var secondScope = provider.CreateScope();
        var second = secondScope.ServiceProvider.GetRequiredService<ScopedFactoryDisposable>();
        second.ShouldNotBeSameAs(first);
        secondScope.Dispose();
        second.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证工厂重载缓存及其作用域服务在缓存释放后可回收。
    /// </summary>
    [Fact]
    public void LazyGetService_WithFactory_ShouldReleaseCacheAndScopedService()
    {
        var references = CreateFactoryCachedScopeReferences();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        references.Cache.IsAlive.ShouldBeFalse();
        references.Service.IsAlive.ShouldBeFalse();
    }

    // ==================== LazyGetRequiredService ====================

    /// <summary>
    /// 测试目的：服务已注册时，LazyGetRequiredService&lt;T&gt; 返回正确实例。
    /// </summary>
    [Fact]
    public void LazyGetRequiredService_Registered_ReturnsInstance()
    {
        // Arrange
        var (root, _) = BuildScope(s => s.AddSingleton<IMyService, MyService>());
        var lazy = CreateProvider(root);

        // Act
        var result = lazy.LazyGetRequiredService<IMyService>();

        // Assert
        result.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：服务未注册时，LazyGetRequiredService&lt;T&gt; 应抛出异常。
    /// </summary>
    [Fact]
    public void LazyGetRequiredService_Unregistered_ThrowsException()
    {
        // Arrange
        var (root, _) = BuildScope(_ => { });
        var lazy = CreateProvider(root);

        // Act & Assert
        Should.Throw<Exception>(() => lazy.LazyGetRequiredService<IMyService>());
    }

    // ==================== IServiceProvider 缓存 ====================

    /// <summary>
    /// 测试目的：IServiceProvider 自身被缓存，通过 LazyGetService 可以取回。
    /// </summary>
    [Fact]
    public void LazyGetService_IServiceProvider_ReturnsCachedProvider()
    {
        // Arrange
        var (root, _) = BuildScope(_ => { });
        var lazy = CreateProvider(root);

        // Act
        var sp = lazy.LazyGetService<IServiceProvider>();

        // Assert
        sp.ShouldBeSameAs(root);
    }

    // ==================== 辅助类型 ====================

    /// <summary>
    /// 在独立栈帧创建工厂缓存并释放其作用域。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Cache, WeakReference Service) CreateFactoryCachedScopeReferences()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedFactoryDisposable>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var cache = new LazyServiceProvider(scope.ServiceProvider);
        var service = (ScopedFactoryDisposable)cache.LazyGetService(typeof(ScopedFactoryDisposable),
            serviceProvider => serviceProvider.GetRequiredService<ScopedFactoryDisposable>());

        return (new WeakReference(cache), new WeakReference(service));
    }

    /// <summary>
    /// 记录工厂创建的作用域资源是否已释放。
    /// </summary>
    private sealed class ScopedFactoryDisposable : IDisposable
    {
        /// <summary>
        /// 记录资源的释放次数。
        /// </summary>
        private int _disposeCount;

        /// <summary>
        /// 获取资源释放次数。
        /// </summary>
        public int DisposeCount => Volatile.Read(ref _disposeCount);

        /// <inheritdoc />
        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    /// <summary>
    /// 服务延迟解析测试使用的契约。
    /// </summary>
    private interface IMyService { }
    /// <summary>
    /// 用于验证服务延迟解析的测试服务。
    /// </summary>
    private class MyService : IMyService { }
}
