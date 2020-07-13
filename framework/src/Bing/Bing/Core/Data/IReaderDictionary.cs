namespace Bing.Core.Data
{
    /// <summary>
    /// 读取字典
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public interface IReaderDictionary<in TKey, out TValue>
    {
        /// <summary>
        /// 读取
        /// </summary>
        /// <param name="key">键</param>
        TValue Reader(TKey key);
    }
}
