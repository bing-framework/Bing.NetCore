using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bing.Validation
{
    /// <summary>
    /// 验证错误
    /// </summary>
    public interface IHasValidationErrors
    {
        /// <summary>
        /// 验证错误集合
        /// </summary>
        IList<ValidationResult> ValidationErrors { get; }
    }
}
