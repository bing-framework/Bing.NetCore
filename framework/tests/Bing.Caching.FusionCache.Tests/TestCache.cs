global using Xunit;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 为混合缓存测试创建隔离的依赖注入容器与同步原语。
/// </summary>
internal static class TestCache
{
    /// <summary>
    /// 创建启用作用域与构建校验的测试服务提供程序。
    /// </summary>
    /// <param name="configure">覆盖 Bing 缓存配置的可选委托。</param>
    /// <param name="native">配置原生 FusionCache builder 的可选委托。</param>
    /// <param name="name">缓存实例名称，默认 <c>catalog</c>。</param>
    /// <returns>已注册命名混合缓存的测试服务提供程序。</returns>
    internal static ServiceProvider Create(Action<BingFusionCacheOptions>? configure = null,
        Action<IFusionCacheBuilder>? native = null, string name = "catalog")
    {
        var services = new ServiceCollection();
        services.AddBingFusionCache(name, o =>
        {
            o.Namespace = "test:" + Guid.NewGuid().ToString("N");
            o.UseAsDefault = true;
            configure?.Invoke(o);
        }, native);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>
    /// 创建可由测试手动完成的异步信号源。
    /// </summary>
    /// <typeparam name="T">信号结果类型。</typeparam>
    /// <returns>异步继续执行不内联的完成源。</returns>
    internal static TaskCompletionSource<T> Signal<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 轮询等待条件成立，超过五秒时抛出取消异常。
    /// </summary>
    /// <param name="condition">返回是否已满足条件的异步判断。</param>
    internal static async Task Eventually(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition())
            await Task.Delay(10, timeout.Token);
    }
}
