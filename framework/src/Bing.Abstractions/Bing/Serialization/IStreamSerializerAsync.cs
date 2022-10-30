namespace Bing.Serialization;

/// <summary>
/// 流对象序列化器元接口
/// </summary>
public interface IStreamSerializerAsync : ISerializerAsync
{
    /// <summary>
    /// 序列化对象并打包到流中
    /// </summary>
    /// <typeparam name="TValue">序列化对象类型</typeparam>
    /// <param name="value">值</param>
    /// <param name="stream">流</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PackAsync<TValue>(TValue value, Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// 序列化对象并打包到流中
    /// </summary>
    /// <param name="type">序列化对象类型</param>
    /// <param name="value">值</param>
    /// <param name="stream">流</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PackAsync(Type type, object value, Stream stream, CancellationToken cancellationToken = default);
}
