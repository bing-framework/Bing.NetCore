using Bing.Payments.Gateways;

namespace Bing.Payments.Events
{
    /// <summary>
    /// 支付成功网关事件参数
    /// </summary>
    public class PaySucceedEventArgs:NotifyEventArgs
    {
        /// <summary>
        /// 初始化一个<see cref="PaySucceedEventArgs"/>类型的实例
        /// </summary>
        /// <param name="gateway">网关</param>
        public PaySucceedEventArgs(GatewayBase gateway) : base(gateway)
        {
        }
    }
}
