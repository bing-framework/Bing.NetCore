using Bing.Payments.Gateways;

namespace Bing.Payments.Events
{
    /// <summary>
    /// 未知通知事件参数
    /// </summary>
    public class UnKnownNotifyEventArgs:NotifyEventArgs
    {
        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 初始化一个<see cref="UnKnownNotifyEventArgs"/>类型的实例
        /// </summary>
        /// <param name="gateway">网关</param>
        public UnKnownNotifyEventArgs(GatewayBase gateway) : base(gateway)
        {
        }
    }
}
