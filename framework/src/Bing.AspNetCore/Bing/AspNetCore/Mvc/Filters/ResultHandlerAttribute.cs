using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.AspNetCore.Mvc.Filters;

/// <summary>
/// 响应结果处理过滤器
/// </summary>
public class ResultHandlerAttribute : ResultFilterAttribute
{
    /// <summary>
    /// 结果处理
    /// </summary>
    /// <param name="context">结果执行上下文</param>
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (HasIgnoreResultHandler(context))
            return;
        if (HasIgnoreHandle(context.Result))
            return;

        if (context.Result is ValidationFailedResult validationFailedResult)
        {
            context.Result = new JsonResult(new
            {
                Code = (int)StatusCode.Fail,
                Message = validationFailedResult.AllowMultipleResult
                    ? validationFailedResult.Errors.FirstOrDefault()?.Message
                    : "验证数据失败!",
                Errors = validationFailedResult.Errors
            });
            return;
        }

        if (context.Result is BadRequestObjectResult badRequestObjectResult)
        {
            if (badRequestObjectResult.Value is ValidationProblemDetails details)
            {
                context.Result = new ApiResult(StatusCode.Fail, "无效输入参数", details);
                return;
            }

            context.Result = new ApiResult(StatusCode.Fail, "无效请求");
        }
        else if (context.Result is ObjectResult objectResult)
        {
            context.Result = new ApiResult(StatusCode.Ok, string.Empty, objectResult.Value);
        }
        else if (context.Result is EmptyResult emptyResult)
        {
            context.Result = new ApiResult(StatusCode.Ok, string.Empty);
        }
        else if (context.Result is JsonResult jsonResult)
        {
            context.Result = new ApiResult(StatusCode.Ok, string.Empty, jsonResult.Value);
        }
    }

    /// <summary>
    /// 是否忽略结果处理
    /// </summary>
    /// <param name="context">结果执行上下文</param>
    private bool HasIgnoreResultHandler(ResultExecutingContext context)
    {
        // 控制器过滤
        if (context.Controller.GetType().GetCustomAttributes<IgnoreResultHandlerAttribute>().Any())
            return true;
        // Action过滤
        if (context.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
        {
            var ignore = controllerActionDescriptor.MethodInfo.GetCustomAttributes<IgnoreResultHandlerAttribute>().Any();
            if (ignore)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 是否忽略处理
    /// </summary>
    /// <param name="result">操作结果</param>
    private bool HasIgnoreHandle(IActionResult result)
    {
        if (result is ApiResult)
            return true;
        return false;
    }
}