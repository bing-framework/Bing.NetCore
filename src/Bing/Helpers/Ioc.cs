using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.Dependency;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Helpers
{
    /// <summary>
    /// 容器操作
    /// </summary>
    public static class Ioc
    {
        /// <summary>
        /// 默认容器
        /// </summary>
        internal static readonly DefaultContainer DefaultContainer=new DefaultContainer();

        /// <summary>
        /// 创建容器
        /// </summary>
        /// <param name="configs">依赖配置</param>
        /// <returns></returns>
        public static IContainer CreateContainer(params IConfig[] configs)
        {
            var container=new DefaultContainer();
            container.Register(null, builder => builder.EnableAop(), configs);
            return container;
        }

        /// <summary>
        /// 创建集合
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        public static List<T> CreateList<T>(string name = null)
        {
            return DefaultContainer.CreateList<T>(name);
        }

        /// <summary>
        /// 创建集合
        /// </summary>
        /// <typeparam name="TResult">返回类型</typeparam>
        /// <param name="type">对象类型</param>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        public static List<TResult> CreateList<TResult>(Type type, string name = null)
        {
            return ((IEnumerable<TResult>) DefaultContainer.CreateList(type, name)).ToList();
        }

        /// <summary>
        /// 创建对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        public static T Create<T>(string name = null)
        {
            return DefaultContainer.Create<T>(name);
        }

        /// <summary>
        /// 创建对象
        /// </summary>
        /// <param name="type">对象类型</param>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        public static object Create(Type type, string name = null)
        {
            return DefaultContainer.Create(type, name);
        }

        /// <summary>
        /// 创建对象
        /// </summary>
        /// <typeparam name="TResult">返回类型</typeparam>
        /// <param name="type">对象类型</param>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        public static TResult Create<TResult>(Type type, string name = null)
        {
            return (TResult) DefaultContainer.Create(type, name);
        }

        /// <summary>
        /// 作用域开始
        /// </summary>
        /// <returns></returns>
        public static IScope BeginScope()
        {
            return DefaultContainer.BeginScope();
        }

        /// <summary>
        /// 注册依赖
        /// </summary>
        /// <param name="configs">依赖配置</param>
        public static void Register(params IConfig[] configs)
        {
            DefaultContainer.Register(null, builder => builder.EnableAop(), configs);
        }

        /// <summary>
        /// 注册依赖
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configs">依赖配置</param>
        /// <returns></returns>
        public static IServiceProvider Register(IServiceCollection services, params IConfig[] configs)
        {
            return DefaultContainer.Register(services, builder => builder.EnableAop(), configs);
        }

        /// <summary>
        /// 释放容器
        /// </summary>
        public static void Dispose()
        {
            DefaultContainer?.Dispose();
        }
    }
}
