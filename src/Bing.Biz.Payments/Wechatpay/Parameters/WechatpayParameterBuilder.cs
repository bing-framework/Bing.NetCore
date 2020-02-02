using System.Xml;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Signatures;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Bing.Utils.Parameters;
using Xml = Bing.Helpers.Xml;

namespace Bing.Biz.Payments.Wechatpay.Parameters
{
    /// <summary>
    /// 微信支付参数生成器
    /// </summary>
    public class WechatpayParameterBuilder
    {
        /// <summary>
        /// 参数生成器
        /// </summary>
        private readonly ParameterBuilder _builder;

        /// <summary>
        /// 微信支付配置
        /// </summary>
        public WechatpayConfig Config { get; }

        /// <summary>
        /// 初始化一个<see cref="WechatpayParameterBuilder"/>类型的实例
        /// </summary>
        /// <param name="config">微信支付配置</param>
        public WechatpayParameterBuilder(WechatpayConfig config)
        {
            config.CheckNotNull(nameof(config));
            Config = config;
            _builder = new ParameterBuilder();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="param">支付参数</param>
        public void Init(PayParam param)
        {
            param.CheckNotNull(nameof(param));
            param.Init();
            AppId(Config.AppId)
                .MerchantId(Config.MerchantId)
                .SignType(Config.SignType.Description())
                .Add("nonce_str", Id.Guid())
                .SpbillCreateIp(Web.IP)
                .Body(param.Subject)
                .OutTradeNo(param.OrderId)
                .TotalFee(param.Money)
                .NotifyUrl(param.NotifyUrl)
                .Attach(param.Attach)
                .OpenId(param.OpenId);
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="name">参数名</param>
        /// <param name="value">参数值</param>
        /// <returns></returns>
        public WechatpayParameterBuilder Add(string name, string value)
        {
            _builder.Add(name, value);
            return this;
        }

        /// <summary>
        /// 设置应用标识
        /// </summary>
        /// <param name="appId">应用标识</param>
        /// <returns></returns>
        public WechatpayParameterBuilder AppId(string appId)
        {
            _builder.Add(WechatpayConst.AppId, appId);
            return this;
        }

        /// <summary>
        /// 设置商户号
        /// </summary>
        /// <param name="merchantId">商户号</param>
        /// <returns></returns>
        public WechatpayParameterBuilder MerchantId(string merchantId)
        {
            _builder.Add(WechatpayConst.MerchantId, merchantId);
            return this;
        }

        /// <summary>
        /// 设置标题
        /// </summary>
        /// <param name="body">标题</param>
        /// <returns></returns>
        public WechatpayParameterBuilder Body(string body)
        {
            _builder.Add(WechatpayConst.Body, body);
            return this;
        }

        /// <summary>
        /// 设置商户订单号
        /// </summary>
        /// <param name="orderId">商户订单号</param>
        /// <returns></returns>
        public WechatpayParameterBuilder OutTradeNo(string orderId)
        {
            _builder.Add(WechatpayConst.OutTradeNo, orderId);
            return this;
        }

        /// <summary>
        /// 设置货币类型
        /// </summary>
        /// <param name="feeType">货币类型</param>
        /// <returns></returns>
        public WechatpayParameterBuilder FeeType(string feeType)
        {
            _builder.Add(WechatpayConst.FeeType, feeType);
            return this;
        }

        /// <summary>
        /// 设置总金额
        /// </summary>
        /// <param name="totalFee">总金额。单位：元</param>
        /// <returns></returns>
        public WechatpayParameterBuilder TotalFee(decimal totalFee)
        {
            _builder.Add(WechatpayConst.TotalFee, Conv.ToInt(totalFee * 100));
            return this;
        }

        /// <summary>
        /// 设置回调通知地址
        /// </summary>
        /// <param name="notifyUrl">回调通知地址</param>
        /// <returns></returns>
        public WechatpayParameterBuilder NotifyUrl(string notifyUrl)
        {
            _builder.Add(WechatpayConst.NotifyUrl, GetNotifyUrl(notifyUrl));
            return this;
        }

        /// <summary>
        /// 获取回调通知地址
        /// </summary>
        /// <param name="notifyUrl">回调通知地址</param>
        /// <returns></returns>
        private string GetNotifyUrl(string notifyUrl)
        {
            if (notifyUrl.IsEmpty())
            {
                return Config.NotifyUrl;
            }
            return notifyUrl;
        }

        /// <summary>
        /// 设置终端IP
        /// </summary>
        /// <param name="ip">终端IP</param>
        /// <returns></returns>
        public WechatpayParameterBuilder SpbillCreateIp(string ip)
        {
            _builder.Add(WechatpayConst.SpbillCreateIp, ip);
            return this;
        }

        /// <summary>
        /// 设置交易类型
        /// </summary>
        /// <param name="type">交易类型</param>
        /// <returns></returns>
        public WechatpayParameterBuilder TradeType(string type)
        {
            _builder.Add(WechatpayConst.TradeType, type);
            return this;
        }

        /// <summary>
        /// 设置签名类型
        /// </summary>
        /// <param name="type">签名类型</param>
        /// <returns></returns>
        public WechatpayParameterBuilder SignType(string type)
        {
            _builder.Add(WechatpayConst.SignType, type);
            return this;
        }

        /// <summary>
        /// 设置伙伴标识
        /// </summary>
        /// <param name="partnerId">伙伴标识</param>
        /// <returns></returns>
        public WechatpayParameterBuilder PartnerId(string partnerId)
        {
            _builder.Add(WechatpayConst.PartnerId, partnerId);
            return this;
        }

        /// <summary>
        /// 设置时间戳
        /// </summary>
        /// <param name="timestamp">时间戳</param>
        /// <returns></returns>
        public WechatpayParameterBuilder Timestamp(long timestamp = 0)
        {
            _builder.Add(WechatpayConst.Timestamp, timestamp == 0 ? Time.GetUnixTimestamp() : timestamp);
            return this;
        }

        /// <summary>
        /// 设置包
        /// </summary>
        /// <param name="package">包。默认值："Sign=WXPay"</param>
        /// <returns></returns>
        public WechatpayParameterBuilder Package(string package = "Sign=WXPay")
        {
            _builder.Add(WechatpayConst.Package, package);
            return this;
        }

        /// <summary>
        /// 设置附加数据
        /// </summary>
        /// <param name="attach">附加数据</param>
        /// <returns></returns>
        public WechatpayParameterBuilder Attach(string attach)
        {
            _builder.Add(WechatpayConst.Attach, attach);
            return this;
        }

        /// <summary>
        /// 设置用户标识
        /// </summary>
        /// <param name="openId">用户标识</param>
        /// <returns></returns>
        public WechatpayParameterBuilder OpenId(string openId)
        {
            _builder.Add(WechatpayConst.OpenId, openId);
            return this;
        }

        /// <summary>
        /// 设置用户授权码
        /// </summary>
        /// <param name="code">用户授权码</param>
        /// <returns></returns>
        public WechatpayParameterBuilder AuthCode(string code)
        {
            _builder.Add(WechatpayConst.AuthCode, code);
            return this;
        }

        /// <summary>
        /// 获取Xml结果，包含签名
        /// </summary>
        /// <returns></returns>
        public string ToXml()
        {
            return ToXmlDocument(GetSignBuilder()).OuterXml;
        }

        /// <summary>
        /// 获取Xml文档
        /// </summary>
        /// <param name="builder">参数生成器</param>
        /// <returns></returns>
        private XmlDocument ToXmlDocument(ParameterBuilder builder)
        {
            var xml = new Xml();
            foreach (var param in builder.GetDictionary())
            {
                AddNode(xml, param.Key, param.Value);
            }
            return xml.Document;
        }

        /// <summary>
        /// 添加Xml节点
        /// </summary>
        /// <param name="xml">Xml操作</param>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        private void AddNode(Xml xml, string key, object value)
        {
            if (key.SafeString().ToLower() == WechatpayConst.TotalFee)
            {
                xml.AddNode(key, value);
                return;
            }

            xml.AddCDataNode(value, key);
        }

        /// <summary>
        /// 获取签名的参数生成器
        /// </summary>
        /// <returns></returns>
        private ParameterBuilder GetSignBuilder()
        {
            var builder = new ParameterBuilder(_builder);
            builder.Add(WechatpayConst.Sign, GetSign());
            return builder;
        }

        /// <summary>
        /// 获取签名
        /// </summary>
        /// <returns></returns>
        public string GetSign()
        {
            return SignManagerFactory.Create(Config, _builder).Sign();
        }

        /// <summary>
        /// 获取Xml结果，不包含签名
        /// </summary>
        /// <returns></returns>
        public string ToXmlNoContainsSign()
        {
            return ToXmlDocument(_builder).OuterXml;
        }

        /// <summary>
        /// 获取Json结果，包含签名
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return GetSignBuilder().ToJson();
        }

        /// <summary>
        /// 输出结果
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ToXml();
        }
    }

}
