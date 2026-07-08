using Bing.EventBus.Local;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bing.EventBus.Tests.Local;

/// <summary>
/// LocalEventBus 异常传播行为测试
/// </summary>
public class LocalEventBusExceptionTest
{
    // ==================== 辅助 ====================

    private static LocalEventBus CreateBus()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new LocalEventBusOptions());
        return new LocalEventBus(options, sp.GetRequiredService<IServiceScopeFactory>());
    }

    private class FaultyEvent : IEvent { }

    // ==================== 单个处理器抛出异常 ====================

    /// <summary>
    /// 测试目的：处理器抛出异常时，PublishAsync 应将该异常传播给调用方。
    /// </summary>
    [Fact]
    public async Task PublishAsync_HandlerThrows_ExceptionPropagated()
    {
        // Arrange
        var bus = CreateBus();
        bus.Subscribe<FaultyEvent>(_ => throw new InvalidOperationException("handler error"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => bus.PublishAsync(new FaultyEvent()));
    }

    /// <summary>
    /// 测试目的：一个处理器抛出异常，另一个处理器依然被调用（所有处理器均触发）。
    /// </summary>
    [Fact]
    public async Task PublishAsync_OneHandlerThrows_OtherHandlerStillCalled()
    {
        // Arrange
        var bus = CreateBus();
        var secondCalled = false;

        // 第一个处理器：抛出异常
        bus.Subscribe<FaultyEvent>(_ => throw new InvalidOperationException("first fails"));

        // 第二个处理器：正常执行
        bus.Subscribe<FaultyEvent>(_ =>
        {
            secondCalled = true;
            return Task.CompletedTask;
        });

        // Act：PublishAsync 会收集所有异常后再抛出
        try
        {
            await bus.PublishAsync(new FaultyEvent());
        }
        catch
        {
            // 忽略异常，只验证第二个处理器是否被调用
        }

        // Assert
        secondCalled.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：多个处理器均抛出异常时，PublishAsync 应抛出 AggregateException。
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleHandlersThrow_AggregateExceptionThrown()
    {
        // Arrange
        var bus = CreateBus();
        bus.Subscribe<FaultyEvent>(_ => throw new InvalidOperationException("error1"));
        bus.Subscribe<FaultyEvent>(_ => throw new ArgumentException("error2"));

        // Act & Assert
        var ex = await Should.ThrowAsync<AggregateException>(() => bus.PublishAsync(new FaultyEvent()));
        ex.InnerExceptions.Count.ShouldBe(2);
    }

    /// <summary>
    /// 测试目的：只有一个处理器且抛出异常时，直接重新抛出原始异常（不包装为 AggregateException）。
    /// </summary>
    [Fact]
    public async Task PublishAsync_SingleHandlerThrows_OriginalExceptionRethrown()
    {
        // Arrange
        var bus = CreateBus();
        bus.Subscribe<FaultyEvent>(_ => throw new InvalidOperationException("single error"));

        // Act & Assert：应该是原始异常类型，不是 AggregateException
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => bus.PublishAsync(new FaultyEvent()));
        ex.Message.ShouldBe("single error");
    }

    // ==================== 异步处理器抛出 ====================

    /// <summary>
    /// 测试目的：异步处理器 await 后抛出的异常，也应被正确传播。
    /// </summary>
    [Fact]
    public async Task PublishAsync_AsyncHandlerThrows_ExceptionPropagated()
    {
        // Arrange
        var bus = CreateBus();
        bus.Subscribe<FaultyEvent>(async _ =>
        {
            await Task.Yield();
            throw new TimeoutException("async error");
        });

        // Act & Assert
        await Should.ThrowAsync<TimeoutException>(() => bus.PublishAsync(new FaultyEvent()));
    }

    // ==================== 正常处理器不受影响 ====================

    /// <summary>
    /// 测试目的：所有处理器正常完成时，不应抛出任何异常。
    /// </summary>
    [Fact]
    public async Task PublishAsync_AllHandlersSucceed_NoException()
    {
        // Arrange
        var bus = CreateBus();
        bus.Subscribe<FaultyEvent>(_ => Task.CompletedTask);
        bus.Subscribe<FaultyEvent>(_ => Task.CompletedTask);

        // Act & Assert
        await Should.NotThrowAsync(() => bus.PublishAsync(new FaultyEvent()));
    }
}
