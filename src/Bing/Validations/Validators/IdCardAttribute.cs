using System.Globalization;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Properties;
using Bing.Extensions;
using Bing.Utils.Helpers;
using Bing.Validations.Validators;

// ReSharper disable once CheckNamespace
namespace System.ComponentModel.DataAnnotations
{
    /// <summary>
    /// 身份证验证
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IdCardAttribute: ValidationAttribute
    {
        /// <summary>
        /// 格式化错误消息
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            if (ErrorMessage == null && ErrorMessageResourceName == null)
                ErrorMessage = LibraryResource.InvalidIdCard;
            return string.Format(CultureInfo.CurrentCulture, ErrorMessageString);
        }

        /// <summary>
        /// 是否验证通过
        /// </summary>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value.SafeString().IsEmpty())
                return ValidationResult.Success;
            if (Regexs.IsMatch(value.SafeString(), ValidatePattern.IdCardPattern))
                return ValidationResult.Success;
            return new ValidationResult(FormatErrorMessage(string.IsNullOrWhiteSpace(validationContext.DisplayName)
                ? validationContext.MemberName
                : validationContext.DisplayName));
        }
    }
}
