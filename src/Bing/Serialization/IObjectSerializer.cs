namespace Bing.Serialization
{
    /// <summary>
    /// 对象序列化器
    /// </summary>
    public interface IObjectSerializer
    {
        /// <summary>
        /// 序列化
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">对象</param>
        /// <returns></returns>
        byte[] Serialize<T>(T obj);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="bytes">字节数组</param>
        /// <returns></returns>
        T Deserialize<T>(byte[] bytes);
    }

    /// <summary>
    /// 对象序列化器
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public interface IObjectSerializer<T>
    {
        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="obj">对象</param>
        /// <returns></returns>
        byte[] Serialize(T obj);

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns></returns>
        T Deserialize(byte[] bytes);
    }
}
