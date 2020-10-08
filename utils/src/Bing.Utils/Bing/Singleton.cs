using System;
using System.Collections.Generic;

namespace Bing
{
    /// <summary>
    /// 单例
    /// </summary>
    public class Singleton
    {
        /// <summary>
        /// 所有单例对象
        /// </summary>
        public static IDictionary<Type, object> AllSingletons { get; }

        /// <summary>
        /// 初始化一个<see cref="Singleton"/>类型的静态实例
        /// </summary>
        static Singleton() => AllSingletons = new Dictionary<Type, object>();
    }

    /// <summary>
    /// 单例
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    public class Singleton<T> : Singleton
    {
        /// <summary>
        /// 实例
        /// </summary>
        private static T _instance;

        /// <summary>
        /// 实例
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

    /// <summary>
    /// 单例列表
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    public class SingletonList<T> : Singleton<IList<T>>
    {
        /// <summary>
        /// 实例
        /// </summary>
        public new static IList<T> Instance => Singleton<IList<T>>.Instance;

        /// <summary>
        /// 初始化一个<see cref="SingletonList{T}"/>类型的静态实例
        /// </summary>
        static SingletonList() => Singleton<IList<T>>.Instance = new List<T>();
    }

    /// <summary>
    /// 单例字典
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public class SingletonDictionary<TKey, TValue> : Singleton<IDictionary<TKey, TValue>>
    {
        /// <summary>
        /// 实例
        /// </summary>
        public new static IDictionary<TKey, TValue> Instance => Singleton<Dictionary<TKey, TValue>>.Instance;

        /// <summary>
        /// 初始化一个<see cref="SingletonDictionary{TKey,TValue}"/>类型的静态实例
        /// </summary>
        static SingletonDictionary() => Singleton<Dictionary<TKey, TValue>>.Instance = new Dictionary<TKey, TValue>();
    }
}
