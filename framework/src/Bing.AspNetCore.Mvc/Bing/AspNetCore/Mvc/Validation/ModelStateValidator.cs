using System.ComponentModel.DataAnnotations;
using Bing.Validations;
using Bing.Validations.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bing.AspNetCore.Mvc.Validation
{
    /// <summary>
    /// 实体状态验证器
    /// </summary>
    public class ModelStateValidator : IModelStateValidator
    {
        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="modelState">实体状态字典</param>
        public virtual void Validate(ModelStateDictionary modelState)
        {
            var validationResult = new ValidationResultCollection();
            AddErrors(validationResult, modelState);
            if(!validationResult.IsValid)
                return;
        }

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="validationResult">验证结果</param>
        /// <param name="modelState">实体状态字典</param>
        public virtual void AddErrors(IValidationResult validationResult, ModelStateDictionary modelState)
        {
            if(modelState.IsValid)
                return;
            foreach (var state in modelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    validationResult.Add(new ValidationResult(error.ErrorMessage, new[] {state.Key}));
                }
            }
        }
    }
}
