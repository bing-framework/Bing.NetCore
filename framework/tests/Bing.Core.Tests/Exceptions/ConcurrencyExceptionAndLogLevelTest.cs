using Bing.Exceptions;
using Bing.Exceptions.Prompts;
using Bing.Logging;
using Microsoft.Extensions.Logging;

namespace Bing.Tests.Exceptions;

/// <summary>
/// ConcurrencyException、HasLogLevelExtensions、ExceptionPrompt 单元测试
/// </summary>
public class ConcurrencyExceptionAndLogLevelTest
{
    // ════════════════════════════════════════════════════════════════
    // ConcurrencyException
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造不应抛异常，且 Message 包含并发异常提示前缀。
    /// </summary>
    [Fact]
    public void ConcurrencyException_DefaultCtor_MessageShouldNotBeEmpty()
    {
        // Act
        var ex = new ConcurrencyException();

        // Assert
        ex.Message.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 测试目的：传入 message 时，Message 属性应包含并发提示文本。
    /// </summary>
    [Fact]
    public void ConcurrencyException_WithMessage_MessageShouldContainPrefix()
    {
        // Act
        var ex = new ConcurrencyException("版本冲突");

        // Assert
        // Message 由 LibraryResource.ConcurrencyExceptionMessage + 传入 message 拼接
        ex.Message.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 测试目的：传入 Exception 包装时，InnerException 应与传入一致。
    /// </summary>
    [Fact]
    public void ConcurrencyException_WithInnerException_ShouldExposeInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("db conflict");

        // Act
        var ex = new ConcurrencyException(inner);

        // Assert
        ex.InnerException.ShouldBeSameAs(inner);
    }

    /// <summary>
    /// 测试目的：传入 message + exception 时，两者均应被保存。
    /// </summary>
    [Fact]
    public void ConcurrencyException_WithMessageAndException_ShouldStoreAll()
    {
        // Arrange
        var inner = new Exception("cause");

        // Act
        var ex = new ConcurrencyException("conflict detail", inner);

        // Assert
        ex.InnerException.ShouldBeSameAs(inner);
        ex.Message.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 测试目的：GetMessage(isProduction=true) 应返回通用提示（不暴露详细信息）。
    /// </summary>
    [Fact]
    public void ConcurrencyException_GetMessage_Production_ShouldReturnGenericMessage()
    {
        // Act
        var ex = new ConcurrencyException("secret internal detail");
        var msg = ex.GetMessage(isProduction: true);

        // Assert
        msg.ShouldNotBeNullOrWhiteSpace();
        // 生产模式不应包含原始 message 中的内部细节（防信息泄露）
        msg.ShouldNotContain("secret internal detail");
    }

    /// <summary>
    /// 测试目的：GetMessage(isProduction=false) 应包含更详细的信息（调试模式）。
    /// </summary>
    [Fact]
    public void ConcurrencyException_GetMessage_NonProduction_ShouldReturnDetailedMessage()
    {
        // Act
        var ex = new ConcurrencyException("conflict detail");
        var msg = ex.GetMessage(isProduction: false);

        // Assert
        msg.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 测试目的：ConcurrencyException 继承自 Warning，应能被 Warning 类型捕获。
    /// </summary>
    [Fact]
    public void ConcurrencyException_IsAssignableTo_Warning()
    {
        // Assert
        new ConcurrencyException().ShouldBeAssignableTo<Warning>();
    }

    // ════════════════════════════════════════════════════════════════
    // HasLogLevelExtensions.WithLogLevel
    // ════════════════════════════════════════════════════════════════

    // Warning 实现了 IHasLogLevel（通过 Warning 基类）
    private class LogLevelException : Exception, IHasLogLevel
    {
        public LogLevel LogLevel { get; set; } = LogLevel.Error;
    }

    /// <summary>
    /// 测试目的：WithLogLevel 应将传入的日志级别赋给异常，并返回该异常（链式调用）。
    /// </summary>
    [Fact]
    public void WithLogLevel_ShouldSetLogLevelAndReturnSameException()
    {
        // Arrange
        var ex = new LogLevelException();

        // Act
        var returned = ex.WithLogLevel(LogLevel.Warning);

        // Assert
        ex.LogLevel.ShouldBe(LogLevel.Warning);
        returned.ShouldBeSameAs(ex);
    }

    /// <summary>
    /// 测试目的：所有合法的 LogLevel 值均应能正确被 WithLogLevel 赋值。
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void WithLogLevel_AllLevels_ShouldSetCorrectly(LogLevel level)
    {
        // Arrange
        var ex = new LogLevelException();

        // Act
        ex.WithLogLevel(level);

        // Assert
        ex.LogLevel.ShouldBe(level);
    }

    /// <summary>
    /// 测试目的：传入 null 时 WithLogLevel 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void WithLogLevel_NullException_ShouldThrowArgumentNullException()
    {
        // Arrange
        LogLevelException ex = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ex.WithLogLevel(LogLevel.Error));
    }

    // ════════════════════════════════════════════════════════════════
    // ExceptionPrompt
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AddPrompt(null) 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_AddPrompt_Null_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ExceptionPrompt.AddPrompt(null));
    }

    /// <summary>
    /// 测试目的：GetPrompt(null, ...) 应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_GetPrompt_NullException_ShouldReturnNull()
    {
        // Act
        var result = ExceptionPrompt.GetPrompt(null, isProduction: true);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：普通异常在生产模式下 GetPrompt 应返回通用系统错误提示（不暴露原始消息）。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_GetPrompt_SystemException_ProductionMode_ShouldReturnSystemError()
    {
        // Arrange
        var ex = new InvalidOperationException("internal secret");

        // Act
        var result = ExceptionPrompt.GetPrompt(ex, isProduction: true);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldNotContain("internal secret");
    }

    /// <summary>
    /// 测试目的：Warning 异常在生产模式下 GetPrompt 应返回 Warning.GetMessage(true)（用户友好消息）。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_GetPrompt_Warning_ProductionMode_ShouldReturnWarningMessage()
    {
        // Arrange
        var ex = new Warning("操作被禁止");

        // Act
        var result = ExceptionPrompt.GetPrompt(ex, isProduction: true);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldBe(ex.GetMessage(isProduction: true));
    }

    /// <summary>
    /// 测试目的：GetException(null) 应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_GetException_Null_ShouldReturnNull()
    {
        // Act
        var result = ExceptionPrompt.GetException(null);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：GetException(普通异常) 在无自定义 prompt 时，应返回原始异常本身。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_GetException_NoPrompts_ShouldReturnSameException()
    {
        // Arrange
        var ex = new InvalidOperationException("raw");

        // Act
        var result = ExceptionPrompt.GetException(ex);

        // Assert
        result.ShouldNotBeNull();
    }
}
