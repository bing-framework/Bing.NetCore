using Bing.EventBus.Local;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bing.EventBus.Tests.Local;

/// <summary>
/// 验证本地事件递归发布的深度保护。
/// </summary>
public sealed class LocalEventBusRecursionTest
{
    /// <summary>
    /// 递归发布超过上限时应失败，并在失败后恢复当前异步发布上下文。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenDepthExceeded_ShouldFailAndRestoreContext()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new LocalEventBusOptions
        {
            MaxPublishDepth = 2
        });
        var bus = new LocalEventBus(options, provider.GetRequiredService<IServiceScopeFactory>());
        var recurse = true;
        var calls = 0;
        bus.Subscribe<RecursiveEvent>(async _ =>
        {
            calls++;
            if (recurse)
                await bus.PublishAsync(new RecursiveEvent());
        });

        await Should.ThrowAsync<InvalidOperationException>(() => bus.PublishAsync(new RecursiveEvent()));
        calls.ShouldBe(2);

        recurse = false;
        await Should.NotThrowAsync(() => bus.PublishAsync(new RecursiveEvent()));
        calls.ShouldBe(3);
    }

    /// <summary>
    /// 最大发布深度无效时应在执行处理器前失败。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenMaxDepthIsInvalid_ShouldFailBeforeHandlers()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new LocalEventBusOptions
        {
            MaxPublishDepth = 0
        });
        var bus = new LocalEventBus(options, provider.GetRequiredService<IServiceScopeFactory>());
        var calls = 0;
        bus.Subscribe<RecursiveEvent>(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        await Should.ThrowAsync<InvalidOperationException>(() => bus.PublishAsync(new RecursiveEvent()));
        calls.ShouldBe(0);
    }

    /// <summary>
    /// 用于验证事件递归分发边界的测试事件。
    /// </summary>
    private sealed class RecursiveEvent { }
}
