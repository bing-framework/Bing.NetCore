using System.Runtime.Serialization;
using Bing.ExceptionHandling;

namespace Bing.Http.Clients;

/// <summary>
/// 远程调用异常
/// </summary>
[Serializable]
public class BingRemoteCallException : BingException, IHasErrorDetails, IHasHttpStatusCode
{
    /// <inheritdoc />
    public override string Code => Error?.Code;

    /// <inheritdoc />
    public string Details => Error?.Details;

    /// <inheritdoc />
    public int HttpStatusCode { get; set; }

    /// <summary>
    /// 远程服务错误信息
    /// </summary>
    public RemoteServiceErrorInfo Error { get; set; }

    /// <summary>
    /// 初始化一个<see cref="BingRemoteCallException"/>类型的实例
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public BingRemoteCallException(string message, Exception innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="BingRemoteCallException"/>类型的实例
    /// </summary>
    /// <param name="serializationInfo">序列化信息</param>
    /// <param name="context">流上下文</param>
    public BingRemoteCallException(SerializationInfo serializationInfo, StreamingContext context)
        : base(serializationInfo, context)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="BingRemoteCallException"/>类型的实例
    /// </summary>
    /// <param name="error">远程服务错误信息</param>
    /// <param name="innerException">内部异常</param>
    public BingRemoteCallException(RemoteServiceErrorInfo error, Exception innerException = null)
        : base(error.Message, innerException)
    {
        Error = error;
        if (error.Data == null)
            return;
        foreach (var dataKey in error.Data.Keys)
            Data[dataKey] = error.Data[dataKey];
    }
}
