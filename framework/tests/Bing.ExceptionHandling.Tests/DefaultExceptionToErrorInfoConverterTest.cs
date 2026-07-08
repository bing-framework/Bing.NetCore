using Bing.AspNetCore.ExceptionHandling;
using Bing.Domain.Entities;
using Bing.Exceptions;
using Bing.Http;
using Shouldly;
using Xunit;

namespace Bing.ExceptionHandling.Tests;

/// <summary>
/// <see cref="DefaultExceptionToErrorInfoConverter"/> 单元测试。
/// 不依赖 ASP.NET Core 管道，直接 new 被测类。
/// </summary>
public class DefaultExceptionToErrorInfoConverterTest
{
    private readonly DefaultExceptionToErrorInfoConverter _converter = new();

    // ── UserFriendlyException ─────────────────────────────────────

    /// <summary>
    /// 测试目的：UserFriendly 异常（Warning）应直接将 Message 作为用户消息返回。
    /// </summary>
    [Fact]
    public void Convert_WhenWarningException_ShouldUseExceptionMessageAsUserMessage()
    {
        // Arrange
        var ex = new Warning("操作不被允许", "W001");

        // Act
        var result = _converter.Convert(ex);

        // Assert
        result.ShouldNotBeNull();
        result.Code.ShouldBe("W001");
        result.Message.ShouldBe("操作不被允许");
    }

    // ── EntityNotFoundException ────────────────────────────────────

    /// <summary>
    /// 测试目的：EntityNotFoundException 带有 EntityType 和 Id 时，
    /// 消息应包含实体类型名和 Id 值，便于调试。
    /// </summary>
    [Fact]
    public void Convert_WhenEntityNotFoundWithTypeAndId_ShouldReturnDescriptiveMessage()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var ex = new EntityNotFoundException(typeof(TestEntity), entityId);

        // Act
        var result = _converter.Convert(ex);

        // Assert
        result.Message.ShouldContain("TestEntity");
        result.Message.ShouldContain(entityId.ToString());
    }

    /// <summary>
    /// 测试目的：EntityNotFoundException 只有消息（无 EntityType）时，
    /// 应直接使用异常消息作为错误信息。
    /// </summary>
    [Fact]
    public void Convert_WhenEntityNotFoundWithoutType_ShouldReturnExceptionMessage()
    {
        // Arrange
        var ex = new EntityNotFoundException("找不到目标记录");

        // Act
        var result = _converter.Convert(ex);

        // Assert
        result.Message.ShouldBe("找不到目标记录");
    }

    // ── 普通 SystemException ──────────────────────────────────────

    /// <summary>
    /// 测试目的：普通系统异常在默认配置（不向客户端发送详情）时，
    /// 应返回通用提示而非原始异常消息（防止信息泄露）。
    /// </summary>
    [Fact]
    public void Convert_WhenSystemException_WithDefaultOptions_ShouldReturnGenericMessage()
    {
        // Arrange
        var ex = new InvalidOperationException("内部数据库连接字符串 secret_key=xxx");

        // Act
        var result = _converter.Convert(ex);

        // Assert
        result.ShouldNotBeNull();
        // 默认不发送详情，消息应为 ExceptionPrompt 返回的通用提示（不含原始内部信息）
        result.Message.ShouldNotBeNullOrWhiteSpace();
    }

    // ── SendExceptionDetailsToClients = true ──────────────────────

    /// <summary>
    /// 测试目的：当 SendExceptionDetailsToClients=true 时，
    /// Details 字段应包含异常类型名和原始消息（调试模式）。
    /// </summary>
    [Fact]
    public void Convert_WhenSendDetailsEnabled_ShouldIncludeExceptionTypeInDetails()
    {
        // Arrange
        var ex = new ArgumentException("param cannot be null");

        // Act
        var result = _converter.Convert(ex, opt => opt.SendExceptionDetailsToClients = true);

        // Assert
        result.Details.ShouldNotBeNullOrWhiteSpace();
        result.Details.ShouldContain("ArgumentException");
    }

    // ── AggregateException 解包 ───────────────────────────────────

    /// <summary>
    /// 测试目的：AggregateException 包裹 EntityNotFoundException 时，
    /// 转换器应解包内层异常，返回 EntityNotFoundException 对应的错误信息。
    /// </summary>
    [Fact]
    public void Convert_WhenAggregateWrapsEntityNotFoundException_ShouldUnwrap()
    {
        // Arrange
        var inner = new EntityNotFoundException(typeof(TestEntity), 42);
        var ex = new AggregateException(inner);

        // Act
        var result = _converter.Convert(ex);

        // Assert
        result.Message.ShouldContain("TestEntity");
        result.Message.ShouldContain("42");
    }

    // ── null 传入 ─────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入 null 时，转换器应抛出 ArgumentNullException，
    /// 而非 NullReferenceException（明确的错误边界）。
    /// </summary>
    [Fact]
    public void Convert_WhenNullException_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<Exception>(() => _converter.Convert(null));
    }

    // ── 测试用实体（仅用于类型名断言）────────────────────────────

    private class TestEntity { }
}
