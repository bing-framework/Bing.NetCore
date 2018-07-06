using System;
using System.Collections.Generic;
using System.Text;
using Bing.Logs;

namespace Bing.Caching.Hybrid
{
    /// <summary>
    /// 混合缓存管理器
    /// </summary>
    public class DefaultHybridCacheManager:IHybridCacheManager
    {
        /// <summary>
        /// 混合缓存提供程序列表
        /// </summary>
        private readonly List<IHybridCacheProvider> _providers;

        /// <summary>
        /// 日志
        /// </summary>
        private readonly ILog _log;

        /// <summary>
        /// 是否已经回收
        /// </summary>
        private bool _disposed;


        public T GetOrAdd<T>(string key, Func<T> getData, TimeSpan? expiry = null)
        {
            throw new NotImplementedException();
        }

        public T Get<T>(string key)
        {
            throw new NotImplementedException();
        }

        public bool Set<T>(string key, T value, TimeSpan? expiry = null)
        {
            throw new NotImplementedException();
        }

        public bool Delete(string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 回收资源
        /// </summary>
        /// <param name="disposing">是否回收中</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (var provider in _providers)
                {
                    if (provider is IDisposable providerWithDisposable)
                    {
                        providerWithDisposable.Dispose();
                    }
                }
            }

            _disposed = true;
        }
    }
}
