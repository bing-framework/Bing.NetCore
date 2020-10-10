using System;
using System.Threading.Tasks;

namespace Bing.Serialization
{
    /// <summary>
    /// 对象序列化器
    /// </summary>
    /// <typeparam name="TSerializedType">目标序列化类型</typeparam>
    public interface IObjectSerializer<TSerializedType> : ISerializer
    {
        /// <summary>
        /// 序列化
        /// </summary>
        /// <typeparam name="T">序列化对象类型</typeparam>
        /// <param name="o">被序列化对象</param>
        /// <returns>序列化结果</returns>
        TSerializedType Serialize<T>(T o);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T">被序列化对象类型</typeparam>
        /// <param name="data">被反序列化对象</param>
        /// <returns>反序列化结果</returns>
        T Deserialize<T>(TSerializedType data);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="data">被反序列化对象</param>
        /// <param name="type">被序列化对象类型</param>
        /// <returns>反序列化结果</returns>
        object Deserialize(TSerializedType data, Type type);

        /// <summary>
        /// 序列化
        /// </summary>
        /// <typeparam name="T">序列化对象类型</typeparam>
        /// <param name="o">被序列化对象</param>
        /// <returns>序列化结果</returns>
        Task<TSerializedType> SerializeAsync<T>(T o);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T">被序列化对象类型</typeparam>
        /// <param name="data">被反序列化对象</param>
        /// <returns>反序列化结果</returns>
        Task<T> DeserializeAsync<T>(TSerializedType data);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="data">被反序列化对象</param>
        /// <param name="type">被序列化对象类型</param>
        /// <returns>反序列化结果</returns>
        Task<object> DeserializeAsync(TSerializedType data, Type type);
    }
}
