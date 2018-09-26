using System.Collections.Generic;
using Bing.Caching.Internal;

namespace Bing.Caching.Options
{
    /// <summary>
    /// Redis选项基类
    /// </summary>
    public class RedisOptionsBase
    {
        /// <summary>
        /// 服务端点列表。用于连接Redis服务器的端点列表
        /// </summary>
        public IList<ServerEndPoint> EndPoints { get; } = new List<ServerEndPoint>();

        /// <summary>
        /// 密码。用于连接Redis服务器的密码
        /// </summary>
        public string Password { get; set; } = null;

        /// <summary>
        /// 连接超时时间。单位：毫秒
        /// </summary>
        public int ConnectionTimeout { get; set; } = 5000;

        /// <summary>
        /// 是否使用SSL加密
        /// </summary>
        public bool IsSsl { get; set; } = false;

        /// <summary>
        /// SSL主机。如果设置，它将在服务器的证书上执行此特定主机
        /// </summary>
        public string SslHost { get; set; } = null;

        /// <summary>
        /// 是否允许管理
        /// </summary>
        public bool AllowAdmin { get; set; } = false;

        /// <summary>
        /// 字符串配置
        /// </summary>
        public string Configuration { get; set; } = "";
    }
}
