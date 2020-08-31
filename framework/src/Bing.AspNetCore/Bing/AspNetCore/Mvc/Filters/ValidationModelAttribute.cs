using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// 验证实体过滤器
    /// </summary>
    public class ValidationModelAttribute : IActionFilter
    {
        /// <summary>
        /// 允许空值
        /// </summary>
        public bool AllowNulls { get; set; }

        /// <summary>
        /// 允许多个结果
        /// </summary>
        public bool AllowMultipleResult { get; set; } = true;

        /// <summary>
        /// 操作执行
        /// </summary>
        /// <param name="context">操作执行上下文</param>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!AllowNulls)
            {
                var nullArguments = context.ActionArguments
                    .Where(arg => arg.Value == null)
                    .Select(arg => new ValidationError()
                    {
                        Name = arg.Key,
                        Message = "值不能为空"
                    }).ToList();

                if (nullArguments.Any())
                {
                    context.Result = new ValidationFailedResult(nullArguments) { AllowMultipleResult = AllowMultipleResult };
                    return;
                }
            }

            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(e => e.Value.Errors.Count > 0)
                    .Select(e => new ValidationError()
                    {
                        Name = e.Key,
                        Message = e.Value.Errors.First().ErrorMessage
                    }).ToList();

                context.Result = new ValidationFailedResult(errors) { AllowMultipleResult = AllowMultipleResult };
            }
        }

        /// <summary>
        /// 操作执行完毕
        /// </summary>
        /// <param name="context">操作执行完毕上下文</param>
        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
