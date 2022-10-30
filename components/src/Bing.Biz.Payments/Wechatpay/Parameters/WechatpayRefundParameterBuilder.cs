using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Biz.Payments.Wechatpay.Parameters;

/// <summary>
/// 微信支付退款参数生成器
/// </summary>
public class WechatpayRefundParameterBuilder : WechatpayParameterBuilder
{
    /// <summary>
    /// 初始化一个<see cref="WechatpayRefundParameterBuilder"/>类型的实例
    /// </summary>
    /// <param name="config">微信支付配置</param>
    public WechatpayRefundParameterBuilder(WechatpayConfig config) : base(config)
    {
    }

    /// <summary>
    /// 初始化退款参数
    /// </summary>
    /// <param name="param">退款参数</param>
    public void Init(WechatRefundRequest param)
    {
        param.CheckNull(nameof(param));
        RefundFee(param.RefundFee)
            .Description(param.RefundDescription)
            .RefundId(param.RefundId)
            .TransactionId(param.TransactionId)
            .NotifyUrl(param.NotifyUrl)
            .OutTradeNo(param.OrderId)
            .TotalFee(param.Money);
    }

    /// <summary>
    /// 设置退款金额
    /// </summary>
    /// <param name="refundFee">退款金额。单位：元</param>
    public WechatpayRefundParameterBuilder RefundFee(decimal refundFee)
    {
        Add("refund_fee", Conv.ToInt(refundFee * 100));
        return this;
    }

    /// <summary>
    /// 设置微信订单号
    /// </summary>
    /// <param name="transactionId">微信订单号</param>
    public WechatpayRefundParameterBuilder TransactionId(string transactionId)
    {
        Add("transaction_id", transactionId);
        return this;
    }

    /// <summary>
    /// 设置商户退款单号
    /// </summary>
    /// <param name="refundId">商户退款单号</param>
    public WechatpayRefundParameterBuilder RefundId(string refundId)
    {
        Add("out_refund_no", refundId);
        return this;
    }

    /// <summary>
    /// 设置退款原因
    /// </summary>
    /// <param name="description">退款原因</param>
    public WechatpayRefundParameterBuilder Description(string description)
    {
        Add("refund_desc", description);
        return this;
    }
}