using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Biz.Payments.Wechatpay.Parameters
{
    /// <summary>
    /// 微信支付参数生成器
    /// </summary>
    public class WechatpayParameterBuilder : WechatpayParameterBuilderBase<WechatpayParameterBuilder>, IWechatpayParameterBuilder<WechatpayParameterBuilder>
    {
        /// <summary>
        /// 初始化一个<see cref="WechatpayParameterBuilder"/>类型的实例
        /// </summary>
        /// <param name="config">微信支付配置</param>
        public WechatpayParameterBuilder(WechatpayConfig config) : base(config)
        {
        }

        /// <summary>
        /// 初始化 
        /// </summary>
        public void Init()
        {
            this.AppId(Config.AppId)
                .MerchantId(Config.MerchantId)
                .SignType(Config.SignType.Description())
                .NonceStr();
        }

        /// <summary>
        /// 初始化支付参数
        /// </summary>
        /// <param name="param">支付参数</param>
        public void Init(PayParam param)
        {
            param.CheckNull(nameof(param));
            param.Init();
            SpbillCreateIp(Web.IP)
                .Body(param.Subject)
                .OutTradeNo(param.OrderId)
                .TotalFee(param.Money)
                .NotifyUrl(param.NotifyUrl)
                .Attach(param.Attach);
        }

        /// <summary>
        /// 设置标题
        /// </summary>
        /// <param name="body">标题</param>
        public WechatpayParameterBuilder Body(string body)
        {
            Add(WechatpayConst.Body, body);
            return this;
        }

        /// <summary>
        /// 设置商户订单号
        /// </summary>
        /// <param name="orderId">商户订单号</param>
        public WechatpayParameterBuilder OutTradeNo(string orderId)
        {
            Add(WechatpayConst.OutTradeNo, orderId);
            return this;
        }

        /// <summary>
        /// 设置货币类型
        /// </summary>
        /// <param name="feeType">货币类型</param>
        public WechatpayParameterBuilder FeeType(string feeType)
        {
            Add(WechatpayConst.FeeType, feeType);
            return this;
        }

        /// <summary>
        /// 设置总金额
        /// </summary>
        /// <param name="totalFee">总金额。单位：元</param>
        public WechatpayParameterBuilder TotalFee(decimal totalFee)
        {
            Add(WechatpayConst.TotalFee, Conv.ToInt(totalFee * 100));
            return this;
        }

        /// <summary>
        /// 设置回调通知地址
        /// </summary>
        /// <param name="notifyUrl">回调通知地址</param>
        public WechatpayParameterBuilder NotifyUrl(string notifyUrl)
        {
            Add(WechatpayConst.NotifyUrl, GetNotifyUrl(notifyUrl));
            return this;
        }

        /// <summary>
        /// 获取回调通知地址
        /// </summary>
        /// <param name="notifyUrl">回调通知地址</param>
        private string GetNotifyUrl(string notifyUrl) => notifyUrl.IsEmpty() ? Config.NotifyUrl : notifyUrl;

        /// <summary>
        /// 设置终端IP
        /// </summary>
        /// <param name="ip">终端IP</param>
        public WechatpayParameterBuilder SpbillCreateIp(string ip)
        {
            Add(WechatpayConst.SpbillCreateIp, ip);
            return this;
        }

        /// <summary>
        /// 设置交易类型
        /// </summary>
        /// <param name="type">交易类型</param>
        public WechatpayParameterBuilder TradeType(string type)
        {
            Add(WechatpayConst.TradeType, type);
            return this;
        }

        /// <summary>
        /// 设置伙伴标识
        /// </summary>
        /// <param name="partnerId">伙伴标识</param>
        public WechatpayParameterBuilder PartnerId(string partnerId)
        {
            Add(WechatpayConst.PartnerId, partnerId);
            return this;
        }

        /// <summary>
        /// 设置时间戳
        /// </summary>
        /// <param name="timestamp">时间戳</param>
        public WechatpayParameterBuilder Timestamp(long timestamp = 0)
        {
            Add(WechatpayConst.Timestamp, timestamp == 0 ? Time.GetUnixTimestamp() : timestamp);
            return this;
        }

        /// <summary>
        /// 设置包
        /// </summary>
        /// <param name="package">包。默认值："Sign=WXPay"</param>
        public WechatpayParameterBuilder Package(string package = "Sign=WXPay")
        {
            Add(WechatpayConst.Package, package);
            return this;
        }

        /// <summary>
        /// 设置附加数据
        /// </summary>
        /// <param name="attach">附加数据</param>
        public WechatpayParameterBuilder Attach(string attach)
        {
            Add(WechatpayConst.Attach, attach);
            return this;
        }

        /// <summary>
        /// 设置用户标识
        /// </summary>
        /// <param name="openId">用户标识</param>
        public WechatpayParameterBuilder OpenId(string openId)
        {
            Add(WechatpayConst.OpenId, openId);
            return this;
        }

        /// <summary>
        /// 设置用户授权码
        /// </summary>
        /// <param name="code">用户授权码</param>
        public WechatpayParameterBuilder AuthCode(string code)
        {
            Add(WechatpayConst.AuthCode, code);
            return this;
        }

    }
}
