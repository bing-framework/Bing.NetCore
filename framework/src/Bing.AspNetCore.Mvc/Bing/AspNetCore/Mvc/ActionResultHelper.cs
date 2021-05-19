using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Threading;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc
{
    /// <summary>
    /// 操作结果帮助类
    /// </summary>
    public static class ActionResultHelper
    {
        /// <summary>
        /// 对象结果类型列表
        /// </summary>
        public static List<Type> ObjectResultTypes { get; }

        /// <summary>
        /// 静态构造函数
        /// </summary>
        static ActionResultHelper()
        {
            ObjectResultTypes=new List<Type>
            {
                typeof(JsonResult),
                typeof(ObjectResult),
                typeof(NoContentResult)
            };
        }

        /// <summary>
        /// 是否对象结果
        /// </summary>
        /// <param name="returnType">返回类型</param>
        /// <param name="excludeTypes"></param>
        public static bool IsObjectResult(Type returnType, params Type[] excludeTypes)
        {
            returnType = AsyncHelper.UnwrapTask(returnType);
            if (!excludeTypes.IsNullOrEmpty() && excludeTypes.Any(t => t.IsAssignableFrom(returnType)))
                return false;
            if (!typeof(IActionResult).IsAssignableFrom(returnType))
                return true;
            return ObjectResultTypes.Any(t => t.IsAssignableFrom(returnType));
        }
    }
}
