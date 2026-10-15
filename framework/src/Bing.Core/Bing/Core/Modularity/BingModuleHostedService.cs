using Bing.Core.Builders;
using Bing.DependencyInjection;
using Bing.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bing.Core.Modularity;

/// <summary>
/// 将宿主停止通知转发给模块生命周期管理器。
/// </summary>
/// <remarks>
/// Web 管道装配仍由调用方在 <c>UseBing</c> 或 <c>UseBingAsync</c> 时完成；Generic Host 后台模式可由注册记录显式启用启动初始化。
/// </remarks>
internal sealed class BingModuleHostedService : IHostedService
{
    /// <summary>
    /// 模块生命周期管理器。
    /// </summary>
    private readonly IBingModuleManager _manager;

    /// <summary>
    /// 当前服务集合的模块注册记录。
    /// </summary>
    private readonly BingModuleRegistration _registration;

    /// <summary>
    /// 初始化一个 <see cref="BingModuleHostedService" /> 类型的实例。
    /// </summary>
    /// <param name="manager">模块生命周期管理器。</param>
    /// <param name="registration">当前服务集合的模块注册记录。</param>
    public BingModuleHostedService(IBingModuleManager manager, BingModuleRegistration registration)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) =>
        _registration.InitializeOnHostStart
            ? _manager.InitializeAsync(cancellationToken: cancellationToken)
            : Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => _manager.ShutdownAsync(cancellationToken);
}
