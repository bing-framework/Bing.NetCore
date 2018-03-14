using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Bing.Validations
{
    /// <summary>
    /// 验证规则
    /// </summary>
    public interface IValidationRule
    {
        /// <summary>
        /// 验证
        /// </summary>
        /// <returns></returns>
        ValidationResult Validate();
    }
}
