using Microsoft.Extensions.Configuration;

namespace Bing.Configuration
{
    /// <summary>
    /// 配置访问器
    /// </summary>
    public interface IConfigurationAccessor
    {
        /// <summary>
        /// 配置
        /// </summary>
        IConfigurationRoot Configuration { get; }
    }
}
