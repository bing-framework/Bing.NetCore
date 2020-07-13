namespace Bing.Core.Data
{
    /// <summary>
    /// 可获取
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public interface IGetable<in TKey, out TValue>
    {
        /// <summary>
        /// 根据键获取值
        /// </summary>
        /// <param name="key">键</param>
        TValue Get(TKey key);
    }
}
