using System;
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
        public SimpleCanalOptions Standalone { get; set; }

        /// <summary>
        /// 集群配置
        /// </summary>
        public ClusterCanalOptions Cluster { get; set; }
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
        public void Configure(CanalOptions options)
        {
            options.Filter = _configuration["Canal:Filter"] ?? ".*\\..*";
            options.BatchSize = Convert.ToInt32(_configuration["Canal:BatchSize"] ?? "1024");
            options.Mode = _configuration["Canal:Mode"] ?? "Standalone";
            if (options.Mode == "Standalone" && _configuration[$"Canal:{options.Mode}:Host"] != null)
            {
                options.Standalone = new SimpleCanalOptions(
                    _configuration[$"Canal:{options.Mode}:Host"]??throw new ArgumentNullException($"[Canal:{options.Mode}:Host] 不能为空!"),
                    Convert.ToInt32(_configuration[$"Canal:{options.Mode}:Port"] ?? throw new ArgumentNullException($"[Canal:{options.Mode}:Port] 不能为空!")),
                    _configuration[$"Canal:{options.Mode}:ClientId"] ?? throw new ArgumentNullException($"[Canal:{options.Mode}:ClientId] 不能为空!"))
                {
                    Destination = _configuration[$"Canal:{options.Mode}:Destination"],
                    UserName = _configuration[$"Canal:{options.Mode}:UserName"] ?? string.Empty,
                    Password = _configuration[$"Canal:{options.Mode}:Password"] ?? string.Empty,
                    IdleTimeout = Convert.ToInt32(_configuration[$"Canal:{options.Mode}:IdleTimeout"] ?? "3600000"),
                    SoTimeout = Convert.ToInt32(_configuration[$"Canal:{options.Mode}:SoTimeout"] ?? "60000"),
                    LazyParseEntry =
                        Convert.ToBoolean(_configuration[$"Canal:{options.Mode}:LazyParseEntry"] ?? "false"),
                };
                options.Cluster = new ClusterCanalOptions("", "");
            }
            else if (options.Mode == "Cluster"&& _configuration[$"Canal:{options.Mode}:ZkAddress"]!=null)
            {
                options.Cluster = new ClusterCanalOptions(
                    _configuration[$"Canal:{options.Mode}:ZkAddress"] ?? throw new ArgumentNullException($"[Canal:{options.Mode}:ZkAddress] 不能为空!"),
                    _configuration[$"Canal:{options.Mode}:ClientId"] ?? throw new ArgumentNullException($"[Canal:{options.Mode}:ClientId] 不能为空!"))
                {
                    Destination = _configuration[$"Canal:{options.Mode}:Destination"],
                    ClientId = _configuration[$"Canal:{options.Mode}:ClientId"],
                    UserName = _configuration[$"Canal:{options.Mode}:UserName"] ?? string.Empty,
                    Password = _configuration[$"Canal:{options.Mode}:Password"] ?? string.Empty,
                    IdleTimeout = Convert.ToInt32(_configuration[$"Canal:{options.Mode}:IdleTimeout"] ?? "3600000"),
                    SoTimeout = Convert.ToInt32(_configuration[$"Canal:{options.Mode}:SoTimeout"] ?? "60000"),
                    LazyParseEntry =
                        Convert.ToBoolean(_configuration[$"Canal:{options.Mode}:LazyParseEntry"] ?? "false"),
                    ZkSessionTimeout =
                        Convert.ToInt32(_configuration[$"Canal:{options.Mode}:ZkSessionTimeout"] ?? "5000"),
                };
                options.Standalone = new SimpleCanalOptions("", 0, "");
            }

            _configuration.GetSection("Canal").Bind(options);

        }

    }
}
