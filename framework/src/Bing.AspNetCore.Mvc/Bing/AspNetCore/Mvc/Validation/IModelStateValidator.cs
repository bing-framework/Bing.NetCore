using Bing.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bing.AspNetCore.Mvc.Validation;

/// <summary>
/// 定义将 MVC 模型状态错误转换为框架验证结果的契约。
/// </summary>
public interface IModelStateValidator
{
    /// <summary>
    /// 验证指定的 MVC 模型状态并收集其中的错误。
    /// </summary>
    /// <param name="modelState">MVC 模型状态字典。</param>
    void Validate(ModelStateDictionary modelState);

    /// <summary>
    /// 将模型状态中的错误添加到指定验证结果集合。
    /// </summary>
    /// <param name="validationResult">用于接收验证错误的结果集合。</param>
    /// <param name="modelState">MVC 模型状态字典。</param>
    void AddErrors(IValidationResult validationResult, ModelStateDictionary modelState);
}