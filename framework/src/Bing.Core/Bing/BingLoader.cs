using System.Diagnostics;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing;

/// <summary>
/// Bing 框架加载器
/// </summary>
public static class BingLoader
{
    /// <summary>
    /// 按服务集合保存类型注册状态。
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IServiceCollection, RegistrationState> RegisteredCollections = new();

    /// <summary>
    /// 注册发现的类型。
    /// </summary>
    public static event Action<Type> RegisterType;

    /// <summary>
    /// 扫描服务集合关联的程序集并注册发现的类型。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <remarks>
    /// 同一服务集合只记录一次完成状态；公开注册事件仍按发现到的类型逐个触发。
    /// </remarks>
    public static void RegisterTypes(IServiceCollection services)
    {
        if (RegisterType == null && !Configuration.OptionsTypeRegistration.TryGet(services, out _))
        {
            if (services != null)
                RegisteredCollections.GetValue(services, _ => new RegistrationState()).Completed = true;
            return;
        }
        foreach (var type in FindTypes(services))
        {
            if (Configuration.OptionsTypeRegistration.TryGet(services, out var registration))
                registration.Register(services, type);
            RegisterType?.Invoke(type);
        }
        RegisteredCollections.GetValue(services, _ => new RegistrationState()).Completed = true;
    }

    /// <summary>
    /// 判断服务集合是否已完成类型扫描。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>已完成扫描时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    internal static bool HasRegisteredTypes(IServiceCollection services) =>
        services != null && RegisteredCollections.TryGetValue(services, out var state) && state.Completed;

    /// <summary>
    /// 为已启用选项类型注册的服务集合补扫类型，不触发公开注册事件。
    /// </summary>
    /// <param name="services">服务集合</param>
    internal static void RegisterOptionsTypes(IServiceCollection services)
    {
        if (!Configuration.OptionsTypeRegistration.TryGet(services, out var registration))
            return;
        foreach (var type in FindTypes(services))
            registration.Register(services, type);
    }

    /// <summary>
    /// 查找服务集合关联程序集中的所有类型。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>按程序集扫描顺序返回的类型序列。</returns>
    private static IEnumerable<Type> FindTypes(IServiceCollection services)
    {
        var allAssemblyFinder = services.GetOrAddAllAssemblyFinder();
        if (allAssemblyFinder is Core.Modularity.BingModuleAssemblyFinder frozen)
        {
            foreach (var type in frozen.FindTypes())
                yield return type;
            yield break;
        }
        foreach (var assembly in allAssemblyFinder.FindAll(true))
        {
            var types = AssemblyHelper.GetAllTypes(assembly).OfType<Type>().ToArray();
            Debug.WriteLine($"Assembly: {assembly.FullName}, TypeLength: {types.Length}");
            foreach (var type in types)
                yield return type;
        }
    }

    /// <summary>
    /// 表示某个服务集合的类型注册状态。
    /// </summary>
    private sealed class RegistrationState
    {
        /// <summary>
        /// 获取或设置类型扫描是否已完成。
        /// </summary>
        public bool Completed { get; set; }
    }
}
