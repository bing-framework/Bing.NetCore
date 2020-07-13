namespace Bing.Utils.Conversions
{
    /// <summary>
    /// 转换实现
    /// </summary>
    /// <typeparam name="TFrom">来源类型</typeparam>
    /// <typeparam name="TTo">目标类型</typeparam>
    public interface IConversionImpl<in TFrom, TTo>
    {
        /// <summary>
        /// 尝试转换
        /// </summary>
        /// <param name="from">源对象</param>
        /// <param name="to">目标对象</param>
        bool TryTo(TFrom from, out TTo to);
    }
}
