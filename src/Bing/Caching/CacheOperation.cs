namespace Bing.Caching
{
    /// <summary>
    /// 缓存操作
    /// </summary>
    public enum CacheOperation
    {
        /// <summary>
        /// 读写操作，如果缓存中有数据，则使用缓存中的数据，如果缓存中没有数据，则加载数据，并写入缓存
        /// </summary>
        ReadWrite,
        /// <summary>
        /// 写入操作，从数据源中加载最新的数据，并写入缓存
        /// </summary>
        Write,
        /// <summary>
        /// 只读操作，只从缓存中读取，用于其他地方往缓存写，这里只读的场景
        /// </summary>
        OnlyRead,
        /// <summary>
        /// 加载操作，只从数据源加载数据，不读取缓存中的数据，也不写入缓存
        /// </summary>
        Load
    }
}
