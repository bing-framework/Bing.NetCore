using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.Configuration
{
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
        public static IServiceCollection AddOptions(this IServiceCollection services, IConfiguration configuration)
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
                    services.AddOptions(type, section, _ => { });
                }
            };
            return services;
        }

        /// <summary>
        /// 注册选项配置
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="optionsType">选项配置类型</param>
        /// <param name="configuration">配置</param>
        /// <param name="configurationBinder">配置绑定操作</param>
        private static void AddOptions(this IServiceCollection services, Type optionsType, IConfiguration configuration, Action<BinderOptions> configurationBinder)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            if (!optionsType.IsClass)
                throw new ArgumentException("optionsType should be class.");

            var configurationChangeTokenSourceType = typeof(ConfigurationChangeTokenSource<>).MakeGenericType(optionsType);
            var configurationChangeTokenSource = Activator.CreateInstance(configurationChangeTokenSourceType, string.Empty, configuration);

            if (configurationChangeTokenSource == null)
                throw new ArgumentNullException(nameof(configurationChangeTokenSource));
            services.AddSingleton(typeof(IOptionsChangeTokenSource<>).MakeGenericType(optionsType), configurationChangeTokenSource);

            var namedConfigureFromConfigurationOptionsType = typeof(NamedConfigureFromConfigurationOptions<>).MakeGenericType(optionsType);
            var namedConfigureFromConfigurationOptions = Activator.CreateInstance(namedConfigureFromConfigurationOptionsType, string.Empty, configuration, configurationBinder);
            if (namedConfigureFromConfigurationOptions == null)
                throw new ArgumentNullException(nameof(namedConfigureFromConfigurationOptions));
            services.AddSingleton(typeof(IConfigureOptions<>).MakeGenericType(optionsType), namedConfigureFromConfigurationOptions);
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
}
