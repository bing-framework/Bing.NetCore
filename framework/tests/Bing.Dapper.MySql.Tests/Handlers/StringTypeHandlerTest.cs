using System.Data;
using Dapper.Handlers;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Handlers;

/// <summary>
/// <see cref="StringTypeHandler"/> 单元测试
/// </summary>
public class StringTypeHandlerTest
{
    private readonly StringTypeHandler _handler = new();

    // ═══════════════════════════════════════════════════════════
    // Parse
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Parse(null) 应返回 null，不抛 NullReferenceException。
    /// </summary>
    [Fact]
    public void Parse_WhenNull_ShouldReturnNull()
    {
        // Act
        var result = _handler.Parse(null!);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Parse 对普通字符串对象应返回对应的字符串值。
    /// </summary>
    [Fact]
    public void Parse_WithStringValue_ShouldReturnSameString()
    {
        // Act
        var result = _handler.Parse("hello world");

        // Assert
        result.ShouldBe("hello world");
    }

    /// <summary>
    /// 测试目的：Parse 对空字符串应返回空字符串。
    /// </summary>
    [Fact]
    public void Parse_WithEmptyString_ShouldReturnEmptyString()
    {
        // Act
        var result = _handler.Parse(string.Empty);

        // Assert
        result.ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：Parse 对非字符串对象（如整数）应调用 ToString() 后返回其字符串表示。
    /// </summary>
    [Fact]
    public void Parse_WithNonStringObject_ShouldCallToString()
    {
        // Act
        var result = _handler.Parse(42);

        // Assert
        result.ShouldBe("42");
    }

    /// <summary>
    /// 测试目的：Parse 对 Guid 对象应返回其字符串表示。
    /// </summary>
    [Fact]
    public void Parse_WithGuid_ShouldReturnGuidString()
    {
        // Arrange
        var guid = new Guid("12345678-1234-5678-1234-567812345678");

        // Act
        var result = _handler.Parse(guid);

        // Assert
        result.ShouldBe(guid.ToString());
    }

    // ═══════════════════════════════════════════════════════════
    // SetValue
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：SetValue(null, value) 应静默忽略，不抛 NullReferenceException。
    /// </summary>
    [Fact]
    public void SetValue_WhenParameterIsNull_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _handler.SetValue(null!, "test"));
    }

    /// <summary>
    /// 测试目的：SetValue 对有效 string 值应将其写入 parameter.Value。
    /// </summary>
    [Fact]
    public void SetValue_WithValidString_ShouldSetParameterValue()
    {
        // Arrange
        var mockParam = new Mock<IDbDataParameter>();
        object? assignedValue = null;
        mockParam.SetupSet(p => p.Value = It.IsAny<object>())
                 .Callback<object>(v => assignedValue = v);

        // Act
        _handler.SetValue(mockParam.Object, "my-value");

        // Assert
        assignedValue.ShouldBe("my-value");
    }

    /// <summary>
    /// 测试目的：SetValue 对 null 字符串值应将 null 写入 parameter.Value。
    /// </summary>
    [Fact]
    public void SetValue_WithNullString_ShouldSetParameterValueToNull()
    {
        // Arrange
        var mockParam = new Mock<IDbDataParameter>();
        var valueWasSet = false;
        mockParam.SetupSet(p => p.Value = null!)
                 .Callback(() => valueWasSet = true);

        // Act
        _handler.SetValue(mockParam.Object, null!);

        // Assert
        valueWasSet.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：SetValue 对空字符串应将空字符串写入 parameter.Value。
    /// </summary>
    [Fact]
    public void SetValue_WithEmptyString_ShouldSetParameterValueToEmpty()
    {
        // Arrange
        var mockParam = new Mock<IDbDataParameter>();
        object? assignedValue = "not-set";
        mockParam.SetupSet(p => p.Value = It.IsAny<object>())
                 .Callback<object>(v => assignedValue = v);

        // Act
        _handler.SetValue(mockParam.Object, string.Empty);

        // Assert
        assignedValue.ShouldBe(string.Empty);
    }
}
