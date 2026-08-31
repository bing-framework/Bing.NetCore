using AspectCore.DynamicProxy;
using AspectCore.Extensions.AspectScope;
using Bing.Aspects;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Application.Aspects;

/// <summary>
/// 提供工作单元拦截能力，在目标调用成功后提交当前工作单元。
/// </summary>
public class UnitOfWorkAttribute : InterceptorBase, IScopeInterceptor
{
    /// <summary>
    /// 获取或设置拦截器作用域；使用 <see cref="Scope.Aspect"/> 时，嵌套拦截器仅由最外层生效。
    /// </summary>
    public Scope Scope { get; set; } = Scope.Aspect;

    /// <summary>
    /// 执行目标方法，并在成功返回后提交工作单元及执行提交后回调。
    /// </summary>
    /// <param name="context">当前拦截上下文。</param>
    /// <param name="next">目标方法的后续执行委托。</param>
    public override async Task Invoke(AspectContext context, AspectDelegate next)
    {
        await next(context);
        var manager = context.ServiceProvider.GetService<IUnitOfWorkManager>();
        if (manager == null)
            return;
        await manager.CommitAsync();
        if (context.Implementation is ICommitAfter service)
            service.CommitAfter();
    }
}
