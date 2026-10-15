namespace Bing.Core.Modularity;

/// <summary>
/// 定义应用主初始化后的模块钩子。
/// </summary>
public interface IOnPostApplicationInitialization
{
    /// <summary>
    /// 执行应用后置初始化。
    /// </summary>
    /// <param name="context">本轮应用初始化上下文。</param>
    void OnPostApplicationInitialization(BingModuleInitializationContext context);

    /// <summary>
    /// 异步执行应用后置初始化。
    /// </summary>
    /// <param name="context">本轮应用初始化上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context, CancellationToken cancellationToken);
}
