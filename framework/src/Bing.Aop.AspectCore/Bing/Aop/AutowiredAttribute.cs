using System;
using AspectCore.DependencyInjection;

namespace Bing.Aop
{
    /// <summary>
    /// 属性注入 属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class AutowiredAttribute : FromServiceContextAttribute
    {
    }
}
