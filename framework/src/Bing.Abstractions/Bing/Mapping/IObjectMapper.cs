using System;

namespace Bing.Mapping
{
    /// <summary>
    /// 对象映射
    /// </summary>
    public interface IObjectMapper : IGenericObjectMapper
    {
        /// <summary>
        /// 将对象映射为指定类型
        /// </summary>
        /// <param name="sourceType">来源类型</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="source">来源对象</param>
        /// <returns>映射结果，将返回 <paramref name="targetType"/> 类型的实例</returns>
        object MapTo(Type sourceType, Type targetType, object source);

        /// <summary>
        /// 将对象映射为指定类型
        /// </summary>
        /// <param name="sourceType">来源类型</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="source">来源对象</param>
        /// <param name="target">目标对象</param>
        /// <returns>映射结果，将返回 <paramref name="target"/> 实例</returns>
        object MapTo(Type sourceType, Type targetType, object source, object target);
    }
}
