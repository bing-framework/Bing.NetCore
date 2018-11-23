using System.Collections.Generic;
using Bing.Utils.Extensions;
using Newtonsoft.Json.Linq;

namespace Bing.Biz.Payments.Alipay.Results
{
    /// <summary>
    /// 支付宝结果
    /// </summary>
    public class AlipayResult
    {
        /// <summary>
        /// 结果
        /// </summary>
        private readonly IDictionary<string, string> _result;

        /// <summary>
        /// 支付宝原始响应
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// 初始化一个<see cref="AlipayResult"/>类型的实例
        /// </summary>
        /// <param name="response">json响应消息</param>
        public AlipayResult(string response)
        {
            Raw = response;
            _result = new Dictionary<string, string>();
            LoadJson(response);
        }

        /// <summary>
        /// 加载json
        /// </summary>
        /// <param name="json">json响应消息</param>
        private void LoadJson(string json)
        {

        }

        /// <summary>
        /// 添加节点
        /// </summary>
        /// <param name="token">token节点</param>
        private void AddNodes(JToken token)
        {

        }

        /// <summary>
        /// 获取忽略项
        /// </summary>
        /// <returns></returns>
        private List<string> GetIgnoreItems()
        {
            return new List<string>()
            {
                "alipay_trade_pay_response"
            };
        }

        /// <summary>
        /// 获取字典
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, string> GetDictionary()
        {
            return _result;
        }

        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public string GetValue(string key)
        {
            if (key.IsEmpty())
            {
                return string.Empty;
            }

            return _result.ContainsKey(key) ? _result[key].SafeString() : string.Empty;
        }

        /// <summary>
        /// 是否包含指定键
        /// </summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public bool HasKey(string key)
        {
            if (key.IsEmpty())
            {
                return false;
            }

            return _result.ContainsKey(key);
        }
    }
}
