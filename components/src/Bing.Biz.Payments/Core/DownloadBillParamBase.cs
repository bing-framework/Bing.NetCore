using System;
using System.Linq;
using Bing.Exceptions;
using Bing.ObjectMapping;
using Bing.Validation;

namespace Bing.Biz.Payments.Core;

/// <summary>
/// 下载账单参数基类
/// </summary>
public class DownloadBillParamBase : IVerifyModel
{
    /// <summary>
    /// 账单日期
    /// </summary>
    public DateTime BillDate { get; set; }

    /// <summary>
    /// 验证
    /// </summary>
    public virtual IValidationResult Validate()
    {
        var result = DataAnnotationValidation.Validate(this);
        if (result.IsValid)
            return ValidationResultCollection.Success;
        throw new Warning(result.First().ErrorMessage);
    }

    /// <summary>
    /// 转换为下载账单参数
    /// </summary>
    public virtual DownloadBillParam ToParam() => this.MapTo<DownloadBillParam>();
}