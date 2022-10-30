namespace Bing.Serialization;

/// <summary>
/// 对象序列化器元接口
/// </summary>
public interface ISerializer
{
    /// <summary>
    /// 序列化
    /// </summary>
    /// <typeparam name="TValue">序列化对象类型</typeparam>
    /// <param name="value">值</param>
    /// <returns>序列化流</returns>
    /// <returns>内存流</returns>
    MemoryStream ToStream<TValue>(TValue value);

    /// <summary>
    /// 序列化
    /// </summary>
    /// <param name="type">序列化对象类型</param>
    /// <param name="value">值</param>
    /// <returns>内存流</returns>
    MemoryStream ToStream(Type type, object value);

    /// <summary>
    /// 反序列化
    /// </summary>
    /// <typeparam name="T">被反序列化对象类型</typeparam>
    /// <param name="stream">流</param>
    /// <returns>反序列化对象</returns>
    T Stream<T>(Stream stream);

    /// <summary>
    /// 反序列化
    /// </summary>
    /// <param name="type">被反序列化对象类型</param>
    /// <param name="stream">流</param>
    /// <returns>反序列化对象</returns>
    object FromStream(Type type, Stream stream);
}
