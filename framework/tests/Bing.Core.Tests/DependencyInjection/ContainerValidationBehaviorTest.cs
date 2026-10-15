using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace Bing.Tests.DependencyInjection;

/// <summary>
/// 验证服务容器对循环依赖和作用域错误的诊断边界。
/// </summary>
public class ContainerValidationBehaviorTest
{
    /// <summary>
    /// 验证构造函数自环在容器构建时被发现。
    /// </summary>
    [Fact]
    public void ValidateOnBuild_ShouldRejectConstructorSelfCycle()
    {
        var services = new ServiceCollection();
        services.AddTransient<SelfCycle>();

        Assert.Throws<AggregateException>(() => services.BuildServiceProvider(ValidationOptions()));
    }

    /// <summary>
    /// 验证多节点构造环在容器构建时被发现。
    /// </summary>
    [Fact]
    public void ValidateOnBuild_ShouldRejectMultiNodeConstructorCycle()
    {
        var services = new ServiceCollection();
        services.AddTransient<FirstCycle>();
        services.AddTransient<SecondCycle>();

        var error = Assert.Throws<AggregateException>(() => services.BuildServiceProvider(ValidationOptions()));
        Assert.Contains("circular", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证缺失依赖在容器构建时被发现。
    /// </summary>
    [Fact]
    public void ValidateOnBuild_ShouldRejectMissingDependency()
    {
        var services = new ServiceCollection();
        services.AddTransient<MissingConsumer>();

        Assert.Throws<AggregateException>(() => services.BuildServiceProvider(ValidationOptions()));
    }

    /// <summary>
    /// 验证单例捕获作用域服务在容器构建时被发现。
    /// </summary>
    [Fact]
    public void ValidateScopes_ShouldRejectSingletonCapturingScoped()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedValue>();
        services.AddSingleton<SingletonConsumer>();

        Assert.Throws<AggregateException>(() => services.BuildServiceProvider(ValidationOptions()));
    }

    /// <summary>
    /// 验证根容器解析作用域服务在解析时被拒绝。
    /// </summary>
    [Fact]
    public void ValidateScopes_ShouldRejectRootScopedResolution()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedValue>();
        using var provider = services.BuildServiceProvider(ValidationOptions());

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ScopedValue>());
    }

    /// <summary>
    /// 验证开放泛型及集合依赖的解析结果。
    /// </summary>
    [Fact]
    public void ValidateOnBuild_ShouldResolveOpenGenericAndEnumerable()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(IGeneric<>), typeof(Generic<>));
        services.AddTransient<IItem, ItemA>();
        services.AddTransient<IItem, ItemB>();
        using var provider = services.BuildServiceProvider(ValidationOptions());

        Assert.IsType<Generic<int>>(provider.GetRequiredService<IGeneric<int>>());
        Assert.Equal(2, provider.GetServices<IItem>().Count());
    }

    /// <summary>
    /// 验证集合元素反向依赖消费者时容器拒绝构造环。
    /// </summary>
    [Fact]
    public void ValidateOnBuild_ShouldRejectEnumerableElementCycle()
    {
        var services = new ServiceCollection();
        services.AddTransient<CollectionConsumer>();
        services.AddTransient<ICollectionItem, CollectionItem>();

        var error = Assert.Throws<AggregateException>(() => services.BuildServiceProvider(ValidationOptions()));
        Assert.Contains("circular", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证跨作用域保存缓存会在原作用域释放后继续返回已缓存实例。
    /// </summary>
    [Fact]
    public void LazyServiceProvider_RetainedAcrossScopes_ShouldRetainCachedInstance()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedDisposable>();
        using var provider = services.BuildServiceProvider(ValidationOptions());
        var firstScope = provider.CreateScope();
        var cache = new LazyServiceProvider(firstScope.ServiceProvider);
        var first = cache.LazyGetService<ScopedDisposable>();
        firstScope.Dispose();
        using var secondScope = provider.CreateScope();

        Assert.True(first.Disposed);
        Assert.Same(first, cache.LazyGetService<ScopedDisposable>());
        Assert.NotSame(first, secondScope.ServiceProvider.GetRequiredService<ScopedDisposable>());
    }

    /// <summary>
    /// 验证同一个 Lazy 实例重入 Value 时由运行库拒绝递归访问。
    /// </summary>
    [Fact]
    public void SameLazyInstance_ReentrantValueAccess_ShouldFailFast()
    {
        Lazy<int> value = null;
        value = new Lazy<int>(() => value.Value);

        Assert.Throws<InvalidOperationException>(() => _ = value.Value);
    }

    /// <summary>
    /// 验证 Lazy 直到访问 Value 才解析服务。
    /// </summary>
    [Fact]
    public void Lazy_ShouldDelayResolutionUntilValueAccess()
    {
        var created = 0;
        var services = new ServiceCollection();
        RegisterLazy(services);
        services.AddTransient<ScopedValue>(_ =>
        {
            created++;
            return new ScopedValue();
        });
        using var provider = services.BuildServiceProvider(ValidationOptions());
        var lazy = provider.GetRequiredService<Lazy<ScopedValue>>();

        Assert.Equal(0, created);
        Assert.NotNull(lazy.Value);
        Assert.Equal(1, created);
    }

    /// <summary>
    /// 验证工厂递归和立即访问 Lazy 不受构建期校验覆盖。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DynamicCycle_ShouldFailAtResolutionRatherThanBuild(bool throughLazy)
    {
        var depth = 0;
        var services = new ServiceCollection();
        RegisterLazy(services);
        services.AddTransient<DynamicCycle>(provider =>
        {
            if (++depth == 4)
                throw new InvalidOperationException("bounded recursive resolution");
            return throughLazy
                ? provider.GetRequiredService<Lazy<DynamicCycle>>().Value
                : provider.GetRequiredService<DynamicCycle>();
        });
        using var root = services.BuildServiceProvider(ValidationOptions());

        var error = Assert.Throws<InvalidOperationException>(() => root.GetRequiredService<DynamicCycle>());
        Assert.Equal("bounded recursive resolution", error.Message);
    }

    /// <summary>
    /// 验证并发访问同一个 Lazy 只解析一次。
    /// </summary>
    [Fact]
    public async Task Lazy_ConcurrentValueAccess_ShouldShareOneResolution()
    {
        var created = 0;
        var services = new ServiceCollection();
        RegisterLazy(services);
        services.AddTransient<ScopedValue>(_ =>
        {
            Interlocked.Increment(ref created);
            return new ScopedValue();
        });
        using var provider = services.BuildServiceProvider();
        var lazy = provider.GetRequiredService<Lazy<ScopedValue>>();

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => lazy.Value)));

        Assert.Equal(1, created);
        Assert.All(results, result => Assert.Same(results[0], result));
    }

    /// <summary>
    /// 验证作用域缓存服务在释放外部引用后可回收。
    /// </summary>
    [Fact]
    public void LazyServiceProvider_ClosedScope_ShouldReleaseCachedReferences()
    {
        var references = CreateCachedScopeReferences();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(references.Cache.IsAlive);
        Assert.False(references.Service.IsAlive);
    }

    /// <summary>
    /// 创建启用容器原生校验的选项。
    /// </summary>
    private static ServiceProviderOptions ValidationOptions() => new()
    {
        ValidateOnBuild = true,
        ValidateScopes = true
    };

    /// <summary>
    /// 注册框架实际使用的延迟解析实现。
    /// </summary>
    private static void RegisterLazy(IServiceCollection services)
    {
        var implementation = typeof(DependencyModule).Assembly.GetType("Bing.DependencyInjection.Lazier`1", true);
        services.AddTransient(typeof(Lazy<>), implementation);
    }

    /// <summary>
    /// 在独立栈帧创建并释放作用域缓存。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Cache, WeakReference Service) CreateCachedScopeReferences()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedValue>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var cache = new LazyServiceProvider(scope.ServiceProvider);
        var service = cache.LazyGetService<ScopedValue>();
        return (new WeakReference(cache), new WeakReference(service));
    }

    /// <summary>
    /// 构造函数自环服务。
    /// </summary>
    private sealed class SelfCycle
    {
        /// <summary>
        /// 初始化一个 <see cref="SelfCycle" /> 类型的实例。
        /// </summary>
        public SelfCycle(SelfCycle value) { }
    }

    /// <summary>
    /// 构造环首节点。
    /// </summary>
    private sealed class FirstCycle
    {
        /// <summary>
        /// 初始化一个 <see cref="FirstCycle" /> 类型的实例。
        /// </summary>
        public FirstCycle(SecondCycle value) { }
    }

    /// <summary>
    /// 构造环次节点。
    /// </summary>
    private sealed class SecondCycle
    {
        /// <summary>
        /// 初始化一个 <see cref="SecondCycle" /> 类型的实例。
        /// </summary>
        public SecondCycle(FirstCycle value) { }
    }

    /// <summary>
    /// 缺失依赖契约。
    /// </summary>
    private interface IMissing { }

    /// <summary>
    /// 依赖缺失服务的消费者。
    /// </summary>
    private sealed class MissingConsumer
    {
        /// <summary>
        /// 初始化一个 <see cref="MissingConsumer" /> 类型的实例。
        /// </summary>
        public MissingConsumer(IMissing value) { }
    }

    /// <summary>
    /// 作用域值。
    /// </summary>
    private sealed class ScopedValue { }

    /// <summary>
    /// 记录作用域结束的服务。
    /// </summary>
    private sealed class ScopedDisposable : IDisposable
    {
        /// <summary>
        /// 是否已释放。
        /// </summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// 集合元素契约。
    /// </summary>
    private interface ICollectionItem { }

    /// <summary>
    /// 依赖整个集合的消费者。
    /// </summary>
    private sealed class CollectionConsumer
    {
        /// <summary>
        /// 初始化一个 <see cref="CollectionConsumer" /> 类型的实例。
        /// </summary>
        public CollectionConsumer(IEnumerable<ICollectionItem> items) { }
    }

    /// <summary>
    /// 反向依赖集合消费者的元素。
    /// </summary>
    private sealed class CollectionItem : ICollectionItem
    {
        /// <summary>
        /// 初始化一个 <see cref="CollectionItem" /> 类型的实例。
        /// </summary>
        public CollectionItem(CollectionConsumer consumer) { }
    }

    /// <summary>
    /// 捕获作用域值的单例。
    /// </summary>
    private sealed class SingletonConsumer
    {
        /// <summary>
        /// 初始化一个 <see cref="SingletonConsumer" /> 类型的实例。
        /// </summary>
        public SingletonConsumer(ScopedValue value) { }
    }

    /// <summary>
    /// 开放泛型契约。
    /// </summary>
    private interface IGeneric<T> { }

    /// <summary>
    /// 开放泛型实现。
    /// </summary>
    private sealed class Generic<T> : IGeneric<T> { }

    /// <summary>
    /// 集合元素契约。
    /// </summary>
    private interface IItem { }

    /// <summary>
    /// 首个集合元素。
    /// </summary>
    private sealed class ItemA : IItem { }

    /// <summary>
    /// 第二个集合元素。
    /// </summary>
    private sealed class ItemB : IItem { }

    /// <summary>
    /// 动态解析环节点。
    /// </summary>
    private sealed class DynamicCycle { }
}
