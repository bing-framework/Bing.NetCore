using System;

namespace Bing.Tools.ExpressDelivery.Exceptions
{
    /// <summary>
    /// 无效参数异常
    /// </summary>
    public class InvalidArgumentException : Exception, IExpressDeliveryException
    {
        /// <summary>
        /// 服务名
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// 错误码
        /// </summary>
        public int ErrorCode { get; }

        /// <summary>
        /// 初始化一个<see cref="InvalidArgumentException"/>类型的实例
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="serviceName">服务名</param>
        /// <param name="errorCode">错误码</param>
        public InvalidArgumentException(string message, string serviceName, int errorCode) : base(message)
        {
            ErrorCode = errorCode;
            ServiceName = serviceName;
        }

        /// <summary>
        /// 输出异常消息
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"code:{ErrorCode},message:{Message}";
    }
}
