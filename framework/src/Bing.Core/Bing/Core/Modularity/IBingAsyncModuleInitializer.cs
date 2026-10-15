using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 定义模块异步初始化能力。
/// </summary>
/// <remarks>
/// 实现此接口的模块必须通过异步入口启动，框架不会再调用其同步初始化方法。
/// </remarks>
public interface IBingAsyncModuleInitializer
{
    /// <summary>
    /// 异步初始化模块。
    /// </summary>
    /// <param name="context">模块初始化上下文。</param>
    /// <param name="cancellationToken">用于取消初始化的令牌。</param>
    Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken);
}
