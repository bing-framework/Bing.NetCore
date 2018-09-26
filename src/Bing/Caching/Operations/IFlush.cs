namespace Bing.Caching.Operations
{
    /// <summary>
    /// 清空缓存
    /// </summary>
    public interface IFlush
    {
        /// <summary>
        /// 清空所有缓存
        /// </summary>
        void Flush();
    }
}
