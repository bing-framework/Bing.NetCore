using System;
using System.ComponentModel;
using System.Linq;
using Bing.Core.Modularity;
using Bing.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Dependency
{
    /// <summary>
    /// 依赖注入模块
    /// </summary>
    [Description("依赖注入模块")]
    public class DependencyModule : BingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Core;

        /// <summary>
        /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
        /// 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为>0的顺序值
        /// </summary>
        public override int Order => 1;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            // 服务定位器设置
            ServiceLocator.Instance.SetServiceCollection(services);
            services.AddTransient(typeof(Lazy<>), typeof(Lazier<>));
            // 查找所有自动注册的服务实现类型
            var dependencyTypeFinder =
                services.GetOrAddTypeFinder<IDependencyTypeFinder>(assemblyFinder =>
                    new DependencyTypeFinder(assemblyFinder));

            var dependencyTypes = dependencyTypeFinder.FindAll();
            foreach (var dependencyType in dependencyTypes)
                AddToServices(services, dependencyType);
            return services;
        }

        /// <summary>
        /// 应用模块服务
        /// </summary>
        /// <param name="provider">服务提供程序</param>
        public override void UseModule(IServiceProvider provider)
        {
            ServiceLocator.Instance.SetApplicationServiceProvider(provider);
            Enabled = true;
        }

        /// <summary>
        /// 将服务实现类型注册到服务集合中
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="implementationType">要注册的服务实现类型</param>
        protected virtual void AddToServices(IServiceCollection services, Type implementationType)
        {
            if(implementationType.IsAbstract||implementationType.IsInterface)
                return;
            var lifetime = GetLifetimeOrNull(implementationType);
            if (lifetime == null)
                return;
            var dependencyAttribute = implementationType.GetAttribute<DependencyAttribute>();
            var serviceTypes = GetImplementedInterfaces(implementationType);

            // 服务数量为0时注册自身
            if (serviceTypes.Length == 0)
            {
                services.TryAdd(new ServiceDescriptor(implementationType, implementationType, lifetime.Value));
                return;
            }

            // 服务实现显式要求注册自身时，注册自身并且继续注册接口
            if (dependencyAttribute?.AddSelf == true)
                services.TryAdd(new ServiceDescriptor(implementationType, implementationType, lifetime.Value));

            // 注册服务
            for (var i = 0; i < serviceTypes.Length; i++)
            {
                var serviceType = serviceTypes[i];
                var descriptor = new ServiceDescriptor(serviceType, implementationType, lifetime.Value);
                if (lifetime.Value == ServiceLifetime.Transient)
                {
                    services.TryAddEnumerable(descriptor);
                    continue;
                }

                bool multiple = serviceType.HasAttribute<MultipleDependencyAttribute>();
                if (i == 0)
                {
                    if (multiple)
                        services.Add(descriptor);
                    else
                        AddSingleService(services, descriptor, dependencyAttribute);
                }
                else
                {
                    // 有多个接口，后边的接口注册使用第一个接口的实例，保证同个实现类的多个接口获得同一实例
                    var firstServiceType = serviceTypes[0];
                    descriptor = new ServiceDescriptor(serviceType, provider => provider.GetService(firstServiceType),
                        lifetime.Value);
                    if (multiple)
                        services.Add(descriptor);
                    else
                        AddSingleService(services, descriptor, dependencyAttribute);
                }
            }
        }

        /// <summary>
        /// 重写以实现 从类型获取要注册的<see cref="ServiceLifetime"/>生命周期类型
        /// </summary>
        /// <param name="type">依赖注入实现类型</param>
        protected virtual ServiceLifetime? GetLifetimeOrNull(Type type)
        {
            var attribute = type.GetAttribute<DependencyAttribute>();
            if (attribute != null)
                return attribute.Lifetime;
            if (type.IsDeriveClassFrom<ITransientDependency>())
                return ServiceLifetime.Transient;
            if (type.IsDeriveClassFrom<IScopedDependency>())
                return ServiceLifetime.Scoped;
            if (type.IsDeriveClassFrom<ISingletonDependency>())
                return ServiceLifetime.Singleton;
            return null;
        }

        /// <summary>
        /// 重写以实现 获取实现类型的所有可注册服务接口
        /// </summary>
        /// <param name="type">依赖注入实现类型</param>
        protected virtual Type[] GetImplementedInterfaces(Type type)
        {
            var exceptInterfaces = new[] {typeof(IDisposable)};
            var interfaceTypes = type.GetInterfaces()
                .Where(x => !exceptInterfaces.Contains(x) && !x.HasAttribute<IgnoreDependencyAttribute>()).ToArray();
            for (var index = 0; index < interfaceTypes.Length; index++)
            {
                var interfaceType = interfaceTypes[index];
                if (interfaceType.IsGenericType && !interfaceType.IsGenericTypeDefinition && interfaceType.FullName == null)
                    interfaceTypes[index] = interfaceType.GetGenericTypeDefinition();
            }
            return interfaceTypes;
        }

        /// <summary>
        /// 添加单一服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="descriptor">描述</param>
        /// <param name="dependencyAttribute">依赖注入行为特性</param>
        private static void AddSingleService(IServiceCollection services, ServiceDescriptor descriptor,
            DependencyAttribute dependencyAttribute)
        {
            if (dependencyAttribute?.ReplaceExisting == true)
                services.Replace(descriptor);
            else if (dependencyAttribute?.TryAdd == true)
                services.TryAdd(descriptor);
            else
                services.Add(descriptor);
        }
    }
}
