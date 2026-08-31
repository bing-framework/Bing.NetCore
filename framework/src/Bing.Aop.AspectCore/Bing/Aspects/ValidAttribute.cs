using AspectCore.DynamicProxy.Parameters;
using Bing.Aspects;

namespace Bing.Validation;

/// <summary>
/// 验证拦截器。
/// 标记在方法参数上，AOP 管道会在方法执行前自动调用 <see cref="IVerifyModel.Validate"/> 进行校验。
/// </summary>
/// <remarks>
/// 此特性依赖 AspectCore 参数拦截机制，因此定义在 Bing.Aop.AspectCore 项目中，
    /// <param name="context">参数拦截上下文。</param>
    /// <param name="next">后续拦截器或目标方法委托。</param>
/// 但保持 <c>Bing.Validation</c> 命名空间不变，消费方无需更改 using 语句。
/// </remarks>
public class ValidAttribute : ParameterInterceptorBase
{
    /// <summary>
    /// 执行参数拦截逻辑，在目标方法执行前触发参数验证。
    /// </summary>
    /// <param name="context">参数拦截上下文。</param>
    /// <param name="next">后续拦截器或目标方法委托。</param>
    public override async Task Invoke(ParameterAspectContext context, ParameterAspectDelegate next)
    {
        Validate(context.Parameter);
        await next(context);
    }

    /// <summary>
    /// 验证单个参数对象；集合参数交由 <see cref="ValidateCollection"/> 处理。
    /// </summary>
    /// <param name="parameter">待验证的参数。</param>
    private static void Validate(Parameter parameter)
    {
        if (Bing.Reflection.Reflections.IsGenericCollection(parameter.RawType))
        {
            ValidateCollection(parameter);
            return;
        }
        if (parameter.Value is IVerifyModel validation)
            validation.Validate();
    }

    /// <summary>
    /// 验证集合中的每个 <see cref="IVerifyModel"/> 元素。
    /// </summary>
    /// <param name="parameter">待验证的参数。</param>
    private static void ValidateCollection(Parameter parameter)
    {
        if (parameter.Value is not IEnumerable<IVerifyModel> validations)
            return;
        foreach (var validation in validations)
            validation.Validate();
    }
}
