using Bing.Authorization;
using Bing.Exceptions;
using Bing.Logging;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Authorization;

/// <summary>
/// <see cref="BingAuthorizationException"/> 单元测试
/// </summary>
public class BingAuthorizationExceptionTest
{
    // ═══════════════════════════════════════════════════════════
    // 继承结构
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：BingAuthorizationException 应继承自 Warning，可被统一的 Warning 处理链捕获。
    /// </summary>
    [Fact]
    public void BingAuthorizationException_ShouldInheritFromWarning()
    {
        // Arrange & Act
        var ex = new BingAuthorizationException("权限不足");

        // Assert
        ex.ShouldBeAssignableTo<Warning>();
    }

    /// <summary>
    /// 测试目的：BingAuthorizationException 应实现 IHasLogLevel，允许调用者设置日志级别。
    /// </summary>
    [Fact]
    public void BingAuthorizationException_ShouldImplementIHasLogLevel()
    {
        // Arrange & Act
        var ex = new BingAuthorizationException("权限不足");

        // Assert
        ex.ShouldBeAssignableTo<IHasLogLevel>();
    }

    // ═══════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：使用 message 构造时，Message 应等于传入的错误描述文本。
    /// </summary>
    [Fact]
    public void Ctor_WithMessage_ShouldSetMessage()
    {
        // Arrange & Act
        var ex = new BingAuthorizationException("无权访问该资源");

        // Assert
        ex.Message.ShouldBe("无权访问该资源");
    }

    /// <summary>
    /// 测试目的：使用 Exception 构造时，InnerException 应正确保存内部异常。
    /// </summary>
    [Fact]
    public void Ctor_WithException_ShouldSetInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("底层错误");

        // Act
        var ex = new BingAuthorizationException(inner);

        // Assert
        ex.InnerException.ShouldBe(inner);
    }

    /// <summary>
    /// 测试目的：使用 message + code 构造时，Code 应被正确设置，用于错误分类与客户端解析。
    /// </summary>
    [Fact]
    public void Ctor_WithMessageAndCode_ShouldSetCode()
    {
        // Arrange & Act
        var ex = new BingAuthorizationException("权限不足", "AUTH_403");

        // Assert
        ex.Message.ShouldBe("权限不足");
        ex.Code.ShouldBe("AUTH_403");
    }

    /// <summary>
    /// 测试目的：使用 message + code + exception 构造时，三个字段均应被正确设置。
    /// </summary>
    [Fact]
    public void Ctor_WithMessageCodeAndException_ShouldSetAllFields()
    {
        // Arrange
        var inner = new UnauthorizedAccessException("底层 401");

        // Act
        var ex = new BingAuthorizationException("授权失败", "AUTH_401", inner);

        // Assert
        ex.Message.ShouldBe("授权失败");
        ex.Code.ShouldBe("AUTH_401");
        ex.InnerException.ShouldBe(inner);
    }

    // ═══════════════════════════════════════════════════════════
    // LogLevel
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：LogLevel 默认值应为 Warning，表示授权失败是预期的业务拒绝，非系统错误。
    /// </summary>
    [Fact]
    public void LogLevel_Default_ShouldBeWarning()
    {
        // Arrange & Act
        var ex = new BingAuthorizationException("权限不足");

        // Assert
        ex.LogLevel.ShouldBe(LogLevel.Warning);
    }

    /// <summary>
    /// 测试目的：LogLevel 可被调用方覆盖，支持按场景调整日志级别。
    /// </summary>
    [Fact]
    public void LogLevel_WhenChanged_ShouldReflectNewValue()
    {
        // Arrange
        var ex = new BingAuthorizationException("权限不足");

        // Act
        ex.LogLevel = LogLevel.Error;

        // Assert
        ex.LogLevel.ShouldBe(LogLevel.Error);
    }

    /// <summary>
    /// 测试目的：通过 IHasLogLevel 接口读取 LogLevel，应与直接属性访问结果一致。
    /// </summary>
    [Fact]
    public void LogLevel_ViaInterface_ShouldMatchDirectAccess()
    {
        // Arrange
        var ex = new BingAuthorizationException("权限不足");
        var hasLogLevel = (IHasLogLevel)ex;

        // Assert
        hasLogLevel.LogLevel.ShouldBe(ex.LogLevel);
        hasLogLevel.LogLevel.ShouldBe(LogLevel.Warning);
    }
}
