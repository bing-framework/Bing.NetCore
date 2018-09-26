using System;

namespace Bing.Caching.Aspects
{
    /// <summary>
    /// 本地缓存 属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class LocalCacheAttribute:Attribute
    {
        /// <summary>
        /// 缓存过期时间，单位：秒，如果为0则表示永久缓存
        /// </summary>
        public int Expire { get; set; }

        /// <summary>
        /// 是否只缓存在本地
        /// </summary>
        public bool OnlyLocal { get; set; } = false;

        /// <summary>
        /// 动态获取缓存过期时间的表达式
        /// </summary>
        public string ExpireExpression { get; set; } = string.Empty;
    }
}
