using Bing.Exceptions.Prompts;
using Shouldly;
using Xunit;

namespace Bing.Aop.AspectCore;

/// <summary>
/// <see cref="AspectExceptionPrompt"/> 单元测试
/// </summary>
public class AspectExceptionPromptTest
{
    private readonly AspectExceptionPrompt _prompt = new();

    /// <summary>
    /// 测试目的：GetPrompt(null) 应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void GetPrompt_NullException_ShouldReturnNull()
    {
        // Act
        var result = _prompt.GetPrompt(null);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：GetPrompt 传入普通异常（非 AspectInvocationException）应返回空字符串。
    /// </summary>
    [Fact]
    public void GetPrompt_RegularException_ShouldReturnEmpty()
    {
        // Arrange
        var ex = new InvalidOperationException("regular error");

        // Act
        var result = _prompt.GetPrompt(ex);

        // Assert
        result.ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：GetRawException 传入普通异常应原样返回，不做任何转换。
    /// </summary>
    [Fact]
    public void GetRawException_RegularException_ShouldReturnSameException()
    {
        // Arrange
        var ex = new ArgumentException("param error");

        // Act
        var result = _prompt.GetRawException(ex);

        // Assert
        ReferenceEquals(result, ex).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：GetRawException 传入嵌套普通异常（非 AspectInvocationException）应返回最外层异常本身。
    /// </summary>
    [Fact]
    public void GetRawException_NestedRegularException_ShouldReturnOuterException()
    {
        // Arrange
        var inner = new Exception("inner");
        var outer = new InvalidOperationException("outer", inner);

        // Act
        var result = _prompt.GetRawException(outer);

        // Assert
        ReferenceEquals(result, outer).ShouldBeTrue();
    }
}
