namespace Bing.Mapping
{
    /// <summary>
    /// 对象泛型映射
    /// </summary>
    public interface IGenericObjectMapper
    {
        /// <summary>
        /// 将对象映射为指定类型
        /// </summary>
        /// <typeparam name="TFrom">来源类型</typeparam>
        /// <typeparam name="TTo">目标类型</typeparam>
        /// <param name="fromObject">来源对象</param>
        /// <returns>映射结果</returns>
        TTo MapTo<TFrom, TTo>(TFrom fromObject);

        /// <summary>
        /// 将对象映射为指定类型
        /// </summary>
        /// <typeparam name="TFrom">来源类型</typeparam>
        /// <typeparam name="TTo">目标类型</typeparam>
        /// <param name="fromObject">来源对象</param>
        /// <param name="toInstance">目标实例对象</param>
        /// <returns>映射结果，将返回 <paramref name="toInstance"/> 实例</returns>
        TTo MapTo<TFrom, TTo>(TFrom fromObject, TTo toInstance);
    }
}
