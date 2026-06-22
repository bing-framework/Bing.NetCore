using Bing.Aop.AspectCore.Samples;

namespace Bing.Aop.AspectCore;

/// <summary>
/// <see cref="NotNullAttribute"/> 拦截器 object 类型与多参数扩展测试
/// </summary>
public class NotNullObjectTest
{
    private readonly ITestService2 _service2;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public NotNullObjectTest(ITestService2 service2)
    {
        _service2 = service2;
    }

    // ── object 类型参数 ────────────────────────────────────────────

    /// <summary>
    /// 测试目的：向 object 类型参数传入 null 时，[NotNull] 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void NotNull_WhenObjectParamIsNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service2.GetNotNullObject(null));
    }

    /// <summary>
    /// 测试目的：向 object 类型参数传入非 null 值时，应返回该值，不抛异常。
    /// </summary>
    [Fact]
    public void NotNull_WhenObjectParamHasValue_ShouldReturnValue()
    {
        // Arrange
        var obj = new { Name = "测试对象" };

        // Act
        var result = _service2.GetNotNullObject(obj);

        // Assert
        result.ShouldBe(obj);
    }

    /// <summary>
    /// 测试目的：向 object 类型参数传入整数装箱值时，应正常通过不抛异常。
    /// </summary>
    [Fact]
    public void NotNull_WhenObjectParamIsBoxedInt_ShouldReturnValue()
    {
        // Arrange & Act
        var result = _service2.GetNotNullObject(42);

        // Assert
        result.ShouldBe(42);
    }

    // ── 多参数拦截 ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：第一个参数为空时，多参数方法应抛出 ArgumentNullException
    /// （参数 A 先于参数 B 被拦截）。
    /// </summary>
    [Fact]
    public void NotEmpty_WhenFirstParamEmpty_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service2.GetBothNotEmpty("", "valid"));
    }

    /// <summary>
    /// 测试目的：第二个参数为空时，多参数方法应抛出 ArgumentNullException
    /// （参数 B 的拦截在 A 通过后触发）。
    /// </summary>
    [Fact]
    public void NotEmpty_WhenSecondParamEmpty_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service2.GetBothNotEmpty("valid", ""));
    }

    /// <summary>
    /// 测试目的：两个参数均有效时，方法应正常返回拼接结果，不抛异常。
    /// </summary>
    [Fact]
    public void NotEmpty_WhenBothParamsValid_ShouldReturnConcatenatedResult()
    {
        // Act
        var result = _service2.GetBothNotEmpty("Hello", "World");

        // Assert
        result.ShouldBe("Hello,World");
    }
}
