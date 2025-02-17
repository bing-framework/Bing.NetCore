using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.AspNetCore.Mvc.Filters;

/// <summary>
/// 验证实体过滤器
/// </summary>
public class ValidationModelAttribute : IActionFilter
{
    /// <summary>
    /// 是否允许参数为空，默认为 false。
    /// </summary>
    public bool AllowNulls { get; set; } = false;

    /// <summary>
    /// 是否允许多个错误结果，默认为 true
    /// </summary>
    public bool AllowMultipleResult { get; set; } = true;

    /// <summary>
    /// 操作执行前进行参数验证
    /// </summary>
    /// <param name="context">操作执行上下文</param>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // 检查是否允许参数为空
        if (!AllowNulls)
        {
            var nullArguments = context.ActionArguments
                .Where(arg => arg.Value == null)
                .Select(arg => new ValidationError
                {
                    Name = arg.Key,
                    Message = "参数不能为空"
                }).ToList();

            if (nullArguments.Any())
            {
                context.Result = CreateValidationFailedResult(nullArguments);
                return;
            }
        }

        // 如果 ModelState 不是有效的，则收集所有错误信息
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(e => e.Value.Errors.Count > 0)
                .SelectMany(e => e.Value.Errors.Select(err => new ValidationError
                {
                    Name = e.Key,
                    Message = err.ErrorMessage
                }))
                .ToList();

            context.Result = CreateValidationFailedResult(errors);
        }
    }

    /// <summary>
    /// 操作执行后，当前实现不执行任何操作
    /// </summary>
    /// <param name="context">操作执行完成的上下文</param>
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    /// <summary>
    /// 创建验证失败的响应结果
    /// </summary>
    /// <param name="errors">错误信息列表</param>
    /// <returns>封装后的 <see cref="ValidationFailedResult"/> 响应结果</returns>
    private IActionResult CreateValidationFailedResult(List<ValidationError> errors)
    {
        return new ValidationFailedResult(errors) { AllowMultipleResult = AllowMultipleResult };
    }
}
