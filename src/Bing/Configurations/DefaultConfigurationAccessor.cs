using Microsoft.Extensions.Configuration;

namespace Bing.Configurations
{
    /// <summary>
    /// 默认配置访问器
    /// </summary>
    public class DefaultConfigurationAccessor : IConfigurationAccessor
    {
        /// <summary>
        /// 空
        /// </summary>
        public static DefaultConfigurationAccessor Empty { get; }

        /// <summary>
        /// 配置
        /// </summary>
        public virtual IConfigurationRoot Configuration { get; }

        /// <summary>
        /// 静态构造函数
        /// </summary>
        static DefaultConfigurationAccessor() => Empty = new DefaultConfigurationAccessor(new ConfigurationBuilder().Build());

        /// <summary>
        /// 初始化一个<see cref="DefaultConfigurationAccessor"/>类型的实例
        /// </summary>
        /// <param name="configuration">配置</param>
        public DefaultConfigurationAccessor(IConfigurationRoot configuration) => Configuration = configuration;
    }
}
