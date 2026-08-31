using Bing.DependencyInjection;

namespace Bing.ExceptionHandling;

/// <summary>
/// 定义可处理异常通知的可多重注册订阅器。
/// </summary>
[MultipleDependency]
public interface IExceptionSubscriber : ITransientDependency
{
    /// <summary>
    /// 获取订阅器的处理顺序。
    /// </summary>
    /// <remarks>值越小越先执行。</remarks>
    int Order { get; }

    /// <summary>
    /// 异步处理异常通知。
    /// </summary>
    /// <param name="context">包含当前异常及可修改处理状态的通知上下文。</param>
    Task HandleAsync(ExceptionNotificationContext context);
}
