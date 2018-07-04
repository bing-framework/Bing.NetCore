using Bing.Exceptions;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Microsoft.Extensions.Configuration;

namespace Bing.Caching
{
    /// <summary>
    /// 缓存键转换器
    /// </summary>
    public static class CacheKeyConverter
    {
        /// <summary>
        /// 缓存区域
        /// </summary>
        private static string _region;

        /// <summary>
        /// 获取缓存区域
        /// </summary>
        /// <returns></returns>
        public static string GetRegion()
        {
            return _region;
        }

        /// <summary>
        /// 设置缓存区域
        /// </summary>
        /// <param name="configuration">配置</param>
        public static void SetRegion(IConfiguration configuration)
        {
            if (!_region.IsEmpty())
            {
                return;
            }

            string actual = configuration.GetValue<string>("Cache:Region");
            if (actual.IsEmpty())
            {
                actual = configuration.GetValue<string>("AppName");
            }

            if (actual.IsEmpty())
            {
                throw new ConfigException("Cache:Region Or AppName");
            }

            _region = actual;
        }

        /// <summary>
        /// 获取转换后的缓存键
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns></returns>
        public static string GetKeyWithRegion(string key)
        {
            Check.NotNullOrEmpty(key, nameof(key));
            if (_region.IsEmpty())
            {
                return key;
            }

            return $"{_region}:{key}";
        }
    }
}
