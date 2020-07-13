using System;
using System.Collections.Generic;

namespace Bing.Caching
{
    /// <summary>
    /// 基于本地内存的单类型缓存基类
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public abstract class SingleTypeLocalMemoryBase<TKey, TValue> : ISingleTypeCache<TKey, TValue>
    {
        /// <summary>
        /// 缓存键数量
        /// </summary>
        public int Count => GetCache().Count;

        /// <summary>
        /// 根据键获取值
        /// </summary>
        /// <param name="key">键</param>
        public virtual TValue Get(TKey key) => Exists(key) ? GetCache()[key] : default;

        /// <summary>
        /// 清空
        /// </summary>
        public virtual void Clear()
        {
            lock (GetSyncCache())
                GetCache().Clear();
        }

        /// <summary>
        /// 读取
        /// </summary>
        public virtual IDictionary<TKey, TValue> Reader() => GetCache();

        /// <summary>
        /// 添加。如果存在则不添加，返回false
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public virtual bool Add(TKey key, TValue value)
        {
            if (Exists(key))
                return false;
            lock (GetSyncCache())
            {
                try
                {
                    GetCache().Add(key, value);
                }
                catch (ArgumentException)
                {
                    // 忽略添加相同键的异常，为了预防密集的线程
                }
                return true;
            }
        }

        /// <summary>
        /// 更新。如果不存在则不更新，返回false
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public virtual bool Update(TKey key, TValue value)
        {
            if (!Exists(key))
                return false;
            lock (GetSyncCache())
            {
                GetCache()[key] = value;
                return true;
            }
        }

        /// <summary>
        /// 设置。如果村则则更新，不存在则添加
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public virtual bool Set(TKey key, TValue value)
        {
            if (Exists(key))
                return Update(key, value);
            return Add(key, value);
        }

        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="key">键</param>
        public virtual bool Remove(TKey key)
        {
            if (!Exists(key))
                return false;
            lock (GetSyncCache())
                return GetCache().Remove(key);
        }

        /// <summary>
        /// 移除集合
        /// </summary>
        /// <param name="keys">键数组</param>
        public virtual bool Remove(TKey[] keys)
        {
            foreach (var key in keys)
                Remove(key);
            return true;
        }

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="key">键</param>
        public virtual bool Exists(TKey key) => GetCache().ContainsKey(key);

        /// <summary>
        /// 获取缓存
        /// </summary>
        protected abstract IDictionary<TKey, TValue> GetCache();

        /// <summary>
        /// 获取同步的缓存对象。用于线程安全
        /// </summary>
        protected abstract object GetSyncCache();
    }
}
