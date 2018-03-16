using System;
using System.Collections.Generic;
using System.Text;
using Bing.Webs.Commons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.Webs.Filters
{
    /// <summary>
    /// 响应结果处理过滤器
    /// </summary>
    public class ResultHandlerAttribute: ResultFilterAttribute
    {
        /// <summary>
        /// 结果处理
        /// </summary>
        /// <param name="context">结果执行上下文</param>
        public override void OnResultExecuting(ResultExecutingContext context)
        {
            var objectResult = (ObjectResult) context.Result;
            context.Result = new Result(StateCode.Ok, "", objectResult.Value);
        }
    }
}
