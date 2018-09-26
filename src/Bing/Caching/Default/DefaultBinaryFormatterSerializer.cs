using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Bing.Caching.Default
{
    /// <summary>
    /// 默认二进制格式的缓存序列化器
    /// </summary>
    public class DefaultBinaryFormatterSerializer:ICacheSerializer
    {
        /// <summary>
        /// 序列化
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="value">值</param>
        /// <returns></returns>
        public byte[] Serialize<T>(T value)
        {
            using (var ms=new MemoryStream())
            {
                new BinaryFormatter().Serialize(ms, value);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="bytes">字节数组</param>
        /// <returns></returns>
        public T Deserialize<T>(byte[] bytes)
        {
            using (var ms=new MemoryStream(bytes))
            {
                return (T) (new BinaryFormatter().Deserialize(ms));
            }
        }

        /// <summary>
        /// 序列化对象
        /// </summary>
        /// <param name="obj">对象</param>
        /// <returns></returns>
        public ArraySegment<byte> SerializeObject(object obj)
        {
            using (var ms=new MemoryStream())
            {
                new BinaryFormatter().Serialize(ms,obj);
                return new ArraySegment<byte>(ms.GetBuffer(), 0, (int) ms.Length);
            }
        }

        /// <summary>
        /// 反序列化对象
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public object DeserializeObject(ArraySegment<byte> value)
        {
            using (var ms = new MemoryStream(value.Array, value.Offset, value.Count))
            {
                return new BinaryFormatter().Deserialize(ms);
            }
        }
    }
}
