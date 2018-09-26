using Bing.Caching.Options;

namespace Bing.Caching.InMemory
{
    /// <summary>
    /// 内存选项
    /// </summary>
    public class InMemoryOptions:CacheProviderOptionsBase
    {
        /// <summary>
        /// 初始化一个<see cref="InMemoryOptions"/>类型的实例
        /// </summary>
        public InMemoryOptions()
        {
            this.CacheProviderType = CacheProviderType.InMemory;
        }
    }
}
