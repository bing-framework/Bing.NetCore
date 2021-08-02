using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bing.Canal.Server
{
    /// <summary>
    /// 服务扩展 - Canal服务
    /// </summary>
    public static partial class Extensions
    {
        public static IServiceCollection AddCanalService(this IServiceCollection services, Action<CanalConsumeRegister> setupAction)
        {
            if (setupAction == null)
                throw new ArgumentNullException(nameof(setupAction));
            var register = new CanalConsumeRegister();
            setupAction(register);
            if(!register.ConsumeList.Any()&&!register.SingletonConsumeList.Any())
                throw new ArgumentNullException(nameof(register.ConsumeList));
            services.AddOptions();
            services.TryAddSingleton<IConfigureOptions<CanalOptions>, ConfigureCanalOptions>();
            services.AddHostedService<SimpleCanalClientHostedService>();
            if (register.ConsumeList.Any())
            {
                foreach (var type in register.ConsumeList) 
                    services.TryAddTransient(type);
            }

            if (register.SingletonConsumeList.Any())
            {
                foreach (var type in register.SingletonConsumeList) 
                    services.TryAddSingleton(type);
            }

            services.AddSingleton(register);
            return services;
        }

        public static IServiceCollection AddClusterCanalService(this IServiceCollection services, Action<CanalConsumeRegister> setupAction)
        {
            if (setupAction == null)
                throw new ArgumentNullException(nameof(setupAction));
            var register = new CanalConsumeRegister();
            setupAction(register);
            if (!register.ConsumeList.Any() && !register.SingletonConsumeList.Any())
                throw new ArgumentNullException(nameof(register.ConsumeList));
            services.AddOptions();
            services.TryAddSingleton<IConfigureOptions<CanalOptions>, ConfigureCanalOptions>();
            services.AddHostedService<ClusterCanalClientHostedService>();
            if (register.ConsumeList.Any())
            {
                foreach (var type in register.ConsumeList)
                    services.TryAddTransient(type);
            }

            if (register.SingletonConsumeList.Any())
            {
                foreach (var type in register.SingletonConsumeList)
                    services.TryAddSingleton(type);
            }

            services.AddSingleton(register);
            return services;
        }
    }
}
