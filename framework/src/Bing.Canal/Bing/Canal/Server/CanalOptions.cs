using CanalSharp.Connections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Bing.Canal.Server
{
    /// <summary>
    /// Canal选项配置
    /// </summary>
    public class CanalOptions
    {
        /// <summary>
        /// 数据过滤
        /// </summary>
        /// <remarks>
        /// 允许所有数据：.*\\..* <br />
        /// 允许某个库数据：库名\\..* <br />
        /// 允许某些表：库名.表名,库名.表名
        /// </remarks>
        public string Filter { get; set; } = ".*\\..*";

        /// <summary>
        /// 数据大小
        /// </summary>
        public int BatchSize { get; set; } = 1024;

        /// <summary>
        /// 模式
        /// </summary>
        /// <remarks>
        /// 单点：Standalone <br />
        /// 集群：Cluster
        /// </remarks>
        public string Mode { get; set; } = "Standalone";

        /// <summary>
        /// 单点配置
        /// </summary>
        public CanalOptionsBase Standalone { get; set; }

        /// <summary>
        /// 集群配置
        /// </summary>
        public CanalOptionsBase Cluster { get; set; }
    }

    /// <summary>
    /// 配置Canal选项配置
    /// </summary>
    public class ConfigureCanalOptions : IConfigureOptions<CanalOptions>
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly IConfiguration _configuration;

        /// <summary>
        /// 初始化一个<see cref="ConfigureCanalOptions"/>类型的实例
        /// </summary>
        /// <param name="configuration">配置</param>
        public ConfigureCanalOptions(IConfiguration configuration) => _configuration = configuration;
        
        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="options">Canal选项配置</param>
        public void Configure(CanalOptions options) => _configuration.GetSection("Canal").Bind(options);
    }
}
