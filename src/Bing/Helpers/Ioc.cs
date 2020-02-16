using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Dependency;

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
        internal static Container DefaultContainer { get; } = new Container();

        /// <summary>
        /// 创建集合
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="name">服务名称</param>
        public static List<T> CreateList<T>(string name = null)
        {
            //return DefaultContainer.CreateList<T>(name);
            return ServiceLocator.Instance.GetServices<T>().ToList();
        }

        /// <summary>
        /// 创建集合
        /// </summary>
        /// <typeparam name="TResult">对象类型</typeparam>
        /// <param name="type">对象类型</param>
        /// <param name="name">服务名称</param>
        public static List<TResult> CreateList<TResult>(Type type, string name = null)
        {
            return ((IEnumerable<TResult>)DefaultContainer.CreateList(type, name)).ToList();
        }

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="name">服务名称</param>
        public static T Create<T>(string name = null)
        {
            //return DefaultContainer.Create<T>(name);
            return ServiceLocator.Instance.GetService<T>();
        }

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <param name="type">对象类型</param>
        /// <param name="name">服务名称</param>
        public static object Create(Type type, string name = null)
        {
            //return DefaultContainer.Create(type, name);
            return ServiceLocator.Instance.GetService(type);
        }

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TResult">对象类型</typeparam>
        /// <param name="type">对象类型</param>
        /// <param name="name">服务名称</param>
        public static TResult Create<TResult>(Type type, string name = null)
        {
            return (TResult)DefaultContainer.Create(type, name);
        }

        /// <summary>
        /// 作用域开始
        /// </summary>
        public static IScope BeginScope()
        {
            return DefaultContainer.BeginScope();
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
