using System;

namespace Bing.Caching
{
    /// <summary>
    /// 缓存序列化器
    /// </summary>
    public interface ICacheSerializer
    {
        /// <summary>
        /// 序列化
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="value">值</param>
        /// <returns></returns>
        byte[] Serialize<T>(T value);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="bytes">字节数组</param>
        /// <returns></returns>
        T Deserialize<T>(byte[] bytes);

        /// <summary>
        /// 序列化对象
        /// </summary>
        /// <param name="obj">对象</param>
        /// <returns></returns>
        ArraySegment<byte> SerializeObject(object obj);

        /// <summary>
        /// 反序列化对象
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        object DeserializeObject(ArraySegment<byte> value);
    }
}
