using System;
using Bing.Datas.Configs;
using Bing.Datas.EntityFramework.Core;
using Bing.Datas.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Datas.EntityFramework.Extensions
{
    /// <summary>
    /// 数据服务 扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册工作单元服务
        /// </summary>
        /// <typeparam name="TService">工作单元接口类型</typeparam>
        /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
        /// <param name="services">服务集合</param>
        /// <param name="configAction">配置操作</param>
        /// <param name="dataConfigAction">数据配置操作</param>
        /// <param name="configuration">配置</param>
        /// <returns></returns>
        public static IServiceCollection AddUnitOfWork<TService, TImplementation>(this IServiceCollection services,
            Action<DbContextOptionsBuilder> configAction, Action<DataConfig> dataConfigAction = null,
            IConfiguration configuration = null)
            where TService : class, IUnitOfWork
            where TImplementation : UnitOfWorkBase, TService
        {
            services.AddDbContext<TImplementation>(configAction);
            var dataConfig = new DataConfig();
            if (dataConfigAction != null)
            {
                services.Configure(dataConfigAction);
                dataConfigAction.Invoke(dataConfig);
            }

            if (configuration != null)
            {
                services.Configure<DataConfig>(configuration);
            }

            services.TryAddScoped<TService>(t => t.GetService<TImplementation>());
            services.TryAddScoped<IUnitOfWork>(t => t.GetService<TImplementation>());
            return services;
        }
    }
}
