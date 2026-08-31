using Bing.Threading;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc;

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
        ObjectResultTypes = new List<Type>
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
    /// <param name="excludeTypes">需要排除的结果类型。</param>
    /// <returns>返回类型应按对象结果处理时返回 <see langword="true"/>；命中排除类型或不属于对象结果时返回 <see langword="false"/>。</returns>
    /// <remarks>异步返回类型会先解包后判断。</remarks>
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
