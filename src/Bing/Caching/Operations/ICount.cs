namespace Bing.Caching.Operations
{
    /// <summary>
    /// 缓存数量
    /// </summary>
    public interface ICount
    {
        /// <summary>
        /// 获取缓存数量
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        /// <returns></returns>
        int GetCount(string prefix = "");
    }
}
