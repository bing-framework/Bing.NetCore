using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Bing;

/// <summary>
/// Bing 框架加载器
/// </summary>
public static class BingLoader
{
    /// <summary>
    /// 注册类型
    /// </summary>
    public static event Action<Type> RegisterType;

    /// <summary>
    /// 注册类型
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void RegisterTypes(IServiceCollection services)
    {
        if (RegisterType == null)
            return;
        var allAssemblyFinder = services.GetOrAddAllAssemblyFinder();
        var assemblies = allAssemblyFinder.FindAll(true);
        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            Debug.WriteLine($"Assembly: {assembly.FullName}, TypeLength: {types.Length}");
            foreach (var type in types)
                RegisterType(type);
        }
    }
}
