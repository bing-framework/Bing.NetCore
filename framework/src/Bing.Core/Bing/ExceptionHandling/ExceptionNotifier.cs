using Bing.DependencyInjection;
using Bing.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.ExceptionHandling;

/// <summary>
/// 异常通知器
/// </summary>
public class ExceptionNotifier : IExceptionNotifier, ITransientDependency
{
    /// <summary>
    /// 日志
    /// </summary>
    public ILogger<ExceptionNotifier> Logger { get; set; }

    /// <summary>
    /// 混合服务作用域工厂
    /// </summary>
    protected IHybridServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 初始化一个<see cref="ExceptionNotifier"/>类型的实例
    /// </summary>
    /// <param name="serviceScopeFactory">混合服务作用域工厂</param>
    public ExceptionNotifier(IHybridServiceScopeFactory serviceScopeFactory)
    {
        ServiceScopeFactory = serviceScopeFactory;
        Logger = NullLogger<ExceptionNotifier>.Instance;
    }

    /// <summary>
    /// 通知
    /// </summary>
    /// <param name="context">异常通知上下文</param>
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
