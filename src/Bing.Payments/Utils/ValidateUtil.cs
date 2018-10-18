using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bing.Payments.Utils
{
    /// <summary>
    /// 验证工具类
    /// </summary>
    internal static class ValidateUtil
    {
        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="obj">验证目标</param>
        /// <param name="data">上下文数据</param>
        public static void Validate(object obj, Dictionary<object, object> data)
        {
            var validationContext = new ValidationContext(obj, data);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(obj, validationContext, results, true);

            if (!isValid)
            {
                throw new ArgumentNullException(results[0].ErrorMessage);
            }
        }
    }
}
