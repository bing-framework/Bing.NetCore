namespace Bing.ElasticSearch.Configs
{
    /// <summary>
    /// ElasticSearch 连接配置
    /// </summary>
    public class ElasticSearchConfig
    {
        /// <summary>
        /// 连接字符串，支持多个节点主机，使用|进行分隔。
        /// 例如：localhost:9200|localhost:8200
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }
    }
}
