using System.Collections.Generic;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Extensions;
using Bing.Extensions;
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
            if (json.IsEmpty())
            {
                return;
            }

            var jObject = JObject.Parse(json);
            foreach (var token in jObject.Children())
            {
                AddNodes(token);
            }
        }

        /// <summary>
        /// 添加节点
        /// </summary>
        /// <param name="token">token节点</param>
        private void AddNodes(JToken token)
        {
            if (!(token is JProperty item))
            {
                return;
            }

            foreach (var value in item.Value)
            {
                AddNodes(value);
            }

            if (GetIgnoreItems().Contains(item.Name))
            {
                return;
            }
            _result.Add(item.Name,item.Value.SafeString());
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

        /// <summary>
        /// 获取状态码
        /// </summary>
        /// <returns></returns>
        public string GetCode()
        {
            return GetValue("code");
        }

        /// <summary>
        /// 获取消息
        /// </summary>
        /// <returns></returns>
        public string GetMessage()
        {
            return GetValue("msg");
        }

        /// <summary>
        /// 获取支付订单号
        /// </summary>
        /// <returns></returns>
        public string GetTradeNo()
        {
            return GetValue(AlipayConst.TradeNo);
        }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success => GetCode() == "10000";
    }
}
