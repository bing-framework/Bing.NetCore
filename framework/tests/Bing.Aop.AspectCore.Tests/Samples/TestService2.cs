namespace Bing.Aop.AspectCore.Samples;

/// <summary>
/// 扩展测试服务实现
/// </summary>
public class TestService2 : ITestService2
{
    /// <inheritdoc />
    public object GetNotNullObject(object value) => value;

    /// <inheritdoc />
    public string GetBothNotEmpty(string a, string b) => $"{a},{b}";
}
