using Bing.DependencyInjection;

namespace Bing.ExceptionHandling;

/// <summary>
/// 异常订阅器
/// </summary>
[MultipleDependency]
public interface IExceptionSubscriber : ITransientDependency
{
    /// <summary>
    /// 排序号。正序
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="context">异常通知上下文</param>
    Task HandleAsync(ExceptionNotificationContext context);
}
