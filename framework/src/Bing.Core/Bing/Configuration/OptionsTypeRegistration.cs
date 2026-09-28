using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Configuration;

/// <summary>
/// 按服务集合隔离的选项类型注册器。
/// </summary>
internal sealed class OptionsTypeRegistration
{
    /// <summary>
    /// 按服务集合保存选项注册器。
    /// </summary>
    private static readonly ConditionalWeakTable<IServiceCollection, OptionsTypeRegistration> Registrations = new();

    /// <summary>
    /// 选项配置源。
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化选项类型注册器。
    /// </summary>
    /// <param name="configuration">选项配置源。</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> 为空时抛出。</exception>
    private OptionsTypeRegistration(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// 获取服务集合的注册器；同一集合重复调用时保留首次配置。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">选项配置源。</param>
    /// <returns>服务集合对应的选项类型注册器。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> 或 <paramref name="configuration"/> 为空时抛出。</exception>
    public static OptionsTypeRegistration GetOrCreate(IServiceCollection services, IConfiguration configuration)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        return Registrations.GetValue(services, _ => new OptionsTypeRegistration(configuration));
    }

    /// <summary>
    /// 尝试获取服务集合对应的选项类型注册器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="registration">获取到的注册器；未找到时为 <see langword="null"/>。</param>
    /// <returns>找到注册器时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public static bool TryGet(IServiceCollection services, out OptionsTypeRegistration registration)
    {
        if (services == null)
        {
            registration = null;
            return false;
        }
        return Registrations.TryGetValue(services, out registration);
    }

    /// <summary>
    /// 注册类型上的选项配置服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="type">待检查的类型。</param>
    public void Register(IServiceCollection services, Type type)
    {
        if (type == null || type.IsAbstract || type.IsInterface)
            return;
        var attribute = type.GetCustomAttribute<OptionsTypeAttribute>();
        if (attribute == null)
            return;
        var section = string.IsNullOrWhiteSpace(attribute.SectionName)
            ? _configuration
            : _configuration.GetSection(attribute.SectionName);
        Extensions.RegisterOptionsType(services, type, section, _ => { });
    }
}
