using Bing.Aop.AspectCore.Samples;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 测试NotEmptyAttribute拦截器
/// </summary>
public class NotEmptyAttributeTest
{
    /// <summary>
    /// 测试服务
    /// </summary>
    private readonly ITestService _service;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public NotEmptyAttributeTest(ITestService service)
    {
        _service = service;
    }

    /// <summary>
    /// 测试 - 传入空字符串 - 抛出异常
    /// </summary>
    [Fact]
    public void NotEmpty_1()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _service.GetNotEmptyValue("");
        });
    }

    /// <summary>
    /// 测试 - 传入字符串 - 返回字符串
    /// </summary>
    [Fact]
    public void NotEmpty_2()
    {
        Assert.Equal("a", _service.GetNotEmptyValue("a"));
    }
}
