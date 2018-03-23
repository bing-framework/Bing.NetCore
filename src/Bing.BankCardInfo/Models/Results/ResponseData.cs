using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Bing.BankCardInfo.Models.Results
{
    internal class ResponseData
    {
        /// <summary>
        /// 验证结果
        /// </summary>
        [JsonProperty("validated")]
        public bool Validated { get; set; }

        /// <summary>
        /// 银行卡号
        /// </summary>
        [JsonProperty("key")]
        public string Key { get; set; }

        /// <summary>
        /// 请求状态
        /// </summary>
        [JsonProperty("stat")]
        public string Stat { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        [JsonProperty("messages")]
        public List<ErrorMessage> Messages { get; set; }

        /// <summary>
        /// 银行名称缩写
        /// </summary>
        [JsonProperty("bank")]
        public string Bank { get; set; }

        /// <summary>
        /// 银行卡类型
        /// </summary>
        [JsonProperty("cardType")]
        public string CardType { get; set; }

        /// <summary>
        /// 转换成Json字符串
        /// </summary>
        /// <returns></returns>
        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
