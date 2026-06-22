using System;
using Bing.EventBus;
using Shouldly;
using Xunit;

namespace Bing.EventBus.Tests;

/// <summary>
/// MessageEvent 单元测试
/// </summary>
public class MessageEventTest
{
    // ════════════════════════════════════════════════════════════════
    // 默认构造
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后 Id 应为非空字符串（由 Guid.NewGuid 生成）。
    /// </summary>
    [Fact]
    public void Ctor_Default_IdShouldBeNonEmpty()
    {
        // Act
        var evt = new MessageEvent();

        // Assert
        evt.Id.ShouldNotBeNullOrWhiteSpace();
        Guid.TryParse(evt.Id, out _).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：默认构造后 Time 应被初始化（不为 default/MinValue）。
    /// </summary>
    [Fact]
    public void Ctor_Default_TimeShouldBeInitialized()
    {
        // Arrange
        var before = DateTime.Now.AddSeconds(-1);

        // Act
        var evt = new MessageEvent();

        // Assert
        evt.Time.ShouldBeGreaterThan(before);
    }

    /// <summary>
    /// 测试目的：两个实例的 Id 应互不相同（Guid 唯一性）。
    /// </summary>
    [Fact]
    public void Ctor_TwoInstances_ShouldHaveDifferentIds()
    {
        // Act
        var e1 = new MessageEvent();
        var e2 = new MessageEvent();

        // Assert
        e1.Id.ShouldNotBe(e2.Id);
    }

    // ════════════════════════════════════════════════════════════════
    // 属性读写
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Name 属性可读写，且读取值与写入值一致。
    /// </summary>
    [Fact]
    public void Name_SetAndGet_ShouldReturnSameValue()
    {
        // Arrange
        var evt = new MessageEvent();

        // Act
        evt.Name = "OrderCreated";

        // Assert
        evt.Name.ShouldBe("OrderCreated");
    }

    /// <summary>
    /// 测试目的：Data 属性可读写任意对象。
    /// </summary>
    [Fact]
    public void Data_SetAndGet_ShouldReturnSameReference()
    {
        // Arrange
        var evt = new MessageEvent();
        var data = new { OrderId = 42 };

        // Act
        evt.Data = data;

        // Assert
        evt.Data.ShouldBeSameAs(data);
    }

    /// <summary>
    /// 测试目的：Callback 属性可读写。
    /// </summary>
    [Fact]
    public void Callback_SetAndGet_ShouldReturnSameValue()
    {
        // Arrange
        var evt = new MessageEvent { Callback = "on_order_created" };

        // Assert
        evt.Callback.ShouldBe("on_order_created");
    }

    /// <summary>
    /// 测试目的：Send 默认值应为 false。
    /// </summary>
    [Fact]
    public void Send_Default_ShouldBeFalse()
    {
        // Act
        var evt = new MessageEvent();

        // Assert
        evt.Send.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：Send 属性可被设置为 true。
    /// </summary>
    [Fact]
    public void Send_SetTrue_ShouldReturnTrue()
    {
        // Arrange
        var evt = new MessageEvent { Send = true };

        // Assert
        evt.Send.ShouldBeTrue();
    }

    // ════════════════════════════════════════════════════════════════
    // ToString
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ToString 应包含事件标识字段。
    /// </summary>
    [Fact]
    public void ToString_ShouldContainEventId()
    {
        // Arrange
        var evt = new MessageEvent();

        // Act
        var str = evt.ToString();

        // Assert
        str.ShouldContain(evt.Id);
    }

    /// <summary>
    /// 测试目的：ToString 设置 Name 时应包含消息名称。
    /// </summary>
    [Fact]
    public void ToString_WhenNameSet_ShouldContainName()
    {
        // Arrange
        var evt = new MessageEvent { Name = "TestEvent" };

        // Act
        var str = evt.ToString();

        // Assert
        str.ShouldContain("TestEvent");
    }

    /// <summary>
    /// 测试目的：ToString 不设置 Name 时，输出不应包含"消息名称"标签（避免空字段干扰日志）。
    /// </summary>
    [Fact]
    public void ToString_WhenNameNotSet_ShouldNotContainNameLabel()
    {
        // Arrange
        var evt = new MessageEvent { Name = null };

        // Act
        var str = evt.ToString();

        // Assert
        str.ShouldNotContain("消息名称");
    }

    /// <summary>
    /// 测试目的：ToString 设置 Callback 时应包含回调名称。
    /// </summary>
    [Fact]
    public void ToString_WhenCallbackSet_ShouldContainCallback()
    {
        // Arrange
        var evt = new MessageEvent { Callback = "my_callback" };

        // Act
        var str = evt.ToString();

        // Assert
        str.ShouldContain("my_callback");
    }

    /// <summary>
    /// 测试目的：ToString 不设置 Callback 时，输出不应包含"回调名称"标签。
    /// </summary>
    [Fact]
    public void ToString_WhenCallbackNotSet_ShouldNotContainCallbackLabel()
    {
        // Arrange
        var evt = new MessageEvent { Callback = null };

        // Act
        var str = evt.ToString();

        // Assert
        str.ShouldNotContain("回调名称");
    }
}
