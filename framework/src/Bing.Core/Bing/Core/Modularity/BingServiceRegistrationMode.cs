using System.Runtime.Loader;

namespace Bing.Core.Modularity;

/// <summary>
/// 约定服务注册的执行时机。
/// </summary>
public enum BingServiceRegistrationMode
{
    /// <summary>
    /// 保留现有服务注册顺序。
    /// </summary>
    Compatible,
    /// <summary>
    /// 在模块主服务配置前注册约定服务。
    /// </summary>
    BeforeConfigureServices
}
