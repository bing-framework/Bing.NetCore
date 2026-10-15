using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 管理应用所属模块的初始化和关闭生命周期。
/// </summary>
public interface IBingModuleManager
{
    /// <summary>
    /// 初始化模块一次。
    /// </summary>
    /// <param name="context">可选的模块初始化上下文。</param>
    /// <remarks>初始化失败后不可重试；包含异步模块时必须使用异步入口。</remarks>
    void Initialize(BingModuleInitializationContext context = null);

    /// <summary>
    /// 初始化模块一次。
    /// </summary>
    /// <param name="context">可选的模块初始化上下文。</param>
    /// <param name="cancellationToken">用于取消初始化的令牌。</param>
    /// <remarks>非 Web 并发请求共享同一次执行，重入请求会被拒绝。</remarks>
    Task InitializeAsync(BingModuleInitializationContext context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按逆序停止并释放模块。
    /// </summary>
    /// <remarks>包含仅异步关闭模块时必须使用异步入口。</remarks>
    void Shutdown();

    /// <summary>
    /// 按逆序停止并释放模块。
    /// </summary>
    /// <param name="cancellationToken">用于取消关闭的令牌。</param>
    /// <remarks>此方法不释放宿主根容器。</remarks>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
