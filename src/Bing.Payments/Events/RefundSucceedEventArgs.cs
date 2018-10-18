using Bing.Payments.Gateways;

namespace Bing.Payments.Events
{
    /// <summary>
    /// 退款成功网关事件参数
    /// </summary>
    public class RefundSucceedEventArgs:NotifyEventArgs
    {
        /// <summary>
        /// 初始化一个<see cref="RefundSucceedEventArgs"/>类型的实例
        /// </summary>
        /// <param name="gateway">网关</param>
        public RefundSucceedEventArgs(GatewayBase gateway) : base(gateway)
        {
        }
    }
}
