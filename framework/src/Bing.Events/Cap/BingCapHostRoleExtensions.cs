using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Processor;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Events.Cap;

/// <summary>
/// 提供 CAP 宿主角色注册扩展。
/// </summary>
public static class BingCapHostRoleExtensions
{
    /// <summary>
    /// 将当前 CAP 宿主限制为仅处理出箱发布。
    /// </summary>
    /// <param name="services">已完成 <see cref="Extensions.AddCapEventBus"/> 注册的服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>
    /// 该方法移除 CAP 原生消费者注册处理器别名，同时保留 CAP 处理服务器；处理服务器包含发布 Outbox
    /// 重试、延迟消息和清理任务。默认未调用时保持 CAP 的发布和消费行为。
    /// 调用方必须在其他进程或宿主中运行同一逻辑消费者组；本方法不会创建消费者工作进程。
    /// </remarks>
    public static IServiceCollection EnableBingCapPublisherOnly(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType != typeof(IProcessingServer))
                continue;

            // CAP 用返回类型为 IConsumerRegister 的工厂注册消费者处理别名。
            var isConsumerAlias = descriptor.ImplementationFactory?.Method.ReturnType == typeof(IConsumerRegister);
            if (isConsumerAlias)
                services.RemoveAt(index);
        }
        return services;
    }
}
