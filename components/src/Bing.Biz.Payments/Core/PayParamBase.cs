﻿using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bing.Biz.Payments.Properties;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.ObjectMapping;
using Bing.Validation;

namespace Bing.Biz.Payments.Core;

/// <summary>
/// 支付参数基类
/// </summary>
public class PayParamBase : IVerifyModel
{
    /// <summary>
    /// 订单标题
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// 商户订单号
    /// </summary>
    [Required(ErrorMessageResourceType = typeof(PayResource), ErrorMessageResourceName = "OrderIdIsEmpty")]
    public string OrderId { get; set; }

    /// <summary>
    /// 支付金额。单位：元
    /// </summary>
    public decimal Money { get; set; }

    /// <summary>
    /// 回调通知地址
    /// </summary>
    public string NotifyUrl { get; set; }

    /// <summary>
    /// 初始化
    /// </summary>
    public virtual void Init() => InitSubject();

    /// <summary>
    /// 初始化订单标题
    /// </summary>
    private void InitSubject()
    {
        if (Subject.IsEmpty()) 
            Subject = OrderId;
    }

    /// <summary>
    /// 验证
    /// </summary>
    public virtual IValidationResult Validate()
    {
        ValidateMoney();
        var result = DataAnnotationValidation.Validate(this);
        if (result.IsValid)
            return ValidationResultCollection.Success;
        throw new Warning(result.First().ErrorMessage);
    }

    /// <summary>
    /// 验证金额
    /// </summary>
    private void ValidateMoney()
    {
        if (Money <= 0)
            throw new Warning(PayResource.InvalidMoney);
    }

    /// <summary>
    /// 转换为支付参数
    /// </summary>
    public virtual PayParam ToParam() => this.MapTo<PayParam>();
}