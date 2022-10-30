using System;
using Bing.Biz.Payments.Wechatpay.Enums;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Validation;

namespace Bing.Biz.Payments.Wechatpay.Parameters.Requests;

/// <summary>
/// 微信支付下载对账单接口参数
/// </summary>
public class WechatpayDownloadBillRequest : IVerifyModel
{
    /// <summary>
    /// 对账单日期
    /// </summary>
    public DateTime? BillDate { get; set; }

    /// <summary>
    /// 账单类型
    /// </summary>
    public WechatpayBillType BillType { get; set; } = WechatpayBillType.All;

    /// <summary>
    /// 验证
    /// </summary>
    public IValidationResult Validate()
    {
        if (BillDate == null)
            throw new Warning("必须设置账单日期");
        return ValidationResultCollection.Success;
    }

    /// <summary>
    /// 获取账单日期
    /// </summary>
    public string GetBillDate()
    {
        return BillDate.SafeValue().ToString("yyyyMMdd");
    }
}