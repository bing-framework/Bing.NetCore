using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Bing.Localization;

/// <summary>
/// 提供本地化服务解析所需的服务提供程序和本地化工厂。
/// </summary>
public class LocalizationContext : IServiceProviderAccessor
{
    /// <inheritdoc />
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 获取构造时从服务提供程序解析的字符串本地化工厂。
    /// </summary>
    public IStringLocalizerFactory LocalizerFactory { get; }

    /// <summary>
    /// 使用服务提供程序初始化 <see cref="LocalizationContext"/> 的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析 <see cref="IStringLocalizerFactory"/> 的服务提供程序。</param>
    /// <exception cref="InvalidOperationException">未注册 <see cref="IStringLocalizerFactory"/> 时抛出。</exception>
    public LocalizationContext(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        LocalizerFactory = ServiceProvider.GetRequiredService<IStringLocalizerFactory>();
    }
}
