using System.ComponentModel.DataAnnotations;
using Bing.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bing.AspNetCore.Mvc.Validation;

/// <summary>
/// 将 MVC 模型状态错误转换为框架验证结果的默认实现。
/// </summary>
public class ModelStateValidator : IModelStateValidator
{
    /// <inheritdoc />
    public virtual void Validate(ModelStateDictionary modelState)
    {
        var validationResult = new ValidationResultCollection();
        AddErrors(validationResult, modelState);
        if(!validationResult.IsValid)
            return;
    }

    /// <inheritdoc />
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
