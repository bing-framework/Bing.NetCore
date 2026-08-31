using Bing.DependencyInjection;
using Bing.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.ExceptionHandling;

/// <summary>
/// 在独立服务作用域中向已注册订阅器分发异常通知的默认实现。
/// </summary>
public class ExceptionNotifier : IExceptionNotifier, ITransientDependency
{
    /// <summary>
    /// 获取或设置用于记录订阅器处理异常的日志记录器。
    /// </summary>
    public ILogger<ExceptionNotifier> Logger { get; set; }

    /// <summary>
    /// 获取用于创建订阅器解析作用域的服务作用域工厂。
    /// </summary>
    protected IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 使用服务作用域工厂初始化 <see cref="ExceptionNotifier"/> 的实例。
    /// </summary>
    /// <param name="serviceScopeFactory">用于解析异常订阅器的服务作用域工厂。</param>
    public ExceptionNotifier(IServiceScopeFactory serviceScopeFactory)
    {
        ServiceScopeFactory = serviceScopeFactory;
        Logger = NullLogger<ExceptionNotifier>.Instance;
    }

    /// <inheritdoc />
    /// <remarks>每次通知创建独立作用域，订阅器按 <see cref="IExceptionSubscriber.Order"/> 升序执行；单个订阅器失败会记录警告后继续通知后续订阅器。</remarks>
    public virtual async Task NotifyAsync(ExceptionNotificationContext context)
    {
        Check.NotNull(context, nameof(context));
        using var scope = ServiceScopeFactory.CreateScope();
        var exceptionSubscribers = scope.ServiceProvider.GetServices<IExceptionSubscriber>().OrderBy(x => x.Order);
        foreach (var exceptionSubscriber in exceptionSubscribers)
        {
            try
            {
                await exceptionSubscriber.HandleAsync(context);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"{exceptionSubscriber.GetType().AssemblyQualifiedName} 异常订阅器抛出异常!");
                Logger.LogException(e, LogLevel.Warning);
            }
        }
    }
}
