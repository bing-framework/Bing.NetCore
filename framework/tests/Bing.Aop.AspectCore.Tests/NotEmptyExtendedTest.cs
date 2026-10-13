using Bing.Aop.AspectCore.Samples;

namespace Bing.Aop.AspectCore;

/// <summary>
/// <see cref="NotEmptyAttribute"/> 拦截器扩展边界测试
/// </summary>
public class NotEmptyExtendedTest
{
    /// <summary>
    /// 供非空验证测试使用的服务实例。
    /// </summary>
    private readonly ITestService _service;

    /// <summary>
    /// 初始化一个 <see cref="NotEmptyExtendedTest"/> 类型的实例。
    /// </summary>
    public NotEmptyExtendedTest(ITestService service)
    {
        _service = service;
    }

    // ── null 输入 ──────────────────────────────────────────────────

    /// <summary>
    /// 验证 [NotEmpty] 对 null 输入抛出 ArgumentNullException。
    /// </summary>
    /// <remarks>该结果由 <c>IsNullOrWhiteSpace(null.SafeString())</c> 的判断链产生。</remarks>
    [Fact]
    public void NotEmpty_WhenNullInput_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.GetNotEmptyValue(null));
    }

    // ── 空白字符串输入 ─────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入仅含空格的字符串时，[NotEmpty] 应抛出 ArgumentNullException （IsNullOrWhiteSpace = true）。
    /// </summary>
    [Fact]
    public void NotEmpty_WhenWhitespaceOnly_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.GetNotEmptyValue("   "));
    }

    /// <summary>
    /// 测试目的：传入制表符字符串时，[NotEmpty] 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void NotEmpty_WhenTabOnly_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.GetNotEmptyValue("\t"));
    }

    /// <summary>
    /// 测试目的：传入换行符字符串时，[NotEmpty] 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void NotEmpty_WhenNewlineOnly_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.GetNotEmptyValue("\n"));
    }

    // ── 有效值通过 ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入包含前导/尾部空格的有效字符串时（非空白）， [NotEmpty] 不应抛出，应返回原始值。
    /// </summary>
    [Fact]
    public void NotEmpty_WhenStringWithContent_ShouldReturnValue()
    {
        // Arrange & Act
        var result = _service.GetNotEmptyValue(" hello ");

        // Assert
        result.ShouldBe(" hello ");
    }
}
