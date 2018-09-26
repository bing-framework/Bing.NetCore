using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// Redis 数据库提供程序
    /// </summary>
    public class RedisDatabaseProvider:IRedisDatabaseProvider
    {
        /// <summary>
        /// Redis 数据库选项
        /// </summary>
        private readonly RedisDbOptions _options;

        /// <summary>
        /// Redis 连接多路复用器
        /// </summary>
        private readonly Lazy<ConnectionMultiplexer> _connectionMultiplexer;

        /// <summary>
        /// 初始化一个<see cref="RedisDatabaseProvider"/>类型的实例
        /// </summary>
        /// <param name="options">Redis选项</param>
        public RedisDatabaseProvider(IOptionsMonitor<RedisOptions> options)
        {
            _options = options.CurrentValue.DbConfig;
            _connectionMultiplexer=new Lazy<ConnectionMultiplexer>(CreateConnectionMultiplexer);
        }

        /// <summary>
        /// 创建连接
        /// </summary>
        /// <returns></returns>
        private ConnectionMultiplexer CreateConnectionMultiplexer()
        {
            if (string.IsNullOrWhiteSpace(_options.Configuration))
            {
                var configurationOptions = new ConfigurationOptions()
                {
                    ConnectTimeout = _options.ConnectionTimeout,
                    Password = _options.Password,
                    Ssl = _options.IsSsl,
                    SslHost = _options.SslHost,
                    AllowAdmin = _options.AllowAdmin,
                    DefaultDatabase = _options.Database
                };

                foreach (var endPoint in _options.EndPoints)
                {
                    configurationOptions.EndPoints.Add(endPoint.Host, endPoint.Port);
                }

                return ConnectionMultiplexer.Connect(configurationOptions.ToString());
            }

            return ConnectionMultiplexer.Connect(_options.Configuration);
        }

        /// <summary>
        /// 获取数据库
        /// </summary>
        /// <returns></returns>
        public IDatabase GetDatabase()
        {
            return _connectionMultiplexer.Value.GetDatabase();
        }

        /// <summary>
        /// 获取服务器列表
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IServer> GetServerList()
        {
            var endpoints = GetMastersServersEndpoints();
            foreach (var endpoint in endpoints)
            {
                yield return _connectionMultiplexer.Value.GetServer(endpoint);
            }
        }

        /// <summary>
        /// 获取主服务器端点
        /// </summary>
        /// <returns></returns>
        private List<EndPoint> GetMastersServersEndpoints()
        {
            var masters=new List<EndPoint>();
            foreach (var endPoint in _connectionMultiplexer.Value.GetEndPoints())
            {
                var server = _connectionMultiplexer.Value.GetServer(endPoint);
                if (server.IsConnected)
                {
                    // 集群
                    if (server.ServerType == ServerType.Cluster)
                    {
                        masters.AddRange(server.ClusterConfiguration.Nodes.Where(x => !x.IsSlave)
                            .Select(x => x.EndPoint));
                        break;
                    }
                    // 单库
                    if (server.ServerType == ServerType.Standalone && !server.IsSlave)
                    {
                        masters.Add(endPoint);
                        break;
                    }
                }
            }
            return masters;
        }
    }
}
