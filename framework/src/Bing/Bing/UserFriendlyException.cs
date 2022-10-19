using System;
using System.Runtime.Serialization;
using Microsoft.Extensions.Logging;

namespace Bing
{
    /// <summary>
    /// 用户友好异常
    /// </summary>
    [Serializable]
    public class UserFriendlyException : BusinessException, IUserFriendlyException
    {
        /// <summary>
        /// 初始化一个<see cref="UserFriendlyException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="code">错误码</param>
        /// <param name="details">错误详情</param>
        /// <param name="innerException">内部异常</param>
        /// <param name="logLevel">日志级别</param>
        public UserFriendlyException(
            string message,
            string code = null,
            string details = null,
            Exception innerException = null,
            LogLevel logLevel = LogLevel.Warning)
            : base(code, message, details, innerException, logLevel)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="UserFriendlyException"/>类型的实例
        /// </summary>
        /// <param name="serializationInfo">序列化信息</param>
        /// <param name="context">流上下文</param>
        public UserFriendlyException(SerializationInfo serializationInfo, StreamingContext context)
            : base(serializationInfo, context)
        {
        }
    }
}
