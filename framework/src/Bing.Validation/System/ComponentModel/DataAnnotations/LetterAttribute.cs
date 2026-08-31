using Bing.Extensions;
using Bing.Helpers;
using Bing.Validations.Validators;

// ReSharper disable once CheckNamespace
namespace System.ComponentModel.DataAnnotations;

/// <summary>
/// 英文字母验证
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class LetterAttribute : ValidationAttribute
{
    /// <summary>
    /// 错误消息
    /// </summary>
    private const string ErrorMsg = "'{0}' 必须是英文字母";

    /// <summary>
    /// 格式化错误消息
    /// </summary>
    /// <param name="name">发生验证错误的成员名称。</param>
    /// <returns>格式化后的错误消息。</returns>
    public override string FormatErrorMessage(string name)
    {
        if (ErrorMessage == null && ErrorMessageResourceName == null)
            ErrorMessage = ErrorMsg;
        return base.FormatErrorMessage(name);
    }

    /// <summary>
    /// 是否验证通过
    /// </summary>
    /// <param name="value">待验证的值。</param>
    /// <param name="validationContext">验证上下文。</param>
    /// <returns>验证通过时返回 <see cref="ValidationResult.Success"/>；验证失败时返回包含错误消息的验证结果。</returns>
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value.SafeString().IsEmpty())
            return ValidationResult.Success;
        if (Regexs.IsMatch(value.SafeString(), ValidatePattern.LetterPattern))
            return ValidationResult.Success;
        return new ValidationResult(FormatErrorMessage(string.IsNullOrWhiteSpace(validationContext.DisplayName)
            ? validationContext.MemberName
            : validationContext.DisplayName));
    }
}
