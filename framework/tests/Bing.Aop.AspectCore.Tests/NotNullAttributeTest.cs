using Bing.Aop.AspectCore.Samples;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 测试NotNullAttribute拦截器
/// </summary>
public class NotNullAttributeTest
{
    /// <summary>
    /// 测试服务
    /// </summary>
    private readonly ITestService _service;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public NotNullAttributeTest(ITestService service)
    {
        _service = service;
    }

    /// <summary>
    /// 测试 - 传入null - 抛出异常
    /// </summary>
    [Fact]
    public void NotNull_1()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _service.GetNotNullValue(null);
        });
    }

    /// <summary>
    /// 测试 - 传入空字符串 - 返回空字符串
    /// </summary>
    [Fact]
    public void NotNull_2()
    {
        Assert.Equal("", _service.GetNotNullValue(""));
    }

    /// <summary>
    /// 测试 - 传入字符串 - 返回字符串
    /// </summary>
    [Fact]
    public void NotNull_3()
    {
        Assert.Equal("a", _service.GetNotNullValue("a"));
    }
}
