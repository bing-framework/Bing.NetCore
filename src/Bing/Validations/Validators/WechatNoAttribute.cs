using Bing.Extensions;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Bing.Validations.Validators;

// ReSharper disable once CheckNamespace
namespace System.ComponentModel.DataAnnotations
{
    /// <summary>
    /// 微信号验证
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class WechatNoAttribute : ValidationAttribute
    {
        /// <summary>
        /// 错误消息
        /// </summary>
        private const string ErrorMsg = "'{0}' 不是有效的微信号";

        /// <summary>
        /// 格式化错误消息
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            if (ErrorMessage == null && ErrorMessageResourceName == null)
                ErrorMessage = ErrorMsg;
            return base.FormatErrorMessage(name);
        }

        /// <summary>
        /// 是否验证通过
        /// </summary>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value.SafeString().IsEmpty())
                return ValidationResult.Success;
            if (Regexs.IsMatch(value.SafeString(), ValidatePattern.WechatNoPatter))
                return ValidationResult.Success;
            return new ValidationResult(FormatErrorMessage(string.IsNullOrWhiteSpace(validationContext.DisplayName)
                ? validationContext.MemberName
                : validationContext.DisplayName));
        }
    }
}
