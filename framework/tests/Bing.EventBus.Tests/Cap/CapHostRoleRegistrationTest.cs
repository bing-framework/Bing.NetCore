using Bing.Events.Cap;
using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Transport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EventBus.Tests.Cap;

/// <summary>
/// 验证 CAP 宿主角色的处理器边界。
/// </summary>
public sealed class CapHostRoleRegistrationTest
{
    /// <summary>
    /// 验证仅发布角色保留 Dispatcher 并移除消费者处理器。
    /// </summary>
    [Fact]
    public void EnableBingCapPublisherOnly_ShouldKeepDispatcherAndRemoveConsumers()
    {
        var services = new ServiceCollection();
        services.AddCapEventBus(_ => { });

        services.EnableBingCapPublisherOnly();

        var processors = services.Where(item => item.ServiceType == typeof(IProcessingServer)).ToArray();
        Assert.Single(processors);
        Assert.Equal(typeof(IDispatcher), processors[0].ImplementationFactory?.Method.ReturnType);
        Assert.Contains(services, item => item.ServiceType == typeof(IConsumerRegister));
        Assert.DoesNotContain(processors, item => item.ImplementationType?.Name == "CapProcessingServer");
    }

    /// <summary>
    /// 验证重复启用仅发布角色不会继续删除调用方处理器。
    /// </summary>
    [Fact]
    public void EnableBingCapPublisherOnly_WhenRepeated_ShouldBeIdempotent()
    {
        var services = new ServiceCollection();
        services.AddCapEventBus(_ => { });

        services.EnableBingCapPublisherOnly();
        var count = services.Count(item => item.ServiceType == typeof(IProcessingServer));
        services.EnableBingCapPublisherOnly();

        Assert.Equal(count, services.Count(item => item.ServiceType == typeof(IProcessingServer)));
    }

    /// <summary>
    /// 验证调用方自定义处理器不会被角色扩展误删。
    /// </summary>
    [Fact]
    public void EnableBingCapPublisherOnly_ShouldPreserveCustomProcessor()
    {
        var services = new ServiceCollection();
        services.AddCapEventBus(_ => { });
        services.AddSingleton<IProcessingServer, CustomProcessor>();

        services.EnableBingCapPublisherOnly();

        Assert.Contains(services, item => item.ServiceType == typeof(IProcessingServer) &&
            item.ImplementationType == typeof(CustomProcessor));
    }

    /// <summary>
    /// 用于验证自定义 CAP 处理器注册的测试替身。
    /// </summary>
    private sealed class CustomProcessor : IProcessingServer
    {
        /// <inheritdoc />
        public Task Start(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
