namespace Bing.Caching
{
    /// <summary>
    /// 缓存提供程序工厂
    /// </summary>
    public interface ICacheProiderFactory
    {
        /// <summary>
        /// 获取缓存提供程序
        /// </summary>
        /// <param name="name">缓存提供程序名称</param>
        /// <returns></returns>
        ICacheProvider GetCacheProvider(string name);
    }
}
