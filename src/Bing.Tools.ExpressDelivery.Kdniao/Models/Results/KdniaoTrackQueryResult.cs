using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bing.Tools.ExpressDelivery.Kdniao.Models.Results
{
    /// <summary>
    /// 快递鸟 即使查询结果
    /// </summary>
    public class KdniaoTrackQueryResult
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        [JsonProperty("EBusinessID")]
        public string EBusinessId { get; set; }

        /// <summary>
        /// 订单编号
        /// </summary>
        [JsonProperty("OrderCode")]
        public string OrderCode { get; set; }

        /// <summary>
        /// 快递公司编码
        /// </summary>
        [JsonProperty("ShipperCode")]
        public string ShipperCode { get; set; }

        /// <summary>
        /// 快递单号
        /// </summary>
        [JsonProperty("LogisticCode")]
        public string LogisticCode { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        [JsonProperty("Success")]
        public bool Success { get; set; }

        /// <summary>
        /// 失败原因
        /// </summary>
        [JsonProperty("Reason")]
        public string Reason { get; set; }

        /// <summary>
        /// 物流状态
        /// </summary>
        [JsonProperty("State")]
        public string State { get; set; }

        /// <summary>
        /// 跟踪轨迹
        /// </summary>
        [JsonProperty("Traces")]
        public List<KdniaoTrackItem> Traces { get; set; } = new List<KdniaoTrackItem>();

        /// <summary>
        /// 快递鸟 跟踪项
        /// </summary>
        public class KdniaoTrackItem
        {
            /// <summary>
            /// 轨迹发生时间
            /// </summary>
            [JsonProperty("AcceptTime")]
            public DateTime AcceptTime { get; set; }

            /// <summary>
            /// 轨迹描述
            /// </summary>
            [JsonProperty("AcceptStation")]
            public string AcceptStation { get; set; }

            /// <summary>
            /// 备注
            /// </summary>
            [JsonProperty("Remark")]
            public string Remark { get; set; }
        }

    }
}
