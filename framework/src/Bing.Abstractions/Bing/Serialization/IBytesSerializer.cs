namespace Bing.Serialization;

/// <summary>
/// 字节数组对象序列化器元接口
/// </summary>
public interface IBytesSerializer : IStreamSerializer
{
    /// <summary>
    /// 将字节数组反序列化为对象
    /// </summary>
    /// <typeparam name="TValue">对象类型</typeparam>
    /// <param name="bytes">字节数组</param>
    /// <returns>如果 bytes 为 null 或为空，将返回 TValue 的默认值</returns>
    TValue FromBytes<TValue>(byte[] bytes);

    /// <summary>
    /// 将字节数组反序列化为对象
    /// </summary>
    /// <param name="type">对象类型</param>
    /// <param name="bytes">字节数组</param>
    /// <returns>如果 bytes 为 null 或为空，将返回 type 的默认值</returns>
    object FromBytes(Type type, byte[] bytes);

    /// <summary>
    /// 序列化为字节数组
    /// </summary>
    /// <typeparam name="TValue">序列化对象类型</typeparam>
    /// <param name="value">值</param>
    byte[] ToBytes<TValue>(TValue value);

    /// <summary>
    /// 序列化为字节数组
    /// </summary>
    /// <param name="type">序列化对象类型</param>
    /// <param name="value">值</param>
    byte[] ToBytes(Type type, object value);
}
