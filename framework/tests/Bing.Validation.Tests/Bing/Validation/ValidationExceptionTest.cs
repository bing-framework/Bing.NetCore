using Bing.Exceptions;
using Bing.Validation;
using Shouldly;
using Xunit;

namespace Bing.Validation.Tests;

/// <summary>
/// <see cref="ValidationException"/> 单元测试
/// </summary>
public class ValidationExceptionTest
{
    // ── Default constructor ────────────────────────────────────────

    /// <summary>
    /// 测试目的：默认构造后，Flag 应为 "__VALID_FLG"，标识其为验证类异常。
    /// </summary>
    [Fact]
    public void DefaultConstructor_ShouldHaveValidFlag()
    {
        // Act
        var ex = new ValidationException();

        // Assert
        ex.Flag.ShouldBe("__VALID_FLG");
    }

    // ── Constructor(string message) ────────────────────────────────

    /// <summary>
    /// 测试目的：通过消息字符串构造时，Message 应正确赋值，Flag 仍为 "__VALID_FLG"。
    /// </summary>
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessageAndFlag()
    {
        // Act
        var ex = new ValidationException("用户名不能为空");

        // Assert
        ex.Message.ShouldBe("用户名不能为空");
        ex.Flag.ShouldBe("__VALID_FLG");
    }

    // ── Constructor(string message, string flag) ───────────────────

    /// <summary>
    /// 测试目的：通过消息与自定义 flag 构造时，应正确覆盖默认 Flag 值。
    /// </summary>
    [Fact]
    public void Constructor_WithMessageAndFlag_ShouldSetCustomFlag()
    {
        // Act
        var ex = new ValidationException("验证失败", "MY_CUSTOM_FLG");

        // Assert
        ex.Message.ShouldBe("验证失败");
        ex.Flag.ShouldBe("MY_CUSTOM_FLG");
    }

    // ── Constructor(string message, Exception innerException) ──────

    /// <summary>
    /// 测试目的：带内部异常构造时，InnerException 应被正确链接，Flag 仍为默认值。
    /// </summary>
    [Fact]
    public void Constructor_WithInnerException_ShouldLinkInnerExceptionAndFlag()
    {
        // Arrange
        var inner = new InvalidOperationException("原始错误");

        // Act
        var ex = new ValidationException("包装错误", inner);

        // Assert
        ex.Message.ShouldBe("包装错误");
        ex.InnerException.ShouldBe(inner);
        ex.Flag.ShouldBe("__VALID_FLG");
    }

    // ── Constructor(IEnumerable<string>) ──────────────────────────

    /// <summary>
    /// 测试目的：传入 null 的验证消息集合应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_WithNullMessages_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ValidationException((IEnumerable<string>)null));
    }

    /// <summary>
    /// 测试目的：通过有效验证消息集合构造时不应抛出异常，
    /// 且 ValidationMessage 属性应包含所有传入消息。
    /// </summary>
    [Fact]
    public void Constructor_WithValidMessages_ShouldSetValidationMessage()
    {
        // Arrange
        var messages = new[] { "字段A错误", "字段B错误" };

        // Act
        var ex = new ValidationException(messages);

        // Assert
        ex.ValidationMessage.ShouldNotBeNull();
        ex.ValidationMessage.ShouldContain("字段A错误");
        ex.ValidationMessage.ShouldContain("字段B错误");
    }

    // ── Constructor(long errorCode, string message) ────────────────

    /// <summary>
    /// 测试目的：通过错误码和消息构造时，Code 和 Message 均应被正确设置。
    /// </summary>
    [Fact]
    public void Constructor_WithErrorCodeAndMessage_ShouldSetCodeAndMessage()
    {
        // Act
        var ex = new ValidationException(4001L, "自定义验证错误码");

        // Assert
        ex.Code.ShouldBe("4001");
        ex.Message.ShouldBe("自定义验证错误码");
        ex.Flag.ShouldBe("__VALID_FLG");
    }

    // ── ToString / GetFullMessage ──────────────────────────────────

    /// <summary>
    /// 测试目的：带验证消息集合时，ToString 应包含各条消息内容（非空输出）。
    /// </summary>
    [Fact]
    public void ToString_WithValidationMessages_ShouldContainMessages()
    {
        // Arrange
        var ex = new ValidationException(new[] { "字段X不合法", "字段Y超出范围" });

        // Act
        var str = ex.ToString();

        // Assert
        str.ShouldContain("字段X不合法");
        str.ShouldContain("字段Y超出范围");
    }

    // ── Is-a hierarchy ────────────────────────────────────────────

    /// <summary>
    /// 测试目的：ValidationException 应继承自 BingException，满足框架异常类型约定。
    /// </summary>
    [Fact]
    public void ValidationException_ShouldInheritFromBingException()
    {
        // Arrange
        var ex = new ValidationException("test");

        // Assert
        ex.ShouldBeAssignableTo<BingException>();
    }
}
