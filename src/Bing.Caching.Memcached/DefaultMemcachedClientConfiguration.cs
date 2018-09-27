using System;
using System.Collections.Generic;
using System.Net;
using Bing.Logs;
using Bing.Logs.Core;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Protocol.Binary;
using Enyim.Caching.Memcached.Protocol.Text;
using Enyim.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.Caching.Memcached
{
    /// <summary>
    /// 默认 Memcached 客户端配置
    /// </summary>
    public class DefaultMemcachedClientConfiguration: IMemcachedClientConfiguration
    {
        /// <summary>
        /// Memcached 节点定位器
        /// </summary>
        private Type _nodeLocator;

        /// <summary>
        /// 转码器
        /// </summary>
        private ITranscoder _transcoder;

        /// <summary>
        /// Memcached 键转换器
        /// </summary>
        private IMemcachedKeyTransformer _keyTransformer;

        /// <summary>
        /// 日志
        /// </summary>
        private ILogger<DefaultMemcachedClientConfiguration> _logger;

        /// <summary>
        /// DNS端点服务列表
        /// </summary>
        public IList<DnsEndPoint> Servers { get; }

        /// <summary>
        /// Socket连接池
        /// </summary>
        public ISocketPoolConfiguration SocketPool { get; }

        /// <summary>
        /// 验证设置
        /// </summary>
        public IAuthenticationConfiguration Authentication { get; }

        /// <summary>
        /// Memcached 键转换器
        /// </summary>
        public IMemcachedKeyTransformer KeyTransformer
        {
            get => this._keyTransformer ?? (this._keyTransformer = new DefaultKeyTransformer());
            set => this._keyTransformer = value;
        }

        /// <summary>
        /// Memcached 节点定位器
        /// </summary>
        public Type NodeLocator
        {
            get => this._nodeLocator;
            set
            {
                ConfigurationHelper.CheckForInterface(value, typeof(IMemcachedNodeLocator));
                this._nodeLocator = value;
            }
        }

        /// <summary>
        /// Memcached 节点定位器工厂
        /// </summary>
        public IProviderFactory<IMemcachedNodeLocator> NodeLocatorFactory { get; set; }

        /// <summary>
        /// 转码器
        /// </summary>
        public ITranscoder Transcoder
        {
            get => this._transcoder ?? (_transcoder = new DefaultTranscoder());
            set => this._transcoder = value;
        }

        /// <summary>
        /// 客户端与服务器之间的通信类型
        /// </summary>
        public MemcachedProtocol Protocol { get; set; }

        /// <summary>
        /// DNS端点服务列表
        /// </summary>
        IList<DnsEndPoint> IMemcachedClientConfiguration.Servers => this.Servers;

        /// <summary>
        /// Socket连接池
        /// </summary>
        ISocketPoolConfiguration IMemcachedClientConfiguration.SocketPool => this.SocketPool;

        /// <summary>
        /// 验证设置
        /// </summary>
        IAuthenticationConfiguration IMemcachedClientConfiguration.Authentication => this.Authentication;

        /// <summary>
        /// 初始化一个<see cref="DefaultMemcachedClientConfiguration"/>类型的实例
        /// </summary>
        /// <param name="options">Memcached选项</param>
        /// <param name="loggerFactory">日志工厂</param>
        /// <param name="transcoder">转码器</param>
        /// <param name="keyTransformer">Memcached 键转换器</param>
        public DefaultMemcachedClientConfiguration(ILoggerFactory loggerFactory,IOptionsMonitor<MemcachedOptions> options,
            ITranscoder transcoder = null, IMemcachedKeyTransformer keyTransformer = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _logger = loggerFactory.CreateLogger<DefaultMemcachedClientConfiguration>();
            var dbConfig = options.CurrentValue.DbConfig;
            Servers = new List<DnsEndPoint>();
            foreach (var server in dbConfig.Servers)
            {
                Servers.Add(new DnsEndPoint(server.Address, server.Port));
            }

            SocketPool = new SocketPoolConfiguration();
            if (dbConfig.SocketPool != null)
            {
                dbConfig.SocketPool.CheckPoolSize();
                dbConfig.SocketPool.CheckTimeout();

                SocketPool.MinPoolSize = dbConfig.SocketPool.MinPoolSize;
                _logger.LogInformation($"{nameof(SocketPool.MinPoolSize)}: {SocketPool.MinPoolSize}");

                SocketPool.MaxPoolSize = dbConfig.SocketPool.MaxPoolSize;
                _logger.LogInformation($"{nameof(SocketPool.MaxPoolSize)}: {SocketPool.MaxPoolSize}");

                SocketPool.ConnectionTimeout = dbConfig.SocketPool.ConnectionTimeout;
                _logger.LogInformation($"{nameof(SocketPool.ConnectionTimeout)}: {SocketPool.ConnectionTimeout}");

                SocketPool.ReceiveTimeout = dbConfig.SocketPool.ReceiveTimeout;
                _logger.LogInformation($"{nameof(SocketPool.ReceiveTimeout)}: {SocketPool.ReceiveTimeout}");

                SocketPool.DeadTimeout = dbConfig.SocketPool.DeadTimeout;
                _logger.LogInformation($"{nameof(SocketPool.DeadTimeout)}: {SocketPool.DeadTimeout}");

                SocketPool.QueueTimeout = dbConfig.SocketPool.QueueTimeout;
                _logger.LogInformation($"{nameof(SocketPool.QueueTimeout)}: {SocketPool.QueueTimeout}");
            }

            if (keyTransformer != null)
            {
                this.KeyTransformer = keyTransformer;
                _logger.LogDebug($"Use KeyTransformer Type : '{keyTransformer.ToString()}'");
            }

            if (NodeLocator == null)
            {
                NodeLocator = dbConfig.Servers.Count > 1 ? typeof(DefaultNodeLocator) : typeof(SingleNodeLocator);
            }

            if (transcoder != null)
            {
                this._transcoder = transcoder;
                _logger.LogDebug($"Use Transcoder Type : '{transcoder.ToString()}'");
            }

            if (dbConfig.NodeLocatorFactory != null)
            {
                NodeLocatorFactory = dbConfig.NodeLocatorFactory;
            }
        }

        /// <summary>
        /// 添加服务器到连接池
        /// </summary>
        /// <param name="address">服务器地址</param>
        public void AddServer(string address)
        {
            this.Servers.Add(ConfigurationHelper.ResolveToEndPoint(address));
        }

        /// <summary>
        /// 添加服务器到连接池
        /// </summary>
        /// <param name="host">主机</param>
        /// <param name="port">端口号</param>
        public void AddServer(string host, int port)
        {
            this.Servers.Add(new DnsEndPoint(host, port));
        }

        /// <summary>
        /// 创建 Memcached 键转换器
        /// </summary>
        /// <returns></returns>
        IMemcachedKeyTransformer IMemcachedClientConfiguration.CreateKeyTransformer() => this.KeyTransformer;

        /// <summary>
        /// 创建 Memcached 节点定位器
        /// </summary>
        /// <returns></returns>
        IMemcachedNodeLocator IMemcachedClientConfiguration.CreateNodeLocator()
        {
            if (this.NodeLocatorFactory != null)
            {
                return this.NodeLocatorFactory.Create();
            }

            return this.NodeLocator == null
                ? new SingleNodeLocator()
                : (IMemcachedNodeLocator)FastActivator.Create(this.NodeLocator);
        }

        /// <summary>
        /// 创建转码器
        /// </summary>
        /// <returns></returns>
        ITranscoder IMemcachedClientConfiguration.CreateTranscoder() => this.Transcoder;

        /// <summary>
        /// 创建服务池
        /// </summary>
        /// <returns></returns>
        IServerPool IMemcachedClientConfiguration.CreatePool()
        {
            switch (this.Protocol)
            {
                case MemcachedProtocol.Text:
                    return new DefaultServerPool(this, new TextOperationFactory(), _logger);
                case MemcachedProtocol.Binary:
                    return new BinaryPool(this, _logger);
            }

            throw new ArgumentOutOfRangeException($"未知通信类型: {(int) this.Protocol}");
        }
    }
}
