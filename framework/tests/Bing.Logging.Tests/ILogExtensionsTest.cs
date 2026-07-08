using Microsoft.Extensions.Logging;
using Bing.Logging.Core;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Logging.Tests;

/// <summary>
/// <see cref="ILogExtensions"/> 扩展方法单元测试
/// </summary>
public class ILogExtensionsTest
{
    /// <summary>
    /// 构建一个模拟 ILog，通过验证 Message 调用来断言扩展方法行为
    /// </summary>
    private static (Mock<ILog> mock, ILog log) BuildMockLog()
    {
        var mock = new Mock<ILog>();
        // 支持链式调用：方法返回 mock 自身
        mock.Setup(x => x.Message(It.IsAny<string>(), It.IsAny<object[]>())).Returns(mock.Object);
        mock.Setup(x => x.Set(It.IsAny<Action<LogEventDescriptor>>())).Returns((Action<LogEventDescriptor> a) =>
        {
            a(new LogEventDescriptor());
            return mock.Object;
        });
        return (mock, mock.Object);
    }

    // ═══════════════════════════════════════════════════════════
    // Append
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Append() 在 log 为 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Append_WhenLogIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        ILog nullLog = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => nullLog.Append("msg"));
    }

    /// <summary>
    /// 测试目的：Append() 应调用 Message() 一次，并返回 log 自身（支持链式调用）。
    /// </summary>
    [Fact]
    public void Append_WithMessage_ShouldCallMessageOnce_AndReturnLog()
    {
        // Arrange
        var (mock, log) = BuildMockLog();

        // Act
        var returned = log.Append("hello {0}", "world");

        // Assert
        mock.Verify(x => x.Message("hello {0}", "world"), Times.Once);
        returned.ShouldBeSameAs(log);
    }

    // ═══════════════════════════════════════════════════════════
    // AppendIf
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AppendIf() 在 log 为 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void AppendIf_WhenLogIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        ILog nullLog = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => nullLog.AppendIf("msg", true));
    }

    /// <summary>
    /// 测试目的：AppendIf() 当条件为 true 时，应调用 Message() 一次。
    /// </summary>
    [Fact]
    public void AppendIf_WhenConditionIsTrue_ShouldCallMessage()
    {
        // Arrange
        var (mock, log) = BuildMockLog();

        // Act
        log.AppendIf("conditional msg", true);

        // Assert
        mock.Verify(x => x.Message("conditional msg"), Times.Once);
    }

    /// <summary>
    /// 测试目的：AppendIf() 当条件为 false 时，不应调用 Message()。
    /// </summary>
    [Fact]
    public void AppendIf_WhenConditionIsFalse_ShouldNotCallMessage()
    {
        // Arrange
        var (mock, log) = BuildMockLog();

        // Act
        log.AppendIf("should not appear", false);

        // Assert
        mock.Verify(x => x.Message(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
    }

    // ═══════════════════════════════════════════════════════════
    // AppendLine
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AppendLine() 在 log 为 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void AppendLine_WhenLogIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        ILog nullLog = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => nullLog.AppendLine("msg"));
    }

    /// <summary>
    /// 测试目的：AppendLine() 应调用 Message() 两次：一次是消息内容，一次是换行符。
    /// </summary>
    [Fact]
    public void AppendLine_WithMessage_ShouldCallMessageTwice()
    {
        // Arrange
        var (mock, log) = BuildMockLog();

        // Act
        log.AppendLine("line content");

        // Assert
        mock.Verify(x => x.Message("line content"), Times.Once);
        mock.Verify(x => x.Message(Environment.NewLine), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // AppendLineIf
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AppendLineIf() 当条件为 true 时，应调用两次 Message()（内容 + 换行）。
    /// </summary>
    [Fact]
    public void AppendLineIf_WhenConditionIsTrue_ShouldCallMessageTwice()
    {
        // Arrange
        var (mock, log) = BuildMockLog();

        // Act
        log.AppendLineIf("conditional line", true);

        // Assert
        mock.Verify(x => x.Message("conditional line"), Times.Once);
        mock.Verify(x => x.Message(Environment.NewLine), Times.Once);
    }

    /// <summary>
    /// 测试目的：AppendLineIf() 当条件为 false 时，不应调用 Message()。
    /// </summary>
    [Fact]
    public void AppendLineIf_WhenConditionIsFalse_ShouldNotCallMessage()
    {
        // Arrange
        var (mock, log) = BuildMockLog();

        // Act
        log.AppendLineIf("should not appear", false);

        // Assert
        mock.Verify(x => x.Message(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
    }

    // ═══════════════════════════════════════════════════════════
    // Line
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Line() 在 log 为 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Line_WhenLogIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        ILog nullLog = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => nullLog.Line());
    }

    /// <summary>
    /// 测试目的：Line() 应调用 Message(Environment.NewLine) 一次，并返回 log 自身。
    /// </summary>
    [Fact]
    public void Line_ShouldCallMessage_WithNewLine()
    {
        // Arrange
        var (mock, log) = BuildMockLog();

        // Act
        var returned = log.Line();

        // Assert
        mock.Verify(x => x.Message(Environment.NewLine), Times.Once);
        returned.ShouldBeSameAs(log);
    }

    // ═══════════════════════════════════════════════════════════
    // ExtraProperty / ExtraPropertyIf
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ExtraProperty() 应调用 Set() 委托（最终设置 Context 扩展属性），并返回 log 自身。
    /// </summary>
    [Fact]
    public void ExtraProperty_ShouldCallSet_AndReturnLog()
    {
        // Arrange
        var (mock, log) = BuildMockLog();
        var setCalled = false;
        mock.Setup(x => x.Set(It.IsAny<Action<LogEventDescriptor>>())).Returns((Action<LogEventDescriptor> a) =>
        {
            setCalled = true;
            return mock.Object;
        });

        // Act
        var returned = log.ExtraProperty("RequestId", "REQ-001");

        // Assert
        setCalled.ShouldBeTrue();
        returned.ShouldBeSameAs(log);
    }

    /// <summary>
    /// 测试目的：ExtraPropertyIf() 当条件为 true 时，应调用 Set()。
    /// </summary>
    [Fact]
    public void ExtraPropertyIf_WhenConditionIsTrue_ShouldCallSet()
    {
        // Arrange
        var (mock, log) = BuildMockLog();
        var setCalled = false;
        mock.Setup(x => x.Set(It.IsAny<Action<LogEventDescriptor>>())).Returns((Action<LogEventDescriptor> a) =>
        {
            setCalled = true;
            return mock.Object;
        });

        // Act
        log.ExtraPropertyIf("key", "value", true);

        // Assert
        setCalled.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：ExtraPropertyIf() 当条件为 false 时，不应调用 Set()，直接返回 log。
    /// </summary>
    [Fact]
    public void ExtraPropertyIf_WhenConditionIsFalse_ShouldNotCallSet()
    {
        // Arrange
        var (mock, log) = BuildMockLog();

        // Act
        var returned = log.ExtraPropertyIf("key", "value", false);

        // Assert
        mock.Verify(x => x.Set(It.IsAny<Action<LogEventDescriptor>>()), Times.Never);
        returned.ShouldBeSameAs(log);
    }

    // ═══════════════════════════════════════════════════════════
    // Tags / Tag / TagsIf / TagIf
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Tags() 应调用 Set()，并返回 log 自身。
    /// </summary>
    [Fact]
    public void Tags_ShouldCallSet_AndReturnLog()
    {
        // Arrange
        var (mock, log) = BuildMockLog();
        var setCalled = false;
        mock.Setup(x => x.Set(It.IsAny<Action<LogEventDescriptor>>())).Returns((Action<LogEventDescriptor> a) =>
        {
            setCalled = true;
            return mock.Object;
        });

        // Act
        var returned = log.Tags("tag1", "tag2");

        // Assert
        setCalled.ShouldBeTrue();
        returned.ShouldBeSameAs(log);
    }

    /// <summary>
    /// 测试目的：TagsIf() 当条件为 true 时，应调用 Set()。
    /// </summary>
    [Fact]
    public void TagsIf_WhenConditionIsTrue_ShouldCallSet()
    {
        // Arrange
        var (mock, log) = BuildMockLog();
        var setCalled = false;
        mock.Setup(x => x.Set(It.IsAny<Action<LogEventDescriptor>>())).Returns((Action<LogEventDescriptor> a) =>
        {
            setCalled = true;
            return mock.Object;
        });

        // Act
        log.TagsIf(true, "t1");

        // Assert
        setCalled.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：TagsIf() 当条件为 false 时，不应调用 Set()，直接返回 log。
    /// </summary>
    [Fact]
    public void TagsIf_WhenConditionIsFalse_ShouldNotCallSet()
    {
        // Arrange
        var (mock, log) = BuildMockLog();

        // Act
        var returned = log.TagsIf(false, "t1");

        // Assert
        mock.Verify(x => x.Set(It.IsAny<Action<LogEventDescriptor>>()), Times.Never);
        returned.ShouldBeSameAs(log);
    }

    /// <summary>
    /// 测试目的：Tag() 应调用 Set()，行为与 Tags() 单参数版本一致。
    /// </summary>
    [Fact]
    public void Tag_ShouldCallSet()
    {
        // Arrange
        var (mock, log) = BuildMockLog();
        var setCalled = false;
        mock.Setup(x => x.Set(It.IsAny<Action<LogEventDescriptor>>())).Returns((Action<LogEventDescriptor> a) =>
        {
            setCalled = true;
            return mock.Object;
        });

        // Act
        log.Tag("single-tag");

        // Assert
        setCalled.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：TagIf() 当条件为 false 时，不应调用 Set()，直接返回 log。
    /// </summary>
    [Fact]
    public void TagIf_WhenConditionIsFalse_ShouldNotCallSet()
    {
        // Arrange
        var (mock, log) = BuildMockLog();

        // Act
        var returned = log.TagIf("t", false);

        // Assert
        mock.Verify(x => x.Set(It.IsAny<Action<LogEventDescriptor>>()), Times.Never);
        returned.ShouldBeSameAs(log);
    }

    /// <summary>
    /// 测试目的：TagIf() 当条件为 true 时，应调用 Set()。
    /// </summary>
    [Fact]
    public void TagIf_WhenConditionIsTrue_ShouldCallSet()
    {
        // Arrange
        var (mock, log) = BuildMockLog();
        var setCalled = false;
        mock.Setup(x => x.Set(It.IsAny<Action<LogEventDescriptor>>())).Returns((Action<LogEventDescriptor> a) =>
        {
            setCalled = true;
            return mock.Object;
        });

        // Act
        log.TagIf("t", true);

        // Assert
        setCalled.ShouldBeTrue();
    }
}
