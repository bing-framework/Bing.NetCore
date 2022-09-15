using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters;
using Bing.Biz.Payments.Wechatpay.Signatures;
using Bing.Extensions;
using Bing.Logs;
using Bing.Utils.Parameters;
using Bing.Validation;
using Xml = Bing.Helpers.Xml;

namespace Bing.Biz.Payments.Wechatpay.Results
{
    /// <summary>
    /// 微信支付结果
    /// </summary>
    public class WechatpayResult
    {
        /// <summary>
        /// 响应结果
        /// </summary>
        private readonly ParameterBuilder _builder;

        /// <summary>
        /// 签名
        /// </summary>
        private string _sign;

        /// <summary>
        /// 初始化一个<see cref="WechatpayResult"/>类型的实例
        /// </summary>
        /// <param name="configProvider">微信支付配置提供程序</param>
        /// <param name="response">xml响应消息</param>
        /// <param name="config">微信支付配置</param>
        /// <param name="parameterBuilder">微信支付参数生成器</param>
        public WechatpayResult(IWechatpayConfigProvider configProvider, string response, WechatpayConfig config = null, IWechatpayParameterBuilder parameterBuilder = null)
        {
            configProvider.CheckNull(nameof(configProvider));
            ConfigProvider = configProvider;
            Raw = response;
            Config = config;
            Builder = parameterBuilder;
            _builder = new ParameterBuilder();
            Resolve(response);
        }

        /// <summary>
        /// 微信支付原始响应
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// 微信支付配置提供程序
        /// </summary>
        public IWechatpayConfigProvider ConfigProvider { get; }

        /// <summary>
        /// 配置
        /// </summary>
        public WechatpayConfig Config { get; }

        /// <summary>
        /// 微信支付参数生成器
        /// </summary>
        public IWechatpayParameterBuilder Builder { get; }

        /// <summary>
        /// 解析响应
        /// </summary>
        /// <param name="response">xml响应消息</param>
        private void Resolve(string response)
        {
            if (response.IsEmpty())
                return;
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
        private ILog GetLog() => Log.GetLog(WechatpayConst.TraceLogName);

        /// <summary>
        /// 获取参数
        /// </summary>
        /// <param name="name">xml节点名称</param>
        public string GetParam(string name) => _builder.GetValue(name).SafeString();

        /// <summary>
        /// 获取返回状态码
        /// </summary>
        public string GetReturnCode() => GetParam(WechatpayConst.ReturnCode);

        /// <summary>
        /// 获取业务结果代码
        /// </summary>
        public string GetResultCode() => GetParam(WechatpayConst.ResultCode);

        /// <summary>
        /// 获取返回消息
        /// </summary>
        public string GetReturnMessage() => GetParam(WechatpayConst.ReturnMessage);

        /// <summary>
        /// 获取应用标识
        /// </summary>
        public string GetAppId() => GetParam(WechatpayConst.AppId);

        /// <summary>
        /// 获取商户号
        /// </summary>
        public string GetMerchantId() => GetParam(WechatpayConst.MerchantId);

        /// <summary>
        /// 获取随机字符串
        /// </summary>
        public string GetNonce() => GetParam("nonce_str");

        /// <summary>
        /// 获取预支付标识
        /// </summary>
        public string GetPrepayId() => GetParam("prepay_id");

        /// <summary>
        /// 获取二维码链接
        /// </summary>
        public string GetCodeUrl() => GetParam("code_url");

        /// <summary>
        /// 获取支付跳转链接
        /// </summary>
        public string GetMWebUrl() => GetParam("mweb_url");

        /// <summary>
        /// 获取微信退款单号
        /// </summary>
        public string GetRefundId() => GetParam("refund_id");

        /// <summary>
        /// 获取交易类型
        /// </summary>
        public string GetTradeType() => GetParam(WechatpayConst.TradeType);

        /// <summary>
        /// 获取错误码
        /// </summary>
        public string GetErrorCode()
        {
            var result = GetParam(WechatpayConst.ErrorCode);
            if (string.IsNullOrWhiteSpace(result) == false)
                return result;
            return GetParam("error_code");
        }

        /// <summary>
        /// 获取错误码和描述
        /// </summary>
        public string GetErrorCodeDescription()
        {
            var result = GetParam(WechatpayConst.ErrorCodeDescription);
            if (string.IsNullOrWhiteSpace(result) == false)
                return result;
            result = GetParam("return_msg");
            if (string.IsNullOrWhiteSpace(result) == false)
                return result;
            return GetErrorCode();
        }

        /// <summary>
        /// 获取签名
        /// </summary>
        public string GetSign() => _sign;

        /// <summary>
        /// 获取参数列表
        /// </summary>
        public IDictionary<string, string> GetParams()
        {
            var builder = new ParameterBuilder(_builder);
            builder.Add(WechatpayConst.Sign, _sign);
            return builder.GetDictionary().ToDictionary(t => t.Key, t => t.Value.SafeString());
        }

        /// <summary>
        /// 验证
        /// </summary>
        public async Task<ValidationResultCollection> ValidateAsync()
        {
            if (GetReturnCode() != WechatpayConst.Success || GetResultCode() != WechatpayConst.Success)
                return new ValidationResultCollection(GetErrorCodeDescription());
            var isValid = await VerifySign();
            if (isValid == false)
                return new ValidationResultCollection("签名失败");
            return ValidationResultCollection.Success;
        }

        /// <summary>
        /// 验证签名
        /// </summary>
        public async Task<bool> VerifySign()
        {
            var config = Config ?? await ConfigProvider.GetConfigAsync(_builder);
            return SignManagerFactory.Create(config, _builder).Verify(GetSign());
        }
    }
}
