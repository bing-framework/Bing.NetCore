using System;

namespace Bing.Caching.Internal
{
    /// <summary>
    /// 服务端点
    /// </summary>
    public class ServerEndPoint
    {
        /// <summary>
        /// 主机
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 初始化一个<see cref="ServerEndPoint"/>类型的实例
        /// </summary>
        public ServerEndPoint() { }

        /// <summary>
        /// 初始化一个<see cref="ServerEndPoint"/>类型的实例
        /// </summary>
        /// <param name="host">主机名</param>
        /// <param name="port">端口号</param>
        public ServerEndPoint(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentNullException(nameof(host));
            }
            Host = host;
            Port = port;
        }
    }
}
