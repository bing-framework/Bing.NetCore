using Bing.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bing.AspNetCore.Mvc.Validation
{
    /// <summary>
    /// 实体状态验证器
    /// </summary>
    public interface IModelStateValidator
    {
        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="modelState">实体状态字典</param>
        void Validate(ModelStateDictionary modelState);

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="validationResult">验证结果</param>
        /// <param name="modelState">实体状态字典</param>
        void AddErrors(IValidationResult validationResult, ModelStateDictionary modelState);
    }
}
