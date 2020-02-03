using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 自动注册数据
    /// </summary>
    public class AutoRegisterData
    {
        /// <summary>
        /// 服务集合
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// 注入类型集合
        /// </summary>
        public IEnumerable<Type> InjectionTypes { get; }

        /// <summary>
        /// 类型过滤器
        /// </summary>
        public Func<Type, bool> TypeFilter { get; set; }

        /// <summary>
        /// 初始化一个<see cref="AutoRegisterData"/>类型的实例
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="injectionTypes">注入类型集合</param>
        public AutoRegisterData(IServiceCollection services, IEnumerable<Type> injectionTypes)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            InjectionTypes = injectionTypes ?? throw new ArgumentNullException(nameof(injectionTypes));
        }
    }

    /// <summary>
    /// 自动注册数据(<see cref="AutoRegisterData"/>) 扩展
    /// </summary>
    public static class AutoRegisterDataExtensions
    {
        /// <summary>
        /// 条件查询
        /// </summary>
        /// <param name="autoRegisterData">自动注册数据</param>
        /// <param name="predicate">查询条件</param>
        public static AutoRegisterData Where(this AutoRegisterData autoRegisterData, Func<Type, bool> predicate)
        {
            if (autoRegisterData == null)
                throw new ArgumentNullException(nameof(autoRegisterData));
            autoRegisterData.TypeFilter = predicate;
            return new AutoRegisterData(autoRegisterData.Services, autoRegisterData.InjectionTypes.Where(predicate));
        }

        /// <summary>
        /// 将类实现的任何公共接口(IDisposable除外)进行注册
        /// </summary>
        /// <param name="autoRegisterData">自动注册数据</param>
        /// <param name="lifetime">生命周期</param>
        public static IServiceCollection AsPublicImplementedInterfaces(this AutoRegisterData autoRegisterData,
            ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            if (autoRegisterData == null)
                throw new ArgumentNullException(nameof(autoRegisterData));
            foreach (var classType in (autoRegisterData.TypeFilter == null ? autoRegisterData.InjectionTypes : autoRegisterData.InjectionTypes.Where(autoRegisterData.TypeFilter)))
            {
                var interfaces = classType.GetTypeInfo().ImplementedInterfaces.Where(x => x != typeof(IDisposable) && x.IsPublic);
                foreach (var @interface in interfaces)
                {
                    autoRegisterData.Services.Add(new ServiceDescriptor(@interface, classType, lifetime));
                }
            }

            return autoRegisterData.Services;
        }
    }
}
