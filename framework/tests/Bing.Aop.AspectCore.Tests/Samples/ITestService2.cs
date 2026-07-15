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
    object GetNotNullObject([NotNull] object value);

    /// <summary>
    /// 获取值，两个参数均不能为空
    /// </summary>
    /// <param name="a">参数A</param>
    /// <param name="b">参数B</param>
    string GetBothNotEmpty([NotEmpty] string a, [NotEmpty] string b);
}
