using System.Threading.Tasks;

namespace Bing.ElasticSearch.Configs
{
    /// <summary>
    /// ElasticSearch 默认配置提供器
    /// </summary>
    public class DefaultElasticSearchConfigProvider:IElasticSearchConfigProvider
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly ElasticSearchConfig _config;

        /// <summary>
        /// 初始化一个<see cref="DefaultElasticSearchConfigProvider"/>类型的实例
        /// </summary>
        /// <param name="config">ElasticSearch 配置</param>
        public DefaultElasticSearchConfigProvider(ElasticSearchConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public ElasticSearchConfig GetConfig()
        {
            return _config;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        public Task<ElasticSearchConfig> GetConfigAsync()
        {
            return Task.FromResult(_config);
        }
    }
}
