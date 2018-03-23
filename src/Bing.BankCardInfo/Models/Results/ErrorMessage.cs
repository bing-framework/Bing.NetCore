using Newtonsoft.Json;

namespace Bing.BankCardInfo.Models.Results
{
    /// <summary>
    /// 错误消息
    /// </summary>
    internal class ErrorMessage
    {
        /// <summary>
        /// 错误码
        /// </summary>
        [JsonProperty("errorCodes")]
        public string ErrorCodes { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
