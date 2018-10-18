using Bing.Payments.Gateways;

namespace Bing.Payments.Events
{
    /// <summary>
    /// 未知网关事件参数
    /// </summary>
    public class UnknownGatewayEventArgs:NotifyEventArgs
    {
        /// <summary>
        /// 初始化一个<see cref="UnknownGatewayEventArgs"/>类型的实例
        /// </summary>
        /// <param name="gateway">网关</param>
        public UnknownGatewayEventArgs(GatewayBase gateway) : base(gateway)
        {
        }
    }
}
