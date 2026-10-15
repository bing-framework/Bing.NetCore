using System;

namespace Bing.Aspects
{
    /// <summary>
    /// 旧版 AspectCore 程序集中的属性注入标记替身。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class AutowiredAttribute : Attribute
    {
    }

    /// <summary>
    /// 旧版 AspectCore 程序集中的忽略拦截标记替身。
    /// </summary>
    public class IgnoreAttribute : Attribute
    {
    }

    /// <summary>
    /// 旧版 AspectCore 程序集中的非空参数标记替身。
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class NotNullAttribute : Attribute
    {
    }

    /// <summary>
    /// 旧版 AspectCore 程序集中的非空文本参数标记替身。
    /// </summary>
    public class NotEmptyAttribute : Attribute
    {
    }
}

namespace Bing.Validation
{
    /// <summary>
    /// 旧版 AspectCore 程序集中的模型验证标记替身。
    /// </summary>
    public class ValidAttribute : Attribute
    {
    }
}
