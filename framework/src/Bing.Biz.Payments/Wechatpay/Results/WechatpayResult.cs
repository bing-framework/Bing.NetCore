using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Signatures;
using Bing.Extensions;
using Bing.Logs;
using Bing.Utils.Parameters;
using Bing.Validations;
using Xml = Bing.Helpers.Xml;

namespace Bing.Biz.Payments.Wechatpay.Results
{
    /// <summary>
    /// 微信支付结果
    /// </summary>
    public class WechatpayResult
    {
        /// <summary>
        /// 配置提供者
        /// </summary>
        private readonly IWechatpayConfigProvider _configProvider;

        /// <summary>
        /// 响应结果
        /// </summary>
        private readonly ParameterBuilder _builder;

        /// <summary>
        /// 签名
        /// </summary>
        private string _sign;

        /// <summary>
        /// 微信支付原始响应
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// 初始化一个<see cref="WechatpayResult"/>类型的实例
        /// </summary>
        /// <param name="configProvider">配置提供器</param>
        /// <param name="response">xml响应消息</param>
        public WechatpayResult(IWechatpayConfigProvider configProvider, string response)
        {
            configProvider.CheckNotNull(nameof(configProvider));
            _configProvider = configProvider;
            Raw = response;
            _builder = new ParameterBuilder();
            Resolve(response);
        }

        /// <summary>
        /// 解析响应
        /// </summary>
        /// <param name="response">xml响应消息</param>
        private void Resolve(string response)
        {
            if (response.IsEmpty())
            {
                return;
            }

            var elements = Xml.ToElements(response);
            elements.ForEach(node =>
            {
                if (node.Name == WechatpayConst.Sign)
                {
                    _sign = node.Value;
                    return;
                }

                _builder.Add(node.Name.LocalName, node.Value);
            });
            WriteLog();
        }

        /// <summary>
        /// 写日志
        /// </summary>
        protected void WriteLog()
        {
            var log = GetLog();
            if (log.IsTraceEnabled == false)
            {
                log.Class(GetType().FullName)
                    .Content("微信支付返回")
                    .Content("参数:")
                    .Content(GetParams())
                    .Content()
                    .Content("原始响应")
                    .Content(Raw)
                    .Trace();
            }
        }

        /// <summary>
        /// 获取日志操作
        /// </summary>
        /// <returns></returns>
        private ILog GetLog()
        {
            try
            {
                return Log.GetLog(WechatpayConst.TraceLogName);
            }
            catch
            {
                return Log.Null;
            }
        }

        /// <summary>
        /// 获取参数
        /// </summary>
        /// <param name="name">xml节点名称</param>
        /// <returns></returns>
        public string GetParam(string name)
        {
            return _builder.GetValue(name).SafeString();
        }

        /// <summary>
        /// 获取返回状态码
        /// </summary>
        /// <returns></returns>
        public string GetReturnCode()
        {
            return GetParam(WechatpayConst.ReturnCode);
        }

        /// <summary>
        /// 获取业务结果代码
        /// </summary>
        /// <returns></returns>
        public string GetResultCode()
        {
            return GetParam(WechatpayConst.ReturnCode);
        }

        /// <summary>
        /// 获取返回消息
        /// </summary>
        /// <returns></returns>
        public string GetReturnMessage()
        {
            return GetParam(WechatpayConst.ReturnMessage);
        }

        /// <summary>
        /// 获取应用标识
        /// </summary>
        /// <returns></returns>
        public string AppId()
        {
            return GetParam(WechatpayConst.AppId);
        }

        /// <summary>
        /// 获取商户号
        /// </summary>
        /// <returns></returns>
        public string GetMerchantId()
        {
            return GetParam(WechatpayConst.MerchantId);
        }

        /// <summary>
        /// 获取随机字符串
        /// </summary>
        /// <returns></returns>
        public string GetNonce()
        {
            return GetParam("nonce_str");
        }

        /// <summary>
        /// 获取预支付标识
        /// </summary>
        /// <returns></returns>
        public string GetPrepayId()
        {
            return GetParam("prepay_id");
        }

        /// <summary>
        /// 获取二维码链接
        /// </summary>
        /// <returns></returns>
        public string GetCodeUrl()
        {
            return GetParam("code_url");
        }

        /// <summary>
        /// 获取支付跳转链接
        /// </summary>
        /// <returns></returns>
        public string GetMWebUrl()
        {
            return GetParam("mweb_url");
        }

        /// <summary>
        /// 获取交易类型
        /// </summary>
        /// <returns></returns>
        public string GetTradeType()
        {
            return GetParam(WechatpayConst.TradeType);
        }

        /// <summary>
        /// 获取错误码
        /// </summary>
        /// <returns></returns>
        public string GetErrorCode()
        {
            return GetParam(WechatpayConst.ErrorCode);
        }

        /// <summary>
        /// 获取错误码和描述
        /// </summary>
        /// <returns></returns>
        public string GetErrorCodeDesciption()
        {
            return GetParam(WechatpayConst.ErrorCodeDescription);
        }

        /// <summary>
        /// 获取签名
        /// </summary>
        /// <returns></returns>
        public string GetSign()
        {
            return _sign;
        }

        /// <summary>
        /// 获取参数列表
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, string> GetParams()
        {
            var builder = new ParameterBuilder(_builder);
            builder.Add(WechatpayConst.Sign, _sign);
            return builder.GetDictionary().ToDictionary(t => t.Key, t => t.Value.SafeString());
        }

        /// <summary>
        /// 验证
        /// </summary>
        /// <returns></returns>
        public async Task<ValidationResultCollection> ValidateAsync()
        {
            if (GetReturnCode() != WechatpayConst.Success || GetResultCode() != WechatpayConst.Success)
            {
                return new ValidationResultCollection(GetErrorCodeDesciption());
            }

            var isValid = await VerifySign();
            if (isValid == false)
            {
                return new ValidationResultCollection("签名失败");
            }
            return ValidationResultCollection.Success;
        }

        /// <summary>
        /// 验证签名
        /// </summary>
        /// <returns></returns>
        public async Task<bool> VerifySign()
        {
            var config = await _configProvider.GetConfigAsync(_builder);
            return SignManagerFactory.Create(config, _builder).Verify(GetSign());
        }
    }
}
