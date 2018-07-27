using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.ElasticSearch.Configs;
using Elasticsearch.Net;
using Nest;

namespace Bing.ElasticSearch
{
    /// <summary>
    /// ElasticSearch 管理器
    /// 参考：https://www.cnblogs.com/huhangfei/p/7524886.html
    /// </summary>
    public class ElasticSearchManager:IElasticSearchManager
    {
        /// <summary>
        /// ElasticSearch 客户端
        /// </summary>
        private IElasticClient _esClient;

        /// <summary>
        /// 配置提供器
        /// </summary>
        protected readonly IElasticSearchConfigProvider ConfigProvider;

        /// <summary>
        /// 初始化一个<see cref="ElasticSearchManager"/>类型的实例
        /// </summary>
        /// <param name="provider">配置提供器</param>
        public ElasticSearchManager(IElasticSearchConfigProvider provider)
        {
            ConfigProvider = provider ?? throw new ArgumentNullException();
        }

        /// <summary>
        /// 获取ES客户端
        /// </summary>
        /// <returns></returns>
        protected virtual async Task<IElasticClient> GetClient()
        {
            if (_esClient != null)
            {
                return _esClient;
            }
            var config = await ConfigProvider.GetConfigAsync();
            _esClient = CreateClient(config);
            return _esClient;
        }

        /// <summary>
        /// 创建ElasticSearch客户端
        /// </summary>
        /// <param name="config">配置</param>
        /// <returns></returns>
        private IElasticClient CreateClient(ElasticSearchConfig config)
        {
            var nodes = config.ConnectionString.Split('|').Select(x => new Uri(x)).ToList();
            var connectionPool = new StaticConnectionPool(nodes);
            var connectionSettings = new ConnectionSettings(connectionPool);
            connectionSettings.DisablePing(false);
            connectionSettings.DisableDirectStreaming(true);
            connectionSettings.ThrowExceptions(true);
            //connectionSettings.BasicAuthentication(config.UserName, config.Password);
            return new ElasticClient(connectionSettings);
        }

        /// <summary>
        /// 创建索引。不映射
        /// </summary>
        /// <param name="indexName">索引名</param>
        /// <returns></returns>
        public async Task CreateIndexAsync(string indexName)
        {
            var exists =await (await GetClient()).IndexExistsAsync(indexName);
            if (exists.Exists)
            {
                return;
            }

            var newName = indexName + DateTime.Now.Ticks;
            var result = await (await GetClient()).CreateIndexAsync(newName,
                ss => ss.Index(newName).Settings(o =>
                    o.NumberOfShards(1).NumberOfReplicas(1).Setting("max_result_window", int.MaxValue)));
            if (result.Acknowledged)
            {
                await (await GetClient()).AliasAsync(al => al.Add(add => add.Index(newName).Alias(indexName)));
                return;
            }

            throw new ElasticSearchException($"创建索引 {indexName} 失败：{result.ServerError.Error.Reason}");
        }

        public Task CreateIndexAsync<T>(string indexName) where T : class
        {
            throw new System.NotImplementedException();
        }

        public Task ResetIndexAsync<T>(string indexName) where T : class
        {
            throw new System.NotImplementedException();
        }

        public Task DeleteIndexAsync(string indexName)
        {
            throw new System.NotImplementedException();
        }

        public Task ReBuild<T>(string indexName) where T : class
        {
            throw new System.NotImplementedException();
        }

        public Task SaveAsync<T>(string indexName, T entity) where T : class
        {
            throw new System.NotImplementedException();
        }

        public Task BulkSaveAsync<T>(string indexName, List<T> entities, int bulkNum = 1000) where T : class
        {
            throw new System.NotImplementedException();
        }

        public Task DeleteAsync<T>(string indexName, string typeName, T entity) where T : class
        {
            throw new System.NotImplementedException();
        }

        public Task BulkDeleteAsync<T>(string indexName, List<T> entities, int bulkNum = 1000) where T : class
        {
            throw new System.NotImplementedException();
        }
    }
}
