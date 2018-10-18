using Bing.Payments.Gateways;

namespace Bing.Payments.Events
{
    /// <summary>
    /// 撤销成功网关事件参数
    /// </summary>
    public class CancelSucceedEventArgs:NotifyEventArgs
    {
        /// <summary>
        /// 初始化一个<see cref="CancelSucceedEventArgs"/>类型的实例
        /// </summary>
        /// <param name="gateway">网关</param>
        public CancelSucceedEventArgs(GatewayBase gateway) : base(gateway)
        {
        }
    }
}
