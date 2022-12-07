using System.Runtime.Serialization;
using Bing.ExceptionHandling;
using Bing.Logging;
using Microsoft.Extensions.Logging;

namespace Bing;

/// <summary>
/// 业务异常
/// </summary>
[Serializable]
public class BusinessException : Exception, IBusinessException, IHasErrorCode, IHasErrorDetails, IHasLogLevel
{
    /// <summary>
    /// 错误码
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// 错误详情
    /// </summary>
    public string Details { get; set; }

    /// <summary>
    /// 日志级别
    /// </summary>
    public LogLevel LogLevel { get; set; }

    /// <summary>
    /// 初始化一个<see cref="BusinessException"/>类型的实例
    /// </summary>
    /// <param name="code">错误码</param>
    /// <param name="message">错误消息</param>
    /// <param name="details">错误详情</param>
    /// <param name="innerException">内部异常</param>
    /// <param name="logLevel">日志级别</param>
    public BusinessException(
        string code,
        string message,
        string details = null,
        Exception innerException = null,
        LogLevel logLevel = LogLevel.Warning)
        : base(message, innerException)
    {
        Code = code;
        Details = details;
        LogLevel = logLevel;
    }

    /// <summary>
    /// 初始化一个<see cref="BusinessException"/>类型的实例
    /// </summary>
    /// <param name="serializationInfo">序列化信息</param>
    /// <param name="context">流上下文</param>
    public BusinessException(SerializationInfo serializationInfo, StreamingContext context)
        : base(serializationInfo, context)
    {
    }

    /// <summary>
    /// 设置数据
    /// </summary>
    /// <param name="name">键名</param>
    /// <param name="value">值</param>
    public BusinessException WithData(string name, object value)
    {
        Data[name] = value;
        return this;
    }
}
