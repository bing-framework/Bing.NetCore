using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.Webs.Filters
{
    /// <summary>
    /// 验证过滤器
    /// </summary>
    public class ValidationAttribute:ActionFilterAttribute
    {
        /// <summary>
        /// 允许空值
        /// </summary>
        public bool AllowNulls { get; set; }

        /// <summary>
        /// 重写OnActionExecuting()，进行值过滤
        /// </summary>
        /// <param name="context"></param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!AllowNulls)
            {
                var nullArguments = context.ActionArguments
                    .Where(arg => arg.Value == null)
                    .Select(arg => new Error()
                    {
                        Name = arg.Key,
                        Message = "值不能为空"
                    }).ToArray();

                if (nullArguments.Any())
                {
                    context.Result = new BadRequestObjectResult(nullArguments);
                    return;
                }
            }

            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(e => e.Value.Errors.Count > 0)
                    .Select(e => new Error()
                    {
                        Name = e.Key,
                        Message = e.Value.Errors.First().ErrorMessage
                    }).ToArray();

                context.Result = new BadRequestObjectResult(errors);
            }
        }

        /// <summary>
        /// 错误
        /// </summary>
        private class Error
        {
            /// <summary>
            /// 属性名
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// 错误消息
            /// </summary>
            public string Message { get; set; }
        }
    }
}
