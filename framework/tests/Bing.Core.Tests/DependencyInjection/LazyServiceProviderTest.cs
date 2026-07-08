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

    private static (IServiceProvider root, IServiceScope scope) BuildScope(
        Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        var root = services.BuildServiceProvider();
        var scope = root.CreateScope();
        return (root, scope);
    }

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

    private interface IMyService { }
    private class MyService : IMyService { }
}
