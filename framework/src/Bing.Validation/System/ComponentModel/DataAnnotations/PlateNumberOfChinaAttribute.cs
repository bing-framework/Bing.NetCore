using Bing.Extensions;
using Bing.Helpers;
using Bing.Validations.Validators;

// ReSharper disable once CheckNamespace
namespace System.ComponentModel.DataAnnotations;

/// <summary>
/// 车牌号验证
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class PlateNumberOfChinaAttribute : ValidationAttribute
{
    /// <summary>
    /// 错误消息
    /// </summary>
    private const string ErrorMsg = "'{0}' 不是有效车牌号";

    /// <inheritdoc />
    public override string FormatErrorMessage(string name)
    {
        if (ErrorMessage == null && ErrorMessageResourceName == null)
            ErrorMessage = ErrorMsg;
        return base.FormatErrorMessage(name);
    }

    /// <inheritdoc />
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value.SafeString().IsEmpty())
            return ValidationResult.Success;
        if (Regexs.IsMatch(value.SafeString(), ValidatePattern.PlateNumberOfChinaPatter))
            return ValidationResult.Success;
        return new ValidationResult(FormatErrorMessage(string.IsNullOrWhiteSpace(validationContext.DisplayName)
            ? validationContext.MemberName
            : validationContext.DisplayName));
    }
}
