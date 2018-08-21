using System;
using AspectCore.Extensions.DependencyInjection;
using Bing.Datas.EntityFramework.Core;
using Bing.Datas.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
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
        /// <returns></returns>
        public static IServiceCollection AddUnitOfWork<TService, TImplementation>(this IServiceCollection services,
            Action<DbContextOptionsBuilder> configAction) 
            where TService : class, IUnitOfWork
            where TImplementation : UnitOfWorkBase, TService
        {
            services.AddDynamicProxy(config =>
            {
                config.NonAspectPredicates.Add(t => t.DeclaringType?.BaseType == typeof(DbContext));
            });
            services.AddDbContext<TImplementation>(configAction);
            services.TryAddScoped<TService>(t => t.GetService<TImplementation>());
            return services;
        }
    }
}
