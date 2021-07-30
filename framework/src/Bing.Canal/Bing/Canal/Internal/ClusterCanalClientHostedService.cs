//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using CanalSharp.Connections;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;

//namespace Bing.Canal.Internal
//{
//    /// <summary>
//    /// 集群Canal客户端后台服务
//    /// </summary>
//    internal class ClusterCanalClientHostedService : IHostedService
//    {
//        /// <summary>
//        /// 日志
//        /// </summary>
//        private readonly ILogger<ClusterCanalClientHostedService> _logger;

//        /// <summary>
//        /// Canal选项配置
//        /// </summary>
//        private readonly ClusterCanalOptions _options;

//        /// <summary>
//        /// Canal 连接
//        /// </summary>
//        private ClusterCanalConnection _canalConnection;

//        /// <summary>
//        /// 连接器
//        /// </summary>
//        private readonly IConnector _connector;

//        /// <summary>
//        /// Canal 格式化器
//        /// </summary>
//        private readonly IFormatter _formatter;

//        public ClusterCanalClientHostedService(ILogger<ClusterCanalClientHostedService> logger
//            , IOptions<ClusterCanalOptions> options
//            , IConnector connector
//            , IFormatter formatter)
//        {
//            _logger = logger;
//            _options = options.Value;
//            _connector = connector;
//            _formatter = formatter;
//        }

//        /// <summary>
//        /// Triggered when the application host is ready to start the service.
//        /// </summary>
//        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
//        public Task StartAsync(CancellationToken cancellationToken)
//        {
//            throw new NotImplementedException();
//        }

//        /// <summary>
//        /// Triggered when the application host is performing a graceful shutdown.
//        /// </summary>
//        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
//        public Task StopAsync(CancellationToken cancellationToken)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
