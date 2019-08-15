using System;
using System.Collections.Generic;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 单例操作。提供一个字典容器，按类型装载所有<see cref="Singleton"/>的单例实例
    /// </summary>
    public class Singleton
    {
        /// <summary>
        /// 单例对象字典
        /// </summary>
        public static IDictionary<Type, object> AllSingletons { get; }

        /// <summary>
        /// 静态构造函数
        /// </summary>
        static Singleton()
        {
            if (AllSingletons == null)
            {
                AllSingletons = new Dictionary<Type, object>();
            }
        }
    }

    /// <summary>
    /// 单例对象操作。定义一个指定类型的单例，该实例的声明周期将跟随整个应用程序
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public class Singleton<T> : Singleton
    {
        /// <summary>
        /// 实例
        /// </summary>
        private static T _instance;

        /// <summary>
        /// 获取指定类型的单例实例
        /// </summary>
        public static T Instance
        {
            get => _instance;
            set
            {
                _instance = value;
                AllSingletons[typeof(T)] = value;
            }
        }
    }
}
