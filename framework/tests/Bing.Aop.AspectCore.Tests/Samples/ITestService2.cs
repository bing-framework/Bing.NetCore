using Bing.Aspects;
using Bing.DependencyInjection;

namespace Bing.Aop.AspectCore.Samples;

/// <summary>
/// 扩展测试服务（覆盖 object 类型参数与多参数场景）
/// </summary>
public interface ITestService2 : ISingletonDependency
{
    /// <summary>
    /// 获取 object，值不能为 null
    /// </summary>
    /// <param name="value">参数</param>
    /// <returns>通过非空值校验的对象。</returns>
    object GetNotNullObject([NotNull] object value);

    /// <summary>
    /// 获取值，两个参数均不能为空
    /// </summary>
    /// <param name="a">参数A</param>
    /// <param name="b">参数B</param>
    /// <returns>通过参数校验后生成的字符串。</returns>
    string GetBothNotEmpty([NotEmpty] string a, [NotEmpty] string b);
}
