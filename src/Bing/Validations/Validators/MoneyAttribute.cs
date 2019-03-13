using Bing.Utils.Extensions;

// ReSharper disable once CheckNamespace
namespace System.ComponentModel.DataAnnotations
{
    /// <summary>
    /// 金额验证
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class MoneyAttribute : ValidationAttribute
    {
        /// <summary>
        /// 错误消息
        /// </summary>
        private const string ErrorMsg = "'{0}' 必须是大于 {1}，小于或等于 {2}";

        /// <summary>
        /// 最小值
        /// </summary>
        public decimal Min { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public decimal Max { get; set; }

        /// <summary>
        /// 初始化一个<see cref="MoneyAttribute"/>类型的实例
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public MoneyAttribute(decimal min, decimal max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>
        /// 格式化错误消息
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            if (ErrorMessage == null && ErrorMessageResourceName == null)
            {
                ErrorMessage = ErrorMsg;
            }

            return base.FormatErrorMessage(name);
        }

        /// <summary>
        /// 是否验证通过
        /// </summary>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value.SafeString().IsEmpty())
            {
                return ValidationResult.Success;
            }

            if (value is decimal numberValue && numberValue > Min && numberValue <= Max)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(FormatErrorMessage(string.Empty));
        }
    }
}
