using System;
using System.Collections.Generic;
using Bing.Canal.Server.Models;

namespace Bing.Canal.Server
{
    /// <summary>
    /// Canal消费者注册器
    /// </summary>
    public class CanalConsumeRegister
    {
        /// <summary>
        /// 消费者列表 - 多例模式
        /// </summary>
        internal List<Type> ConsumeList { get; set; } = new List<Type>();

        /// <summary>
        /// 消费者列表 - 单例模式
        /// </summary>
        internal List<Type> SingletonConsumeList { get; set; } = new List<Type>();

        /// <summary>
        /// 注册消费组件 - 单例模式
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        public CanalConsumeRegister RegisterSingleton<T>() where T : INotificationHandler<CanalBody>
        {
            var type = typeof(T);
            if(!SingletonConsumeList.Contains(type))
                SingletonConsumeList.Add(type);
            return this;
        }

        /// <summary>
        /// 注册消费组件 - 多例模式
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        public CanalConsumeRegister Register<T>() where T : INotificationHandler<CanalBody>
        {
            var type = typeof(T);
            if (!ConsumeList.Contains(type))
                ConsumeList.Add(type);
            return this;
        }
    }
}
