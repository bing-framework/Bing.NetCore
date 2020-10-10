using System;
using System.IO;
using System.Threading.Tasks;

namespace Bing.Serialization
{
    /// <summary>
    /// 对象序列化器元接口
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// 序列化
        /// </summary>
        /// <typeparam name="T">序列化对象类型</typeparam>
        /// <param name="o">被序列化对象</param>
        /// <returns>序列化流</returns>
        Stream SerializeToStream<T>(T o);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T">被反序列化对象类型</typeparam>
        /// <param name="stream">流</param>
        /// <returns>反序列化对象</returns>
        T DeserializeFromStream<T>(Stream stream);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="type">被反序列化对象类型</param>
        /// <returns>反序列化对象</returns>
        object DeserializeFromStream(Stream stream, Type type);

        /// <summary>
        /// 序列化
        /// </summary>
        /// <typeparam name="T">序列化对象类型</typeparam>
        /// <param name="o">被序列化对象</param>
        /// <returns>序列化流</returns>
        Task<Stream> SerializeToStreamAsync<T>(T o);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T">被反序列化对象类型</typeparam>
        /// <param name="stream">流</param>
        /// <returns>反序列化对象</returns>
        Task<T> DeserializeFromStreamAsync<T>(Stream stream);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="type">被反序列化对象类型</param>
        /// <returns>反序列化对象</returns>
        Task<object> DeserializeFromStreamAsync(Stream stream, Type type);
    }
}
