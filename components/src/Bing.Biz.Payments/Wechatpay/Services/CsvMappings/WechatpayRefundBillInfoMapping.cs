﻿using Bing.Biz.Payments.Wechatpay.Results;
using TinyCsvParser.Mapping;

namespace Bing.Biz.Payments.Wechatpay.Services.CsvMappings;

/// <summary>
/// 微信支付退款对账单信息Csv映射
/// </summary>
internal class WechatpayRefundBillInfoMapping : CsvMapping<WechatpayBillInfo>
{
    /// <summary>
    /// 初始化一个<see cref="WechatpayRefundBillInfoMapping"/>类型的实例
    /// </summary>
    public WechatpayRefundBillInfoMapping()
    {
        MapProperty(0, t => t.TransactionTime, new RemovePrefixStringConvert());
        MapProperty(1, t => t.AppId, new RemovePrefixStringConvert());
        MapProperty(2, t => t.MerchantId, new RemovePrefixStringConvert());
        MapProperty(3, t => t.SubMerchantId, new RemovePrefixStringConvert());
        MapProperty(4, t => t.DeviceNumber, new RemovePrefixStringConvert());
        MapProperty(5, t => t.TradeId, new RemovePrefixStringConvert());
        MapProperty(6, t => t.OrderId, new RemovePrefixStringConvert());
        MapProperty(7, t => t.OpenId, new RemovePrefixStringConvert());
        MapProperty(8, t => t.TradeType, new RemovePrefixStringConvert());
        MapProperty(9, t => t.TradeStatus, new RemovePrefixStringConvert());
        MapProperty(10, t => t.Bank, new RemovePrefixStringConvert());
        MapProperty(11, t => t.CurrencyType, new RemovePrefixStringConvert());
        MapProperty(12, t => t.TotalAmount, new RemovePrefixDecimalConvert());
        MapProperty(13, t => t.CouponAmount, new RemovePrefixDecimalConvert());
        MapProperty(14, t => t.RefundApplyTime, new RemovePrefixStringConvert());
        MapProperty(15, t => t.RefundTime, new RemovePrefixStringConvert());
        MapProperty(16, t => t.WechatpayRefundId, new RemovePrefixStringConvert());
        MapProperty(17, t => t.MerchantRefundId, new RemovePrefixStringConvert());
        MapProperty(18, t => t.RefundAmount, new RemovePrefixDecimalConvert());
        MapProperty(19, t => t.CouponRefundAmount, new RemovePrefixDecimalConvert());
        MapProperty(20, t => t.RefundType, new RemovePrefixStringConvert());
        MapProperty(21, t => t.RefundStatus, new RemovePrefixStringConvert());
        MapProperty(22, t => t.TradeName, new RemovePrefixStringConvert());
        MapProperty(23, t => t.MerchantAttach, new RemovePrefixStringConvert());
        MapProperty(24, t => t.Commission, new RemovePrefixDecimalConvert());
        MapProperty(25, t => t.Rate, new RemovePrefixStringConvert());
        MapProperty(26, t => t.OrderAmount, new RemovePrefixDecimalConvert());
        MapProperty(27, t => t.ApplyRefundAmount, new RemovePrefixDecimalConvert());
        MapProperty(28, t => t.RateRemark, new RemovePrefixStringConvert());
    }
}