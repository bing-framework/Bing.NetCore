using System.Threading.Tasks;

namespace Bing.ElasticSearch.Configs
{
    /// <summary>
    /// ElasticSearch 配置提供器
    /// </summary>
    public interface IElasticSearchConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        ElasticSearchConfig GetConfig();

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        Task<ElasticSearchConfig> GetConfigAsync();
    }
}
