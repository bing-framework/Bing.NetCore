using Bing.Domain.Entities.Events;

namespace Bing.Domain.Entities;

/// <summary>
/// 聚合根 - 领域事件 测试
/// </summary>
public class AggregateRootDomainEventsTest
{
    /// <summary>
    /// 测试用领域事件样例
    /// </summary>
    private class SampleDomainEvent : DomainEvent
    {
        /// <summary>消息内容</summary>
        public string Message { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        public SampleDomainEvent(string message) => Message = message;
    }

    /// <summary>
    /// 测试目的：新建聚合根的领域事件集合初始为 null（懒初始化，未触发任何 Add）。
    /// </summary>
    [Fact]
    public void GetDomainEvents_Initial_ShouldBeNull()
    {
        // Arrange
        var sample = new AggregateRootSample();

        // Act
        var events = sample.GetDomainEvents();

        // Assert
        events.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：添加一个领域事件后，集合应包含该事件且数量为 1。
    /// </summary>
    [Fact]
    public void AddDomainEvent_Single_ShouldBeInCollection()
    {
        // Arrange
        var sample = new AggregateRootSample();
        var domainEvent = new SampleDomainEvent("created");

        // Act
        sample.AddDomainEvent(domainEvent);

        // Assert
        var events = sample.GetDomainEvents();
        events.ShouldNotBeNull();
        events.Count.ShouldBe(1);
        events.ShouldContain(domainEvent);
    }

    /// <summary>
    /// 测试目的：连续添加多个领域事件后，集合应按顺序包含所有事件。
    /// </summary>
    [Fact]
    public void AddDomainEvent_Multiple_ShouldContainAll()
    {
        // Arrange
        var sample = new AggregateRootSample();
        var event1 = new SampleDomainEvent("event1");
        var event2 = new SampleDomainEvent("event2");

        // Act
        sample.AddDomainEvent(event1);
        sample.AddDomainEvent(event2);

        // Assert
        var events = sample.GetDomainEvents();
        events.Count.ShouldBe(2);
        events.ElementAt(0).ShouldBe(event1);
        events.ElementAt(1).ShouldBe(event2);
    }

    /// <summary>
    /// 测试目的：ClearDomainEvents 后集合应为空（不为 null，已初始化）。
    /// </summary>
    [Fact]
    public void ClearDomainEvents_AfterAdd_ShouldBeEmpty()
    {
        // Arrange
        var sample = new AggregateRootSample();
        sample.AddDomainEvent(new SampleDomainEvent("created"));

        // Act
        sample.ClearDomainEvents();

        // Assert
        sample.GetDomainEvents().ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：RemoveDomainEvent 应只移除指定事件，其他事件保持不变。
    /// </summary>
    [Fact]
    public void RemoveDomainEvent_ShouldOnlyRemoveSpecificEvent()
    {
        // Arrange
        var sample = new AggregateRootSample();
        var event1 = new SampleDomainEvent("event1");
        var event2 = new SampleDomainEvent("event2");
        sample.AddDomainEvent(event1);
        sample.AddDomainEvent(event2);

        // Act
        sample.RemoveDomainEvent(event1);

        // Assert
        var events = sample.GetDomainEvents();
        events.Count.ShouldBe(1);
        events.ShouldContain(event2);
        events.ShouldNotContain(event1);
    }

    /// <summary>
    /// 测试目的：未添加任何事件时调用 ClearDomainEvents 不应抛出异常。
    /// </summary>
    [Fact]
    public void ClearDomainEvents_WhenNoEvents_ShouldNotThrow()
    {
        // Arrange
        var sample = new AggregateRootSample();

        // Act & Assert
        Should.NotThrow(() => sample.ClearDomainEvents());
    }

    /// <summary>
    /// 测试目的：未添加任何事件时调用 RemoveDomainEvent 不应抛出异常。
    /// </summary>
    [Fact]
    public void RemoveDomainEvent_WhenNoEvents_ShouldNotThrow()
    {
        // Arrange
        var sample = new AggregateRootSample();
        var domainEvent = new SampleDomainEvent("event");

        // Act & Assert
        Should.NotThrow(() => sample.RemoveDomainEvent(domainEvent));
    }

    /// <summary>
    /// 测试目的：允许向同一聚合根重复添加相同实例的领域事件（无去重）。
    /// </summary>
    [Fact]
    public void AddDomainEvent_SameInstance_AllowsDuplicates()
    {
        // Arrange
        var sample = new AggregateRootSample();
        var domainEvent = new SampleDomainEvent("event");

        // Act
        sample.AddDomainEvent(domainEvent);
        sample.AddDomainEvent(domainEvent);

        // Assert
        var events = sample.GetDomainEvents();
        events.Count.ShouldBe(2);
    }
}
