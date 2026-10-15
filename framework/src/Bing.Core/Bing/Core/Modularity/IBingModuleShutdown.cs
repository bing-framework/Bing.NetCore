using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 定义模块同步停止能力。
/// </summary>
public interface IBingModuleShutdown
{
    /// <summary>
    /// 停止模块自持有的工作和订阅。
    /// </summary>
    /// <param name="context">模块关闭上下文。</param>
    void Shutdown(BingModuleShutdownContext context);

    /// <summary>
    /// 异步停止模块自持有的工作和订阅。
    /// </summary>
    /// <param name="context">模块关闭上下文。</param>
    /// <param name="cancellationToken">用于取消关闭的令牌。</param>
    Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken);
}
