using System;

namespace Bing.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// 忽略响应结果处理
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public class IgnoreResultHandlerAttribute : Attribute
    {
    }
}
