using System;
using Enyim.Caching.Memcached;

namespace Bing.Caching.Memcached
{
    /// <summary>
    /// 默认 Memcached 转码器
    /// </summary>
    public class DefaultMemcachedTranscoder:DefaultTranscoder
    {
        /// <summary>
        /// 缓存序列化器
        /// </summary>
        private readonly ICacheSerializer _serializer;

        /// <summary>
        /// 初始化一个<see cref="DefaultMemcachedTranscoder"/>类型的实例
        /// </summary>
        /// <param name="serializer">缓存序列化器</param>
        public DefaultMemcachedTranscoder(ICacheSerializer serializer)
        {
            _serializer = serializer;
        }

        /// <summary>
        /// 序列化对象
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        protected override ArraySegment<byte> SerializeObject(object value)
        {
            return _serializer.SerializeObject(value);
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="item">缓存项</param>
        /// <returns></returns>
        public override T Deserialize<T>(CacheItem item)
        {
            return (T)base.Deserialize<T>(item);
        }

        /// <summary>
        /// 反序列化对象
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        protected override object DeserializeObject(ArraySegment<byte> value)
        {
            return _serializer.DeserializeObject(value);
        }
    }
}
