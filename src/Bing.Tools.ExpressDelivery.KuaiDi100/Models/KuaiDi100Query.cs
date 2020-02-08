using Bing.Tools.ExpressDelivery.Exceptions;
using Bing.Tools.ExpressDelivery.KuaiDi100.Core;
using Newtonsoft.Json;

namespace Bing.Tools.ExpressDelivery.KuaiDi100.Models
{
    /// <summary>
    /// 快递100 实时查询
    /// </summary>
    public class KuaiDi100Query
    {
        /// <summary>
        /// 快递公司编码
        /// </summary>
        [JsonProperty("com")]
        public string CompanyCode { get; set; }

        /// <summary>
        /// 快递单号
        /// </summary>
        [JsonProperty("num")]
        public string Sheet { get; set; }

        /// <summary>
        /// 出发地城市
        /// </summary>
        [JsonProperty("from")]
        public string From { get; set; }

        /// <summary>
        /// 目的地城市，到达目的地会加大监控频率
        /// </summary>
        [JsonProperty("to")]
        public string To { get; set; }

        /// <summary>
        /// 检查参数
        /// </summary>
        public void CheckParameters()
        {
            if (string.IsNullOrWhiteSpace(CompanyCode))
            {
                throw new InvalidArgumentException("快递公司编码为空", Constants.ServiceName, 401);
            }
            if (string.IsNullOrWhiteSpace(Sheet))
            {
                throw new InvalidArgumentException("快递单号为空", Constants.ServiceName, 401);
            }
        }
    }
}
