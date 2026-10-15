using Bing.EventBus.Local;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EventBus.Tests.Local;

/// <summary>
/// 本地事件总线的并发订阅与分发测试。
/// </summary>
public class LocalEventBusConcurrencyTest
{
    /// <summary>
    /// 用于验证本地事件总线并发行为的测试事件。
    /// </summary>
    private sealed class TestEvent : IEvent { }

    /// <summary>
    /// 分发开始后修改订阅，不应改变当前分发的处理器集合。
    /// </summary>
    [Fact]
    public async Task PublishAsync_ConcurrentSubscriptionChanges_UsesInitialSnapshot()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var bus = new LocalEventBus(Microsoft.Extensions.Options.Options.Create(new LocalEventBusOptions()),
            provider.GetRequiredService<IServiceScopeFactory>());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldCalls = 0;
        var newCalls = 0;

        bus.Subscribe<TestEvent>(async _ =>
        {
            entered.TrySetResult();
            await release.Task;
        });
        using var oldSubscription = bus.Subscribe<TestEvent>(_ =>
        {
            Interlocked.Increment(ref oldCalls);
            return Task.CompletedTask;
        });

        var publishing = bus.PublishAsync(new TestEvent());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        oldSubscription.Dispose();
        using var newSubscription = bus.Subscribe<TestEvent>(_ =>
        {
            Interlocked.Increment(ref newCalls);
            return Task.CompletedTask;
        });
        release.TrySetResult();

        await publishing;
        Assert.Equal(1, oldCalls);
        Assert.Equal(0, newCalls);

        await bus.PublishAsync(new TestEvent());
        Assert.Equal(1, oldCalls);
        Assert.Equal(1, newCalls);
    }

    /// <summary>
    /// 验证最后一个订阅取消后不会永久保留事件类型键。
    /// </summary>
    [Fact]
    public void UnsubscribeLastHandler_ShouldRemoveEmptyEventTypeEntry()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var bus = new InspectableLocalEventBus(Microsoft.Extensions.Options.Options.Create(new LocalEventBusOptions()),
            provider.GetRequiredService<IServiceScopeFactory>());

        bus.UnsubscribeAll<TestEvent>();
        Assert.Equal(0, bus.HandlerTypeCount);

        using var subscription = bus.Subscribe<TestEvent>(_ => Task.CompletedTask);
        Assert.Equal(1, bus.HandlerTypeCount);
        subscription.Dispose();
        Assert.Equal(0, bus.HandlerTypeCount);

        bus.UnsubscribeAll<TestEvent>();
        Assert.Equal(0, bus.HandlerTypeCount);
    }

    /// <summary>
    /// 暴露事件类型数量以验证空列表回收。
    /// </summary>
    private sealed class InspectableLocalEventBus : LocalEventBus
    {
        /// <summary>
        /// 初始化一个 <see cref="InspectableLocalEventBus" /> 类型的实例。
        /// </summary>
        /// <param name="options">本地事件总线选项。</param>
        /// <param name="serviceScopeFactory">服务作用域工厂。</param>
        public InspectableLocalEventBus(Microsoft.Extensions.Options.IOptions<LocalEventBusOptions> options,
            IServiceScopeFactory serviceScopeFactory) : base(options, serviceScopeFactory)
        {
        }

        /// <summary>
        /// 获取当前保留事件类型数量。
        /// </summary>
        public int HandlerTypeCount => HandlerFactories.Count;
    }
}
