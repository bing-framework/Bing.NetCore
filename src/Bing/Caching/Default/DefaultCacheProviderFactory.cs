using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Caching.Default
{
    /// <summary>
    /// 默认缓存提供程序工厂
    /// </summary>
    public class DefaultCacheProviderFactory:ICacheProiderFactory
    {
        /// <summary>
        /// 缓存提供程序列表
        /// </summary>
        private readonly IEnumerable<ICacheProvider> _cacheProviders;

        /// <summary>
        /// 初始化一个<see cref="DefaultCacheProviderFactory"/>类型的实例
        /// </summary>
        /// <param name="cacheProviders">缓存提供程序列表</param>
        public DefaultCacheProviderFactory(IEnumerable<ICacheProvider> cacheProviders)
        {
            _cacheProviders = cacheProviders;
        }

        /// <summary>
        /// 获取缓存提供程序
        /// </summary>
        /// <param name="name">缓存提供程序名称</param>
        /// <returns></returns>
        public ICacheProvider GetCacheProvider(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var provider = _cacheProviders.FirstOrDefault(x => x.Name.Equals(name));
            if (provider == null)
            {
                throw new ArgumentException("找不到匹配的缓存提供程序");
            }

            return provider;
        }
    }
}
