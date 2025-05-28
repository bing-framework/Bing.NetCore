using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bing.Configuration;

/// <summary>
/// 服务集合(<see cref="IServiceCollection"/>) 扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 注册选项配置
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    public static IServiceCollection AddOptionsType(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions();
        BingLoader.RegisterType += type =>
        {
            if (type.IsAbstract || type.IsInterface)
                return;
            var attribute = type.GetCustomAttribute<OptionsTypeAttribute>();
            if (attribute != null)
            {
                var section = string.IsNullOrWhiteSpace(attribute.SectionName)
                    ? configuration
                    : configuration.GetSection(attribute.SectionName);
                services.AddOptionsType(type, section, _ => { });
            }
        };
        return services;
    }

    /// <summary>
    /// 将指定的 <paramref name="optionsType"/> 配置绑定到 <paramref name="configuration"/>，并注册到依赖注入容器中。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="optionsType">要绑定的选项类型，必须是非抽象类。</param>
    /// <param name="configuration">配置</param>
    /// <param name="configurationBinder">配置绑定操作</param>
    /// <exception cref="ArgumentNullException">
    /// 当 <paramref name="services"/> 或 <paramref name="configuration"/> 为空时抛出。
    /// </exception>
    /// <exception cref="ArgumentException">
    /// 当 <paramref name="optionsType"/> 不是可实例化的类（即抽象类或接口）时抛出。
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// 当动态创建 <paramref name="optionsType"/> 相关的 `IOptionsChangeTokenSource` 或 `IConfigureOptions` 失败时抛出。
    /// </exception>
    private static void AddOptionsType(this IServiceCollection services, Type optionsType, IConfiguration configuration, Action<BinderOptions> configurationBinder)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (optionsType.IsAbstract || !optionsType.IsClass)
            throw new ArgumentException($"参数 {nameof(optionsType)} 必须是一个可实例化的类（非抽象类）。");

        // 创建并注册 IOptionsChangeTokenSource<T>
        var configurationChangeTokenSourceType = typeof(ConfigurationChangeTokenSource<>).MakeGenericType(optionsType);
        var configurationChangeTokenSource = Activator.CreateInstance(configurationChangeTokenSourceType, string.Empty, configuration);
        if (configurationChangeTokenSource == null)
            throw new InvalidOperationException($"无法创建 IOptionsChangeTokenSource 实例，类型: {configurationChangeTokenSourceType.FullName}。");
        services.TryAddSingleton(typeof(IOptionsChangeTokenSource<>).MakeGenericType(optionsType), _ => configurationChangeTokenSource);

        // 创建并注册 IConfigureOptions<T>
        var namedConfigureFromConfigurationOptionsType = typeof(NamedConfigureFromConfigurationOptions<>).MakeGenericType(optionsType);
        var namedConfigureFromConfigurationOptions = Activator.CreateInstance(namedConfigureFromConfigurationOptionsType, string.Empty, configuration, configurationBinder);
        if (namedConfigureFromConfigurationOptions == null)
            throw new InvalidOperationException($"无法创建 IConfigureOptions 实例，类型: {namedConfigureFromConfigurationOptionsType.FullName}。");
        services.TryAddSingleton(typeof(IConfigureOptions<>).MakeGenericType(optionsType), _ => namedConfigureFromConfigurationOptions);
    }

    /// <summary>
    /// 打印配置
    /// </summary>
    /// <param name="configuration">配置</param>
    /// <param name="writer">写入操作</param>
    public static void Print(this IConfiguration configuration, Action<string> writer)
    {
        if (configuration == null || writer == null)
            return;
        writer("Configuration: ");
        foreach (var kv in configuration.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(kv.Key))
                writer($"{kv.Key} = {kv.Value}");
        }
    }
}
