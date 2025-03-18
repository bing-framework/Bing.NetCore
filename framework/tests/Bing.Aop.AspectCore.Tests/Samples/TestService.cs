using Bing.Aspects;

namespace Bing.Aop.AspectCore.Samples;

/// <summary>
/// 测试服务
/// </summary>
public class TestService : ITestService
{
    /// <summary>
    /// 获取值，值不能为空
    /// </summary>
    /// <param name="value">参数</param>
    public string GetNotEmptyValue(string value) => value;

    /// <summary>
    /// 获取值，值不能为null
    /// </summary>
    /// <param name="value">参数</param>
    public string GetNotNullValue([NotNull] string value) => value;
}
