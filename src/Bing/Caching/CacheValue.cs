namespace Bing.Caching
{
    /// <summary>
    /// 缓存值
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class CacheValue<T>
    {
        /// <summary>
        /// 是否有值
        /// </summary>
        public bool HasValue { get; }

        /// <summary>
        /// 缓存值
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsNull => Value == null;

        /// <summary>
        /// 空值
        /// </summary>
        public static CacheValue<T> Null { get; } = new CacheValue<T>(default(T), true);

        /// <summary>
        /// 无值
        /// </summary>
        public static CacheValue<T> NoValue { get; } = new CacheValue<T>(default(T), false);

        /// <summary>
        /// 初始化一个<see cref="CacheValue{T}"/>类型的实例
        /// </summary>
        /// <param name="value">缓存值</param>
        /// <param name="hasValue">是否有值</param>
        public CacheValue(T value, bool hasValue)
        {
            Value = value;
            HasValue = hasValue;
        }

        /// <summary>
        /// 转换成字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Value?.ToString() ?? "<NULL>";
        }
    }
}
