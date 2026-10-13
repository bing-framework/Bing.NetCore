using Bing.Aspects;

namespace Bing.Aop.AspectCore.Samples;

/// <summary>
/// 测试服务
/// </summary>
public class TestService : ITestService
{
    /// <inheritdoc />
    public string GetNotEmptyValue(string value) => value;

    /// <inheritdoc />
    public string GetNotNullValue([NotNull] string value) => value;
}
