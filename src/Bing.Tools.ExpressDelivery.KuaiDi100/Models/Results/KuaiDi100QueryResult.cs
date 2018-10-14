using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bing.Tools.ExpressDelivery.KuaiDi100.Models.Results
{
    /// <summary>
    /// 快递100 实时查询结果
    /// </summary>
    public class KuaiDi100QueryResult
    {
        /// <summary>
        /// 消息
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; }

        /// <summary>
        /// 快递单当前签收状态。0:在途中,1:已揽收,2:疑难,3:已签收,4:拒签,5:同城派送中,6:退回,7:转单
        /// </summary>
        [JsonProperty("state")]
        public string State { get; set; }

        /// <summary>
        /// 通讯状态
        /// </summary>
        [JsonProperty("status")]
        public string Status { get; set; }

        /// <summary>
        /// 快递单明细状态标记
        /// </summary>
        [JsonProperty("condition")]
        public string Condition { get; set; }

        /// <summary>
        /// 是否签收标记
        /// </summary>
        [JsonProperty("ischeck")]
        public string IsCheck { get; set; }

        /// <summary>
        /// 快递公司编码
        /// </summary>
        [JsonProperty("com")]
        public string CompanyCode { get; set; }

        /// <summary>
        /// 快递单单号
        /// </summary>
        [JsonProperty("nu")]
        public string Sheet { get; set; }

        /// <summary>
        /// 查询结果
        /// </summary>
        [JsonProperty("data")]
        public List<KuaiDi100TrackItem> Datas { get; set; } = new List<KuaiDi100TrackItem>();

        /// <summary>
        /// 快递100 跟踪项
        /// </summary>
        public class KuaiDi100TrackItem
        {
            /// <summary>
            /// 内容
            /// </summary>
            [JsonProperty("context")]
            public string Context { get; set; }

            /// <summary>
            /// 时间
            /// </summary>
            [JsonProperty("time")]
            public DateTime Time { get; set; }

            /// <summary>
            /// 格式化后时间
            /// </summary>
            [JsonProperty("ftime")]
            public string FormatTime { get; set; }
        }
    }
}
