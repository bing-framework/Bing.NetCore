using System.Linq;
using Bing.EventBus.Local;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.EventBus.Tests;

/// <summary>
/// 验证事件处理器作用域在构造失败和异步分发路径中的所有权与释放行为。
/// </summary>
public sealed class EventHandlerScopeLifecycleTest
{
    /// <summary>
    /// 验证异步分发会等待仅实现异步释放的依赖完成清理。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithAsyncOnlyDependency_ShouldAwaitAsyncDisposal()
    {
        var counter = new DisposalCounter();
        var (provider, bus) = CreateBus(services =>
        {
            services.AddScoped(_ => new AsyncOnlyDependency(counter));
            services.AddTransient<AsyncOnlyHandler>();
        });
        using (provider)
        {
            Subscribe<TestEvent, AsyncOnlyHandler>(bus, provider);

            await bus.PublishAsync(new TestEvent());

            counter.Handled.ShouldBe(1);
            counter.AsyncDisposals.ShouldBe(1);
            counter.SyncDisposals.ShouldBe(0);
        }
    }

    /// <summary>
    /// 验证同步分发仍可在只实现同步释放的依赖上完成。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithSyncOnlyDependency_ShouldKeepSynchronousDisposal()
    {
        var counter = new DisposalCounter();
        var (provider, bus) = CreateBus(services =>
        {
            services.AddScoped(_ => new SyncDependency(counter));
            services.AddTransient<SyncHandler>();
        });
        using (provider)
        {
            Subscribe<TestEvent, SyncHandler>(bus, provider);

            await bus.PublishAsync(new TestEvent());

            counter.Handled.ShouldBe(1);
            counter.SyncDisposals.ShouldBe(1);
            counter.AsyncDisposals.ShouldBe(0);
        }
    }

    /// <summary>
    /// 验证同时实现两个释放接口的依赖在异步分发中优先走异步释放。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithDualDisposableDependency_ShouldPreferAsyncDisposalOnce()
    {
        var counter = new DisposalCounter();
        var (provider, bus) = CreateBus(services =>
        {
            services.AddScoped(_ => new DualDependency(counter));
            services.AddTransient<DualHandler>();
        });
        using (provider)
        {
            Subscribe<TestEvent, DualHandler>(bus, provider);

            await bus.PublishAsync(new TestEvent());

            counter.Handled.ShouldBe(1);
            counter.AsyncDisposals.ShouldBe(1);
            counter.SyncDisposals.ShouldBe(0);
        }
    }

    /// <summary>
    /// 验证处理器业务异常仍保持原异常，并完成异步依赖清理。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenHandlerFails_ShouldPreserveOriginalAndDisposeAsyncDependency()
    {
        var counter = new DisposalCounter();
        var (provider, bus) = CreateBus(services =>
        {
            services.AddScoped(_ => new AsyncOnlyDependency(counter));
            services.AddTransient<FailingHandler>();
        });
        using (provider)
        {
            Subscribe<TestEvent, FailingHandler>(bus, provider);

            var error = await Should.ThrowAsync<InvalidOperationException>(() =>
                bus.PublishAsync(new TestEvent()));

            error.Message.ShouldBe("handler-execution-failure");
            counter.AsyncDisposals.ShouldBe(1);
        }
    }

    /// <summary>
    /// 验证处理器构造失败时释放已创建的异步依赖，并保留主异常。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenHandlerConstructionFails_ShouldDisposeNewScope()
    {
        var counter = new DisposalCounter();
        var (provider, bus) = CreateBus(services =>
        {
            services.AddScoped(_ => new AsyncOnlyDependency(counter));
            services.AddTransient<ThrowingHandler>(sp =>
            {
                sp.GetRequiredService<AsyncOnlyDependency>();
                throw new InvalidOperationException("handler-construction-failure");
            });
        });
        using (provider)
        {
            Subscribe<TestEvent, ThrowingHandler>(bus, provider);

            var error = await Should.ThrowAsync<InvalidOperationException>(() =>
                bus.PublishAsync(new TestEvent()));

            error.Message.ShouldBe("handler-construction-failure");
            counter.AsyncDisposals.ShouldBe(1);
        }
    }

    /// <summary>
    /// 验证构造失败与清理失败同时发生时保留主异常并附加清理证据。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenConstructionAndCleanupFail_ShouldPreserveBothErrors()
    {
        var (provider, bus) = CreateBus(services =>
        {
            services.AddScoped<ThrowingAsyncDisposable>();
            services.AddTransient<ThrowingCleanupHandler>(sp =>
            {
                sp.GetRequiredService<ThrowingAsyncDisposable>();
                throw new InvalidOperationException("handler-construction-failure");
            });
        });
        using (provider)
        {
            Subscribe<TestEvent, ThrowingCleanupHandler>(bus, provider);

            var error = await Should.ThrowAsync<InvalidOperationException>(() =>
                bus.PublishAsync(new TestEvent()));

            error.Message.ShouldBe("handler-construction-failure");
            error.Data["Bing.EventBus.HandlerScopeCleanupError"].ShouldBeOfType<InvalidOperationException>()
                .Message.ShouldBe("dependency-async-dispose-failure");
        }
    }

    /// <summary>
    /// 验证业务异常与清理异常同时发生时两个异常都进入聚合结果。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenHandlerAndCleanupFail_ShouldAggregateBothErrors()
    {
        var (provider, bus) = CreateBus(services =>
        {
            services.AddScoped<ThrowingAsyncDisposable>();
            services.AddTransient<FailingThrowingCleanupHandler>();
        });
        using (provider)
        {
            Subscribe<TestEvent, FailingThrowingCleanupHandler>(bus, provider);

            var error = await Should.ThrowAsync<AggregateException>(() =>
                bus.PublishAsync(new TestEvent()));

            error.InnerExceptions.Select(exception => exception.Message).ShouldBe(
                new[] { "handler-execution-failure", "dependency-async-dispose-failure" });
        }
    }

    /// <summary>
    /// 验证先解析依赖、后构造失败的工厂入口直接清理新作用域。
    /// </summary>
    [Fact]
    public async Task GetHandlerAsync_WhenConstructionFails_ShouldDisposeScopeAndDependency()
    {
        var counter = new DisposalCounter();
        var services = new ServiceCollection();
        services.AddScoped(_ => new AsyncOnlyDependency(counter));
        services.AddTransient<ThrowingHandler>(sp =>
        {
            sp.GetRequiredService<AsyncOnlyDependency>();
            throw new InvalidOperationException("handler-construction-failure");
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var factory = new IocEventHandlerFactory(provider.GetRequiredService<IServiceScopeFactory>(),
            typeof(ThrowingHandler));

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            factory.GetHandlerAsync().AsTask());

        error.Message.ShouldBe("handler-construction-failure");
        counter.AsyncDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证同步获取入口在构造失败时释放已创建的同步作用域。
    /// </summary>
    [Fact]
    public void GetHandler_WhenConstructionFails_ShouldDisposeSyncScopeAndDependency()
    {
        var counter = new DisposalCounter();
        var services = new ServiceCollection();
        services.AddScoped(_ => new SyncDependency(counter));
        services.AddTransient<SyncThrowingHandler>(sp =>
        {
            sp.GetRequiredService<SyncDependency>();
            throw new InvalidOperationException("handler-construction-failure");
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var factory = new IocEventHandlerFactory(provider.GetRequiredService<IServiceScopeFactory>(),
            typeof(SyncThrowingHandler));

        var error = Should.Throw<InvalidOperationException>(() => factory.GetHandler());

        error.Message.ShouldBe("handler-construction-failure");
        counter.SyncDisposals.ShouldBe(1);
        counter.AsyncDisposals.ShouldBe(0);
    }

    /// <summary>
    /// 验证解析对象不是事件处理器时仍释放新建作用域并保留转换异常。
    /// </summary>
    [Fact]
    public async Task GetHandlerAsync_WhenResolvedTypeIsNotHandler_ShouldDisposeNewScope()
    {
        var counter = new DisposalCounter();
        var services = new ServiceCollection();
        services.AddScoped(_ => new AsyncOnlyDependency(counter));
        services.AddTransient<NotAHandler>(sp =>
        {
            sp.GetRequiredService<AsyncOnlyDependency>();
            return new NotAHandler();
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var factory = new IocEventHandlerFactory(provider.GetRequiredService<IServiceScopeFactory>(),
            typeof(NotAHandler));

        await Should.ThrowAsync<InvalidCastException>(() => factory.GetHandlerAsync().AsTask());

        counter.AsyncDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证旧同步工厂消费者在异步分发中保持兼容并执行原释放委托。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithLegacySynchronousFactory_ShouldKeepCompatibility()
    {
        var handler = new LegacyHandler();
        var disposed = 0;
        var (provider, bus) = CreateBus(_ => { });
        using (provider)
        {
            bus.Subscribe(typeof(TestEvent),
                new LegacyFactory(new EventHandlerDisposeWrapper(handler, () => disposed++)));

            await bus.PublishAsync(new TestEvent());

            handler.Handled.ShouldBe(1);
            disposed.ShouldBe(1);
        }
    }

    /// <summary>
    /// 验证旧同步工厂清理失败时立即终止分发并保留原清理异常。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenLegacyCleanupFails_ShouldAbortDispatchAndPreserveCleanupError()
    {
        var first = new LegacyHandler();
        var second = new LegacyHandler();
        var firstDisposals = 0;
        var (provider, bus) = CreateBus(_ => { });
        using (provider)
        {
            bus.Subscribe(typeof(TestEvent), new LegacyFactory(new EventHandlerDisposeWrapper(first, () =>
            {
                firstDisposals++;
                throw new InvalidOperationException("legacy-cleanup-failure");
            })));
            bus.Subscribe(typeof(TestEvent), new LegacyFactory(new EventHandlerDisposeWrapper(second)));

            var error = await Should.ThrowAsync<InvalidOperationException>(() =>
                bus.PublishAsync(new TestEvent()));

            error.Message.ShouldBe("legacy-cleanup-failure");
            first.Handled.ShouldBe(1);
            second.Handled.ShouldBe(0);
            firstDisposals.ShouldBe(1);
        }
    }

    /// <summary>
    /// 验证旧同步工厂业务失败与清理失败同时发生时保留两个异常并终止分发。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenLegacyHandlerAndCleanupFail_ShouldPreserveBothAndAbortDispatch()
    {
        var first = new ThrowingLegacyHandler("legacy-handler-failure");
        var second = new LegacyHandler();
        var firstDisposals = 0;
        var (provider, bus) = CreateBus(_ => { });
        using (provider)
        {
            bus.Subscribe(typeof(TestEvent), new LegacyFactory(new EventHandlerDisposeWrapper(first, () =>
            {
                firstDisposals++;
                throw new InvalidOperationException("legacy-cleanup-failure");
            })));
            bus.Subscribe(typeof(TestEvent), new LegacyFactory(new EventHandlerDisposeWrapper(second)));

            var error = await Should.ThrowAsync<AggregateException>(() =>
                bus.PublishAsync(new TestEvent()));

            error.InnerExceptions.Select(exception => exception.Message).ShouldBe(
                new[] { "legacy-handler-failure", "legacy-cleanup-failure" });
            first.Handled.ShouldBe(1);
            second.Handled.ShouldBe(0);
            firstDisposals.ShouldBe(1);
        }
    }

    /// <summary>
    /// 创建启用作用域校验的本地事件总线及其服务提供程序。
    /// </summary>
    /// <param name="configure">登记处理器和依赖的配置委托。</param>
    /// <returns>服务提供程序与本地事件总线。</returns>
    private static (ServiceProvider Provider, LocalEventBus Bus) CreateBus(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        configure(services);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var bus = new LocalEventBus(provider.GetRequiredService<IOptions<LocalEventBusOptions>>(),
            provider.GetRequiredService<IServiceScopeFactory>());
        return (provider, bus);
    }

    /// <summary>
    /// 使用依赖注入工厂为指定事件类型登记处理器。
    /// </summary>
    /// <typeparam name="TEvent">事件类型。</typeparam>
    /// <typeparam name="THandler">处理器实现类型。</typeparam>
    /// <param name="bus">目标本地事件总线。</param>
    /// <param name="provider">提供作用域工厂的服务提供程序。</param>
    private static void Subscribe<TEvent, THandler>(LocalEventBus bus, ServiceProvider provider)
        where TEvent : class
        where THandler : IEventHandler
    {
        bus.Subscribe(typeof(TEvent), new IocEventHandlerFactory(
            provider.GetRequiredService<IServiceScopeFactory>(), typeof(THandler)));
    }

    /// <summary>
    /// 测试事件。
    /// </summary>
    public sealed class TestEvent
    {
        /// <summary>
        /// 获取或设置事件值。
        /// </summary>
        public int Value { get; set; }
    }

    /// <summary>
    /// 记录释放次数的共享计数器。
    /// </summary>
    public sealed class DisposalCounter
    {
        /// <summary>
        /// 获取或设置处理器执行次数。
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
    /// 仅实现同步释放的依赖。
    /// </summary>
    public sealed class SyncDependency : IDisposable
    {
        /// <summary>
        /// 用于记录同步释放次数的共享计数器。
        /// </summary>
        private readonly DisposalCounter _counter;

        /// <summary>
        /// 初始化一个 <see cref="SyncDependency" /> 类的新实例。
        /// </summary>
        /// <param name="counter">释放计数器。</param>
        public SyncDependency(DisposalCounter counter) => _counter = counter;

        /// <summary>
        /// 获取共享计数器。
        /// </summary>
        public DisposalCounter Counter => _counter;

        /// <inheritdoc />
        public void Dispose() => _counter.SyncDisposals++;
    }

    /// <summary>
    /// 仅实现异步释放的依赖。
    /// </summary>
    public sealed class AsyncOnlyDependency : IAsyncDisposable
    {
        /// <summary>
        /// 用于记录异步释放次数的共享计数器。
        /// </summary>
        private readonly DisposalCounter _counter;

        /// <summary>
        /// 初始化一个 <see cref="AsyncOnlyDependency" /> 类的新实例。
        /// </summary>
        /// <param name="counter">释放计数器。</param>
        public AsyncOnlyDependency(DisposalCounter counter) => _counter = counter;

        /// <summary>
        /// 获取共享计数器。
        /// </summary>
        public DisposalCounter Counter => _counter;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _counter.AsyncDisposals++;
            return default;
        }
    }

    /// <summary>
    /// 同时实现同步和异步释放的依赖。
    /// </summary>
    public sealed class DualDependency : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 用于分别记录同步和异步释放次数的共享计数器。
        /// </summary>
        private readonly DisposalCounter _counter;

        /// <summary>
        /// 初始化一个 <see cref="DualDependency" /> 类的新实例。
        /// </summary>
        /// <param name="counter">释放计数器。</param>
        public DualDependency(DisposalCounter counter) => _counter = counter;

        /// <summary>
        /// 获取共享计数器。
        /// </summary>
        public DisposalCounter Counter => _counter;

        /// <inheritdoc />
        public void Dispose() => _counter.SyncDisposals++;

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
    /// 使用异步依赖的事件处理器。
    /// </summary>
    public sealed class AsyncOnlyHandler : ILocalEventHandler<TestEvent>
    {
        /// <summary>
        /// 供处理器执行时累计处理计数并观察异步释放的依赖。
        /// </summary>
        private readonly AsyncOnlyDependency _dependency;

        /// <summary>
        /// 初始化一个 <see cref="AsyncOnlyHandler" /> 类的新实例。
        /// </summary>
        /// <param name="dependency">异步依赖。</param>
        public AsyncOnlyHandler(AsyncOnlyDependency dependency) => _dependency = dependency;

        /// <inheritdoc />
        public Task HandleAsync(TestEvent eventData)
        {
            _dependency.Counter.Handled++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 使用同步依赖的事件处理器。
    /// </summary>
    public sealed class SyncHandler : ILocalEventHandler<TestEvent>
    {
        /// <summary>
        /// 供处理器执行时累计处理计数并观察同步释放的依赖。
        /// </summary>
        private readonly SyncDependency _dependency;

        /// <summary>
        /// 初始化一个 <see cref="SyncHandler" /> 类的新实例。
        /// </summary>
        /// <param name="dependency">同步依赖。</param>
        public SyncHandler(SyncDependency dependency) => _dependency = dependency;

        /// <inheritdoc />
        public Task HandleAsync(TestEvent eventData)
        {
            _dependency.Counter.Handled++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 使用双接口依赖的事件处理器。
    /// </summary>
    public sealed class DualHandler : ILocalEventHandler<TestEvent>
    {
        /// <summary>
        /// 供处理器执行时累计处理计数并观察双接口释放行为的依赖。
        /// </summary>
        private readonly DualDependency _dependency;

        /// <summary>
        /// 初始化一个 <see cref="DualHandler" /> 类的新实例。
        /// </summary>
        /// <param name="dependency">双接口依赖。</param>
        public DualHandler(DualDependency dependency) => _dependency = dependency;

        /// <inheritdoc />
        public Task HandleAsync(TestEvent eventData)
        {
            _dependency.Counter.Handled++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 执行时抛出异常的处理器。
    /// </summary>
    public sealed class FailingHandler : ILocalEventHandler<TestEvent>
    {
        /// <summary>
        /// 初始化一个 <see cref="FailingHandler" /> 类的新实例。
        /// </summary>
        /// <param name="dependency">异步依赖。</param>
        public FailingHandler(AsyncOnlyDependency dependency) { }

        /// <inheritdoc />
        public Task HandleAsync(TestEvent eventData) =>
            Task.FromException(new InvalidOperationException("handler-execution-failure"));
    }

    /// <summary>
    /// 构造时抛出异常的处理器。
    /// </summary>
    public sealed class ThrowingHandler : ILocalEventHandler<TestEvent>
    {
        /// <inheritdoc />
        public Task HandleAsync(TestEvent eventData) => Task.CompletedTask;
    }

    /// <summary>
    /// 使用同步依赖且构造时抛出异常的处理器。
    /// </summary>
    public sealed class SyncThrowingHandler : ILocalEventHandler<TestEvent>
    {
        /// <inheritdoc />
        public Task HandleAsync(TestEvent eventData) => Task.CompletedTask;
    }

    /// <summary>
    /// 不实现事件处理器契约的解析目标。
    /// </summary>
    public sealed class NotAHandler
    {
    }

    /// <summary>
    /// 构造时抛出异常且清理也会失败的处理器。
    /// </summary>
    public sealed class ThrowingCleanupHandler : ILocalEventHandler<TestEvent>
    {
        /// <inheritdoc />
        public Task HandleAsync(TestEvent eventData) => Task.CompletedTask;
    }

    /// <summary>
    /// 业务执行与清理都会失败的处理器。
    /// </summary>
    public sealed class FailingThrowingCleanupHandler : ILocalEventHandler<TestEvent>
    {
        /// <summary>
        /// 初始化一个 <see cref="FailingThrowingCleanupHandler" /> 类的新实例。
        /// </summary>
        /// <param name="dependency">异步释放会抛出异常的依赖。</param>
        public FailingThrowingCleanupHandler(ThrowingAsyncDisposable dependency) { }

        /// <inheritdoc />
        public Task HandleAsync(TestEvent eventData) =>
            Task.FromException(new InvalidOperationException("handler-execution-failure"));
    }

    /// <summary>
    /// 旧同步工厂使用的处理器。
    /// </summary>
    public sealed class LegacyHandler : ILocalEventHandler<TestEvent>
    {
        /// <summary>
        /// 获取执行次数。
        /// </summary>
        public int Handled { get; private set; }

        /// <inheritdoc />
        public Task HandleAsync(TestEvent eventData)
        {
            Handled++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 旧同步工厂使用且业务执行失败的事件处理器。
    /// </summary>
    public sealed class ThrowingLegacyHandler : ILocalEventHandler<TestEvent>
    {
        /// <summary>
        /// 处理器失败时用于构造异常的固定业务消息。
        /// </summary>
        private readonly string _message;

        /// <summary>
        /// 初始化一个 <see cref="ThrowingLegacyHandler" /> 类的新实例。
        /// </summary>
        /// <param name="message">业务失败消息。</param>
        public ThrowingLegacyHandler(string message) => _message = message;

        /// <summary>
        /// 获取执行次数。
        /// </summary>
        public int Handled { get; private set; }

        /// <inheritdoc />
        public Task HandleAsync(TestEvent eventData)
        {
            Handled++;
            return Task.FromException(new InvalidOperationException(_message));
        }
    }

    /// <summary>
    /// 只实现旧同步契约的工厂。
    /// </summary>
    private sealed class LegacyFactory : IEventHandlerFactory
    {
        /// <summary>
        /// 每次获取时返回的处理器包装器。
        /// </summary>
        private readonly IEventHandlerDisposeWrapper _wrapper;

        /// <summary>
        /// 初始化一个 <see cref="LegacyFactory" /> 类的新实例。
        /// </summary>
        /// <param name="wrapper">要返回的处理器包装器。</param>
        public LegacyFactory(IEventHandlerDisposeWrapper wrapper) => _wrapper = wrapper;

        /// <inheritdoc />
        public IEventHandlerDisposeWrapper GetHandler() => _wrapper;

        /// <inheritdoc />
        public bool IsInFactories(List<IEventHandlerFactory> handlerFactories) =>
            handlerFactories.Contains(this);
    }
}
