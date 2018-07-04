namespace Bing.Caching.Redis
{
    /// <summary>
    /// Redis缓存配置类
    /// </summary>
    public class RedisCacheOptions
    {
        /// <summary>
        /// 主机名
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// 端口
        /// </summary>
        public string Port { get; set; }

        /// <summary>
        /// 授权密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 数据库
        /// </summary>
        public int Db { get; set; }
    }
}
