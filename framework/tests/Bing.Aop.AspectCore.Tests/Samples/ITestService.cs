using Bing.Aspects;
using Bing.DependencyInjection;

namespace Bing.Aop.AspectCore.Samples;

/// <summary>
/// 测试服务
/// </summary>
public interface ITestService : ISingletonDependency
{
    /// <summary>
    /// 获取值，值不能为空
    /// </summary>
    /// <param name="value">参数</param>
    /// <returns>通过非空校验的字符串。</returns>
    string GetNotEmptyValue([NotEmpty] string value);

    /// <summary>
    /// 获取值，值不能为null
    /// </summary>
    /// <param name="value">参数</param>
    /// <returns>通过非空值校验的字符串。</returns>
    string GetNotNullValue([NotNull] string value);
}
