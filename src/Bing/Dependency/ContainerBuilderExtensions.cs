using System;
using AspectCore.Configuration;
using AspectCore.DynamicProxy;
using AspectCore.DynamicProxy.Parameters;
using AspectCore.Extensions.AspectScope;
using AspectCore.Extensions.Autofac;
using Autofac;
using Autofac.Builder;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Dependency
{
    /// <summary>
    /// Autofac 容器生成器 扩展
    /// </summary>
    public static partial class ContainerBuilderExtensions
    {
        #region AddTransient(注册服务，生命周期为 InstancePerDependency - 每次创建一个新实例)

        /// <summary>
        /// 注册服务，生命周期为 InstancePerDependency(每次创建一个新实例)
        /// </summary>
        /// <typeparam name="TService">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        /// <param name="builder">容器生成器</param>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        public static IRegistrationBuilder<TImplementation, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddTransient<TService, TImplementation>(this ContainerBuilder builder, string name = null)
            where TService : class where TImplementation : class, TService
        {
            if (name == null)
            {
                return builder.RegisterType<TImplementation>().As<TService>().InstancePerDependency();
            }
            return builder.RegisterType<TImplementation>().Named<TService>(name).InstancePerDependency();
        }
        #endregion

        #region AddScoped(注册服务，生命周期为 InstancePerLifetimeScope - 每个请求一个实例)

        /// <summary>
        /// 注册服务，生命周期为 InstancePerLifetimeScope(每个请求一个实例)
        /// </summary>
        /// <typeparam name="TService">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        /// <param name="builder">容器生成器</param>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        public static IRegistrationBuilder<TImplementation, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddScoped<TService, TImplementation>(this ContainerBuilder builder, string name = null)
            where TService : class where TImplementation : class, TService
        {
            if (name == null)
            {
                return builder.RegisterType<TImplementation>().As<TService>().InstancePerLifetimeScope();
            }
            return builder.RegisterType<TImplementation>().Named<TService>(name).InstancePerLifetimeScope();
        }

        /// <summary>
        /// 注册服务，生命周期为 InstancePerLifetimeScope(每个请求一个实例)
        /// </summary>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        /// <param name="builder">容器生成器</param>
        /// <returns></returns>
        public static IRegistrationBuilder<TImplementation, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddScoped<TImplementation>(this ContainerBuilder builder) where TImplementation : class
        {
            return builder.RegisterType<TImplementation>().InstancePerLifetimeScope();
        }

        #endregion

        #region AddSingleton(注册服务，生命周期为 SingleInstance - 单例)

        /// <summary>
        /// 注册服务，生命周期为 SingleInstance(单例)
        /// </summary>
        /// <typeparam name="TService">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        /// <param name="builder">容器生成器</param>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        public static IRegistrationBuilder<TImplementation, ConcreteReflectionActivatorData, SingleRegistrationStyle>
            AddSingleton<TService, TImplementation>(this ContainerBuilder builder, string name = null)
            where TService : class where TImplementation : class, TService
        {
            if (name == null)
            {
                return builder.RegisterType<TImplementation>().As<TService>().SingleInstance();
            }
            return builder.RegisterType<TImplementation>().Named<TService>(name).SingleInstance();
        }

        /// <summary>
        /// 注册服务，生命周期为 SingleInstance(单例)
        /// </summary>
        /// <typeparam name="TService">接口类型</typeparam>
        /// <param name="builder">容器生成器</param>
        /// <param name="instance">服务实例</param>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        public static IRegistrationBuilder<TService, SimpleActivatorData, SingleRegistrationStyle>
            AddSingleton<TService>(this ContainerBuilder builder, TService instance, string name = null)
            where TService : class
        {
            if (name == null)
            {
                return builder.RegisterInstance(instance).As<TService>().SingleInstance();
            }
            return builder.RegisterInstance(instance).Named<TService>(name).SingleInstance();
        }

        #endregion

        #region EnableAop(启用Aop)

        /// <summary>
        /// 启用Aop
        /// </summary>
        /// <param name="builder">容器生成器</param>
        /// <param name="configAction">Aop配置</param>
        public static void EnableAop(this ContainerBuilder builder, Action<IAspectConfiguration> configAction = null)
        {
            builder.RegisterDynamicProxy(config =>
            {
                config.EnableParameterAspect();
                config.NonAspectPredicates.Add(t =>
                    Reflection.GetTopBaseType(t.DeclaringType).SafeString() ==
                    "Microsoft.EntityFrameworkCore.DbContext");
                configAction?.Invoke(config);
            });
            builder.EnableAspectScoped();
        }

        #endregion

        #region EnableAspectScoped(启用Aop作用域)

        /// <summary>
        /// 启用Aop作用域
        /// </summary>
        /// <param name="builder">容器生成器</param>
        public static void EnableAspectScoped(this ContainerBuilder builder)
        {
            builder.AddScoped<IAspectScheduler, ScopeAspectScheduler>();
            builder.AddScoped<IAspectBuilderFactory, ScopeAspectBuilderFactory>();
            builder.AddScoped<IAspectContextFactory, ScopeAspectContextFactory>();
        }

        #endregion
    }
}
