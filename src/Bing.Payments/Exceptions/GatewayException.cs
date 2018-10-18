using System;

namespace Bing.Payments.Exceptions
{
    /// <summary>
    /// 网关异常
    /// </summary>
    public class GatewayException:Exception
    {
        /// <summary>
        /// 初始化一个<see cref="GatewayException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        public GatewayException(string message) : base(message) { }
    }
}
