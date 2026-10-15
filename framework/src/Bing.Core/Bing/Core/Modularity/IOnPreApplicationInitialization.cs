namespace Bing.Core.Modularity;

/// <summary>
/// 定义应用主初始化前的模块钩子。
/// </summary>
public interface IOnPreApplicationInitialization
{
    /// <summary>
    /// 执行应用前置初始化。
    /// </summary>
    /// <param name="context">本轮应用初始化上下文。</param>
    void OnPreApplicationInitialization(BingModuleInitializationContext context);

    /// <summary>
    /// 异步执行应用前置初始化。
    /// </summary>
    /// <param name="context">本轮应用初始化上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context, CancellationToken cancellationToken);
}
