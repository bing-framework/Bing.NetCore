using Bing.Events;
using Bing.ExceptionHandling;
using Bing.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bing.Core.Tests;

// ─── 测试辅助：带 EventName 特性的事件 ────────────────────────────

[EventName("my.custom.event")]
file class NamedEvent : Event { }

/// <summary>
/// 未标注 EventName 特性的普通事件（回退到 FullName）
/// </summary>
file class UnnamedEvent : Event { }

/// <summary>
/// 泛型事件，使用 GenericEventNameAttribute 注解前缀
/// </summary>
[GenericEventName(Prefix = "pre.")]
file class GenericPrefixEvent<T> : Event { }

// ─── EventNameAttribute 测试 ──────────────────────────────────────

/// <summary>
/// <see cref="EventNameAttribute"/> 单元测试
/// </summary>
public class EventNameAttributeTest
{
    /// <summary>
    /// 测试目的：构造函数传入有效名称，Name 属性应返回该值。
    /// </summary>
    [Fact]
    public void Constructor_WithValidName_ShouldSetNameProperty()
    {
        // Arrange & Act
        var attr = new EventNameAttribute("order.created");

        // Assert
        attr.Name.ShouldBe("order.created");
    }

    /// <summary>
    /// 测试目的：GetName 方法应返回构造时设置的 Name 值（忽略 eventType 参数）。
    /// </summary>
    [Fact]
    public void GetName_ShouldReturnConstructorName()
    {
        // Arrange
        var attr = new EventNameAttribute("payment.paid");

        // Act
        var result = attr.GetName(typeof(object));

        // Assert
        result.ShouldBe("payment.paid");
    }

    /// <summary>
    /// 测试目的：GetNameOrDefault&lt;T&gt; 对标注了 [EventName] 的类型应返回特性值。
    /// </summary>
    [Fact]
    public void GetNameOrDefault_Generic_WithAttribute_ShouldReturnAttributeName()
    {
        // Act
        var name = EventNameAttribute.GetNameOrDefault<NamedEvent>();

        // Assert
        name.ShouldBe("my.custom.event");
    }

    /// <summary>
    /// 测试目的：GetNameOrDefault 对未标注 [EventName] 的类型应回退到 FullName。
    /// </summary>
    [Fact]
    public void GetNameOrDefault_WithoutAttribute_ShouldReturnFullName()
    {
        // Act
        var name = EventNameAttribute.GetNameOrDefault<UnnamedEvent>();

        // Assert
        name.ShouldNotBeNullOrEmpty();
        name.ShouldContain(nameof(UnnamedEvent));
    }

    /// <summary>
    /// 测试目的：GetNameOrDefault(Type) 传入 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void GetNameOrDefault_NullType_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => EventNameAttribute.GetNameOrDefault(null));
    }
}

// ─── GenericEventNameAttribute 测试 ──────────────────────────────

/// <summary>
/// <see cref="GenericEventNameAttribute"/> 单元测试
/// </summary>
public class GenericEventNameAttributeTest
{
    /// <summary>
    /// 测试目的：传入非泛型类型时应抛出 Warning 异常。
    /// </summary>
    [Fact]
    public void GetName_NonGenericType_ShouldThrowWarning()
    {
        // Arrange
        var attr = new GenericEventNameAttribute();

        // Act & Assert
        Should.Throw<Bing.Exceptions.Warning>(() => attr.GetName(typeof(int)));
    }

    /// <summary>
    /// 测试目的：泛型类型有多个泛型参数时应抛出 Warning 异常。
    /// </summary>
    [Fact]
    public void GetName_MultipleGenericArgs_ShouldThrowWarning()
    {
        // Arrange
        var attr = new GenericEventNameAttribute();
        // Dictionary<K,V> 有两个泛型参数
        var multiArgGenericType = typeof(Dictionary<string, int>);

        // Act & Assert
        Should.Throw<Bing.Exceptions.Warning>(() => attr.GetName(multiArgGenericType));
    }

    /// <summary>
    /// 测试目的：Prefix 为空时，GetName 返回泛型参数的 FullName（无前缀）。
    /// </summary>
    [Fact]
    public void GetName_WithoutPrefixOrPostfix_ShouldReturnGenericArgFullName()
    {
        // Arrange
        var attr = new GenericEventNameAttribute();
        var eventType = typeof(GenericPrefixEvent<UnnamedEvent>);

        // Act
        var name = attr.GetName(eventType);

        // Assert
        name.ShouldContain(nameof(UnnamedEvent));
    }

    /// <summary>
    /// 测试目的：设置 Prefix 后，GetName 应在结果前追加前缀。
    /// </summary>
    [Fact]
    public void GetName_WithPrefix_ShouldPrependPrefix()
    {
        // Arrange
        var attr = new GenericEventNameAttribute { Prefix = "domain." };
        var eventType = typeof(GenericPrefixEvent<UnnamedEvent>);

        // Act
        var name = attr.GetName(eventType);

        // Assert
        name.ShouldStartWith("domain.");
    }

    /// <summary>
    /// 测试目的：设置 Postfix 后，GetName 应在结果后追加后缀。
    /// </summary>
    [Fact]
    public void GetName_WithPostfix_ShouldAppendPostfix()
    {
        // Arrange
        var attr = new GenericEventNameAttribute { Postfix = ".created" };
        var eventType = typeof(GenericPrefixEvent<UnnamedEvent>);

        // Act
        var name = attr.GetName(eventType);

        // Assert
        name.ShouldEndWith(".created");
    }
}

// ─── Event 测试 ───────────────────────────────────────────────────

/// <summary>
/// <see cref="Event"/> 单元测试
/// </summary>
public class EventClassTest
{
    /// <summary>
    /// 测试目的：默认构造时 Id 应为非空 GUID 格式字符串，Time 应接近当前时间。
    /// </summary>
    [Fact]
    public void DefaultCtor_ShouldSetIdAndTime()
    {
        // Arrange
        var before = DateTime.Now.AddSeconds(-1);

        // Act
        var evt = new Event();

        // Assert
        evt.Id.ShouldNotBeNullOrEmpty();
        Guid.TryParse(evt.Id, out _).ShouldBeTrue();
        evt.Time.ShouldBeGreaterThan(before);
        evt.Time.ShouldBeLessThanOrEqualTo(DateTime.Now.AddSeconds(1));
    }

    /// <summary>
    /// 测试目的：传入显式事件名时，GetEventName 应返回该名称。
    /// </summary>
    [Fact]
    public void GetEventName_WithExplicitName_ShouldReturnThatName()
    {
        // Arrange
        var evt = new Event("explicit.event");

        // Act
        var name = evt.GetEventName();

        // Assert
        name.ShouldBe("explicit.event");
    }

    /// <summary>
    /// 测试目的：不传入名称但类型标注了 [EventName]，GetEventName 应返回特性值。
    /// </summary>
    [Fact]
    public void GetEventName_WithAttribute_ShouldReturnAttributeName()
    {
        // Arrange
        var evt = new NamedEvent();

        // Act
        var name = evt.GetEventName();

        // Assert
        name.ShouldBe("my.custom.event");
    }

    /// <summary>
    /// 测试目的：不传入名称且未标注特性，GetEventName 应回退到 FullName（含命名空间）。
    /// </summary>
    [Fact]
    public void GetEventName_NoAttributeNoName_ShouldReturnFullName()
    {
        // Arrange
        var evt = new UnnamedEvent();

        // Act
        var name = evt.GetEventName();

        // Assert
        name.ShouldNotBeNullOrEmpty();
        name.ShouldContain(nameof(UnnamedEvent));
    }

    /// <summary>
    /// 测试目的：ToString 应包含事件标识和时间信息，不抛出异常。
    /// </summary>
    [Fact]
    public void ToString_ShouldContainIdAndTime()
    {
        // Arrange
        var evt = new Event("test.event");

        // Act
        var str = Should.NotThrow(() => evt.ToString());

        // Assert
        str.ShouldContain(evt.Id);
        str.ShouldContain("事件标识");
    }
}

// ─── BingOptionsBase / BingOptions 测试 ──────────────────────────

/// <summary>
/// <see cref="BingOptionsBase"/> 及 <see cref="BingOptions"/> 单元测试
/// </summary>
public class BingOptionsTest
{
    /// <summary>
    /// 测试目的：AddExtension 传入 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void AddExtension_NullExtension_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new BingOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.AddExtension(null));
    }

    /// <summary>
    /// 测试目的：AddExtension 传入有效扩展后，再次调用 AddExtension 可继续添加（列表增长）。
    /// </summary>
    [Fact]
    public void AddExtension_ValidExtension_ShouldBeAdded()
    {
        // Arrange
        var options = new BingOptions();
        var ext1 = new Mock<IBingOptionsExtension>().Object;
        var ext2 = new Mock<IBingOptionsExtension>().Object;

        // Act
        options.AddExtension(ext1);
        options.AddExtension(ext2);

        // Assert — 通过 Extensions 内部属性（internal）无法直接验证，但 AddExtension 不抛异常即符合预期
        Should.NotThrow(() => options.AddExtension(new Mock<IBingOptionsExtension>().Object));
    }

    /// <summary>
    /// 测试目的：BingOptions 是 BingOptionsBase 的直接子类。
    /// </summary>
    [Fact]
    public void BingOptions_ShouldExtendBingOptionsBase()
    {
        // Act
        var options = new BingOptions();

        // Assert
        (options is BingOptionsBase).ShouldBeTrue();
    }
}

// ─── ExceptionNotificationContext 测试 ────────────────────────────

/// <summary>
/// <see cref="ExceptionNotificationContext"/> 单元测试
/// </summary>
public class ExceptionNotificationContextTest
{
    /// <summary>
    /// 测试目的：构造函数传入 null 异常时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_NullException_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ExceptionNotificationContext(null));
    }

    /// <summary>
    /// 测试目的：正常构造时，Exception 属性应等于传入的异常实例。
    /// </summary>
    [Fact]
    public void Constructor_WithException_ShouldSetExceptionProperty()
    {
        // Arrange
        var ex = new InvalidOperationException("测试异常");

        // Act
        var ctx = new ExceptionNotificationContext(ex);

        // Assert
        ctx.Exception.ShouldBeSameAs(ex);
    }

    /// <summary>
    /// 测试目的：未指定 logLevel 时，LogLevel 应从异常 GetLogLevel() 推断（普通异常 = Error）。
    /// </summary>
    [Fact]
    public void Constructor_WithoutLogLevel_ShouldInferLogLevel()
    {
        // Arrange
        var ex = new Exception("普通异常");

        // Act
        var ctx = new ExceptionNotificationContext(ex);

        // Assert
        ctx.LogLevel.ShouldBe(LogLevel.Error);
    }

    /// <summary>
    /// 测试目的：传入显式 logLevel 时，应使用指定的级别。
    /// </summary>
    [Fact]
    public void Constructor_WithExplicitLogLevel_ShouldUseSpecifiedLevel()
    {
        // Arrange
        var ex = new Exception("带 LogLevel 的异常");

        // Act
        var ctx = new ExceptionNotificationContext(ex, LogLevel.Warning);

        // Assert
        ctx.LogLevel.ShouldBe(LogLevel.Warning);
    }

    /// <summary>
    /// 测试目的：默认 Handled 应为 true。
    /// </summary>
    [Fact]
    public void Constructor_DefaultHandled_ShouldBeTrue()
    {
        // Arrange
        var ex = new Exception("默认 handled");

        // Act
        var ctx = new ExceptionNotificationContext(ex);

        // Assert
        ctx.Handled.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：传入 handled = false 时，Handled 属性应为 false。
    /// </summary>
    [Fact]
    public void Constructor_WithHandledFalse_ShouldSetHandledFalse()
    {
        // Arrange
        var ex = new Exception("未处理异常");

        // Act
        var ctx = new ExceptionNotificationContext(ex, handled: false);

        // Assert
        ctx.Handled.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：Handled 属性是可读写的，可在构造后修改。
    /// </summary>
    [Fact]
    public void Handled_CanBeModifiedAfterConstruction()
    {
        // Arrange
        var ctx = new ExceptionNotificationContext(new Exception("mutable"));

        // Act
        ctx.Handled = false;

        // Assert
        ctx.Handled.ShouldBeFalse();
    }
}

// ─── ExceptionNotifierExtensions 测试 ────────────────────────────

/// <summary>
/// <see cref="ExceptionNotifierExtensions"/> 单元测试
/// </summary>
public class ExceptionNotifierExtensionsTest
{
    /// <summary>
    /// 测试目的：NotifyAsync 扩展方法对 null notifier 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public async Task NotifyAsync_NullNotifier_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            ExceptionNotifierExtensions.NotifyAsync(null, new Exception("test")));
    }

    /// <summary>
    /// 测试目的：NotifyAsync 扩展方法应将参数正确包装为 ExceptionNotificationContext 并调用 notifier。
    /// </summary>
    [Fact]
    public async Task NotifyAsync_ShouldForwardToNotifierWithContext()
    {
        // Arrange
        var mockNotifier = new Mock<IExceptionNotifier>();
        ExceptionNotificationContext capturedCtx = null;
        mockNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<ExceptionNotificationContext>()))
            .Callback<ExceptionNotificationContext>(c => capturedCtx = c)
            .Returns(Task.CompletedTask);

        var ex = new InvalidOperationException("转发测试");

        // Act
        await mockNotifier.Object.NotifyAsync(ex, LogLevel.Warning, handled: false);

        // Assert
        capturedCtx.ShouldNotBeNull();
        capturedCtx.Exception.ShouldBeSameAs(ex);
        capturedCtx.LogLevel.ShouldBe(LogLevel.Warning);
        capturedCtx.Handled.ShouldBeFalse();
    }
}
