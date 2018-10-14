using Bing.Tools.ExpressDelivery.Exceptions;
using Bing.Tools.ExpressDelivery.Kdniao.Core;

namespace Bing.Tools.ExpressDelivery.Kdniao.Models
{
    /// <summary>
    /// 快递鸟 即时跟踪
    /// </summary>
    public class KdniaoTrackQuery
    {
        /// <summary>
        /// 快递公司编码
        /// </summary>
        public string ShipperCode { get; set; }

        /// <summary>
        /// 快递单号
        /// </summary>
        public string LogisticCode { get; set; }

        /// <summary>
        /// 订单编号
        /// </summary>
        public string OrderCode { get; set; }

        /// <summary>
        /// 是否处理信息
        /// </summary>
        public string IsHandleInfo { get; set; } = "0";

        /// <summary>
        /// 检查参数
        /// </summary>
        public void CheckParameters()
        {
            if (string.IsNullOrWhiteSpace(ShipperCode))
            {
                throw new InvalidArgumentException("快递公司编码为空", Constants.ServiceName, 401);
            }
            if (string.IsNullOrWhiteSpace(LogisticCode))
            {
                throw new InvalidArgumentException("快递单号为空", Constants.ServiceName, 401);
            }
        }
    }
}
