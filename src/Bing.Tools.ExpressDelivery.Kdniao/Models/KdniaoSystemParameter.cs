using Bing.Tools.ExpressDelivery.Kdniao.Enums;

namespace Bing.Tools.ExpressDelivery.Kdniao.Models
{
    /// <summary>
    /// 快递鸟 系统级参数
    /// </summary>
    public abstract class KdniaoSystemParameter
    {
        /// <summary>
        /// 请求内容
        /// </summary>
        public string RequestData { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public string EBusinessId { get; set; }

        /// <summary>
        /// 请求类型
        /// </summary>
        public KdniaoRequestType RequestType { get; set; }

        /// <summary>
        /// 数据签名
        /// </summary>
        public string DataSign { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public string DataType { get; set; }
    }
}
