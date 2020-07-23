using System;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// 属性注入 特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class FromServiceAttribute : Attribute
    {
    }
}
