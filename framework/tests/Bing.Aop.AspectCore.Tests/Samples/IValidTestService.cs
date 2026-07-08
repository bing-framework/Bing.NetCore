using Bing.DependencyInjection;
using Bing.Validation;

namespace Bing.Aop.AspectCore.Samples;

/// <summary>
/// 用于验证 <see cref="ValidAttribute"/> 拦截行为的测试服务接口。
/// 参数标注 [Valid] 后，AOP 管道会在方法调用前触发参数的 IVerifyModel.Validate()。
/// </summary>
public interface IValidTestService : ISingletonDependency
{
    /// <summary>
    /// 接受任意对象参数；若参数实现 IVerifyModel，则 AOP 会自动调用 Validate()
    /// </summary>
    string ProcessObject([Valid] object input);
}
