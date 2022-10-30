namespace Bing.Serialization;

/// <summary>
/// 对象序列化器元接口【异步】
/// </summary>
public interface ISerializerAsync
{
    /// <summary>
    /// 异步序列化
    /// </summary>
    /// <typeparam name="TValue">序列化对象类型</typeparam>
    /// <param name="value">值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>内存流</returns>
    Task<MemoryStream> ToStreamAsync<TValue>(TValue value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步序列化
    /// </summary>
    /// <param name="type">序列化对象类型</param>
    /// <param name="value">值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>内存流</returns>
    Task<MemoryStream> ToStreamAsync(Type type, object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步反序列化
    /// </summary>
    /// <typeparam name="T">被反序列化对象类型</typeparam>
    /// <param name="stream">流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化对象</returns>
    Task<T> FromStreamAsync<T>(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步反序列化
    /// </summary>
    /// <param name="type">被反序列化对象类型</param>
    /// <param name="stream">流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化对象</returns>
    Task<object> FromStreamAsync(Type type, Stream stream, CancellationToken cancellationToken = default);
}
