namespace Bing.Aop.AspectCore.Samples;

/// <summary>
/// 扩展测试服务实现
/// </summary>
public class TestService2 : ITestService2
{
    /// <summary>
    /// 获取 object，值不能为 null
    /// </summary>
    public object GetNotNullObject(object value) => value;

    /// <summary>
    /// 获取值，两个参数均不能为空
    /// </summary>
    public string GetBothNotEmpty(string a, string b) => $"{a},{b}";
}
