using Bing.ExceptionHandling;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.ExceptionHandling;

/// <summary>
/// 验证异常通知器在真实依赖注入作用域中的异步释放行为。
/// </summary>
public sealed class ExceptionNotifierLifecycleTest
{
    /// <summary>
    /// 验证通知完成后等待仅异步依赖释放。
    /// </summary>
    [Fact]
    public async Task NotifyAsync_WithAsyncOnlyDependency_ShouldAwaitAsyncDisposal()
    {
        var counter = new SubscriberCounter();
        var services = new ServiceCollection();
        services.AddScoped(_ => new AsyncOnlyDependency(counter));
        services.AddTransient<IExceptionSubscriber, AsyncSubscriber>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var notifier = CreateNotifier(provider);

        await notifier.NotifyAsync(new ExceptionNotificationContext(new Exception("notification")));

        counter.Handled.ShouldBe(1);
        counter.AsyncDisposals.ShouldBe(1);
        counter.SyncDisposals.ShouldBe(0);
    }

    /// <summary>
    /// 验证订阅器异常被隔离后仍完成异步释放。
    /// </summary>
    [Fact]
    public async Task NotifyAsync_WhenSubscriberFails_ShouldIsolateAndDisposeAsyncDependency()
    {
        var counter = new SubscriberCounter();
        var services = new ServiceCollection();
        services.AddScoped(_ => new AsyncOnlyDependency(counter));
        services.AddTransient<IExceptionSubscriber, FailingSubscriber>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var notifier = CreateNotifier(provider);

        await notifier.NotifyAsync(new ExceptionNotificationContext(new Exception("notification")));

        counter.Handled.ShouldBe(1);
        counter.AsyncDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证解析失败时释放已创建依赖并保留解析主异常。
    /// </summary>
    [Fact]
    public async Task NotifyAsync_WhenSubscriberResolutionFails_ShouldDisposeCreatedDependency()
    {
        var counter = new SubscriberCounter();
        var services = new ServiceCollection();
        services.AddScoped(_ => new AsyncOnlyDependency(counter));
        services.AddTransient<IExceptionSubscriber>(sp =>
        {
            sp.GetRequiredService<AsyncOnlyDependency>();
            throw new InvalidOperationException("subscriber-resolution-failure");
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var notifier = CreateNotifier(provider);

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            notifier.NotifyAsync(new ExceptionNotificationContext(new Exception("notification"))));

        error.Message.ShouldBe("subscriber-resolution-failure");
        counter.AsyncDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证解析失败与清理失败同时发生时保留解析主异常并附加清理证据。
    /// </summary>
    [Fact]
    public async Task NotifyAsync_WhenResolutionAndCleanupFail_ShouldPreserveBothErrors()
    {
        var services = new ServiceCollection();
        services.AddScoped<ThrowingAsyncDisposable>();
        services.AddTransient<IExceptionSubscriber>(sp =>
        {
            sp.GetRequiredService<ThrowingAsyncDisposable>();
            throw new InvalidOperationException("subscriber-resolution-failure");
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var notifier = CreateNotifier(provider);

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            notifier.NotifyAsync(new ExceptionNotificationContext(new Exception("notification"))));

        error.Message.ShouldBe("subscriber-resolution-failure");
        error.Data["Bing.ExceptionHandling.ScopeCleanupError"].ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldBe("dependency-async-dispose-failure");
    }

    /// <summary>
    /// 从服务提供程序创建使用真实作用域工厂的通知器。
    /// </summary>
    /// <param name="provider">已注册订阅器和作用域工厂的服务提供程序。</param>
    /// <returns>待测试的异常通知器。</returns>
    private static ExceptionNotifier CreateNotifier(ServiceProvider provider) =>
        new(provider.GetRequiredService<IServiceScopeFactory>());

    /// <summary>
    /// 记录订阅器执行和依赖释放次数。
    /// </summary>
    public sealed class SubscriberCounter
    {
        /// <summary>
        /// 获取或设置处理次数。
        /// </summary>
        public int Handled { get; set; }

        /// <summary>
        /// 获取或设置同步释放次数。
        /// </summary>
        public int SyncDisposals { get; set; }

        /// <summary>
        /// 获取或设置异步释放次数。
        /// </summary>
        public int AsyncDisposals { get; set; }
    }

    /// <summary>
    /// 仅实现异步释放的依赖。
    /// </summary>
    public sealed class AsyncOnlyDependency : IAsyncDisposable
    {
        /// <summary>
        /// 用于记录异步释放次数的共享计数器。
        /// </summary>
        private readonly SubscriberCounter _counter;

        /// <summary>
        /// 初始化一个 <see cref="AsyncOnlyDependency" /> 类的新实例。
        /// </summary>
        /// <param name="counter">释放计数器。</param>
        public AsyncOnlyDependency(SubscriberCounter counter) => _counter = counter;

        /// <summary>
        /// 获取共享计数器。
        /// </summary>
        public SubscriberCounter Counter => _counter;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _counter.AsyncDisposals++;
            return default;
        }
    }

    /// <summary>
    /// 异步释放时抛出异常的依赖。
    /// </summary>
    public sealed class ThrowingAsyncDisposable : IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync() =>
            new(Task.FromException(new InvalidOperationException("dependency-async-dispose-failure")));
    }

    /// <summary>
    /// 正常处理通知的订阅器。
    /// </summary>
    public sealed class AsyncSubscriber : IExceptionSubscriber
    {
        /// <summary>
        /// 供处理器执行时累计计数并观察释放行为的异步依赖。
        /// </summary>
        private readonly AsyncOnlyDependency _dependency;

        /// <summary>
        /// 初始化一个 <see cref="AsyncSubscriber" /> 类的新实例。
        /// </summary>
        /// <param name="dependency">异步依赖。</param>
        public AsyncSubscriber(AsyncOnlyDependency dependency) => _dependency = dependency;

        /// <inheritdoc />
        public int Order => 10;

        /// <inheritdoc />
        public Task HandleAsync(ExceptionNotificationContext context)
        {
            _dependency.Counter.Handled++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 处理时抛出异常的订阅器。
    /// </summary>
    public sealed class FailingSubscriber : IExceptionSubscriber
    {
        /// <summary>
        /// 供失败处理器执行时累计计数并观察释放行为的异步依赖。
        /// </summary>
        private readonly AsyncOnlyDependency _dependency;

        /// <summary>
        /// 初始化一个 <see cref="FailingSubscriber" /> 类的新实例。
        /// </summary>
        /// <param name="dependency">异步依赖。</param>
        public FailingSubscriber(AsyncOnlyDependency dependency) => _dependency = dependency;

        /// <inheritdoc />
        public int Order => 10;

        /// <inheritdoc />
        public Task HandleAsync(ExceptionNotificationContext context)
        {
            _dependency.Counter.Handled++;
            return Task.FromException(new InvalidOperationException("subscriber-handle-failure"));
        }
    }
}
