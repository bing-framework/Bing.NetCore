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
            ConfigProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// 获取ES客户端
        /// </summary>
        /// <returns></returns>
        protected virtual async Task<IElasticClient> GetClientAsync()
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
        public virtual async Task CreateIndexAsync(string indexName)
        {
            var client = await GetClientAsync();
            var exists = await client.IndexExistsAsync(indexName);
            if (exists.Exists)
            {
                return;
            }

            var newName = indexName + DateTime.Now.Ticks;
            var result = await client.CreateIndexAsync(newName,
                ss => ss.Index(newName).Settings(o =>
                    o.NumberOfShards(1).NumberOfReplicas(1).Setting("max_result_window", int.MaxValue)));
            if (result.Acknowledged)
            {
                await client.AliasAsync(al => al.Add(add => add.Index(newName).Alias(indexName)));
                return;
            }

            throw new ElasticSearchException($"创建索引 {indexName} 失败：{result.ServerError.Error.Reason}");
        }

        /// <summary>
        /// 创建索引。自动映射实体属性
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="indexName">索引名</param>
        /// <returns></returns>
        public virtual async Task CreateIndexAsync<T>(string indexName) where T : class
        {
            var client = await GetClientAsync();

            var exists = await client.IndexExistsAsync(indexName);
            if (exists.Exists)
            {
                return;
            }
            var newName = indexName + DateTime.Now.Ticks;
            var result = await client.CreateIndexAsync(newName,
                ss => ss.Index(newName).Settings(o =>
                        o.NumberOfShards(1).NumberOfReplicas(1).Setting("max_result_window", int.MaxValue))
                    .Mappings(m => m.Map<T>(mm => mm.AutoMap())));
            if (result.Acknowledged)
            {
                await client.AliasAsync(al => al.Add(add => add.Index(newName).Alias(indexName)));
                return;
            }
            throw new ElasticSearchException($"创建索引 {indexName} 失败：{result.ServerError.Error.Reason}");
        }

        /// <summary>
        /// 重置索引
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="indexName">索引名</param>
        /// <returns></returns>
        public virtual async Task ResetIndexAsync<T>(string indexName) where T : class
        {
            await DeleteIndexAsync(indexName);
            await CreateIndexAsync<T>(indexName);
        }

        /// <summary>
        /// 删除索引
        /// </summary>
        /// <param name="indexName">索引名</param>
        /// <returns></returns>
        public virtual async Task DeleteIndexAsync(string indexName)
        {
            var client = await GetClientAsync();
            var response = await client.DeleteIndexAsync(indexName);
            if (response.Acknowledged)
            {
                return;
            }

            throw new ElasticSearchException($"删除索引 {indexName} 失败：{response.ServerError.Error.Reason}");
        }

        /// <summary>
        /// 重新构建索引
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="indexName">索引名</param>
        /// <returns></returns>
        public virtual async Task ReBuild<T>(string indexName) where T : class
        {
            var client = await GetClientAsync();
            var result = await client.GetAliasAsync(q => q.Index(indexName));
            var oldName = result.Indices.Keys.First();
            // 创建新的索引
            var newIndex = indexName + DateTime.Now.Ticks;
            var createResult = await client.CreateIndexAsync(newIndex,
                c => c.Index(newIndex).Mappings(ms => ms.Map<T>(m => m.AutoMap())));
            if (!createResult.Acknowledged)
            {
                throw new ElasticSearchException($"重建索引-创建索引 {indexName} 失败：{createResult.ServerError.Error.Reason}");
            }
            // 重建索引数据
            var reResult =
                await client.ReindexOnServerAsync(descriptor =>
                    descriptor.Source(source => source.Index(indexName)).Destination(dest => dest.Index(newIndex)));
            if (reResult.ServerError != null)
            {
                throw new ElasticSearchException($"重建索引-设置索引 {indexName} 数据失败：{reResult.ServerError.Error.Reason}");
            }
            // 设置索引别名
            var aliasResult = await client.AliasAsync(al =>
                al.Remove(rem => rem.Index(oldName.ToString()).Alias(indexName))
                    .Add(add => add.Index(newIndex).Alias(indexName)));
            if (!aliasResult.Acknowledged)
            {
                throw new ElasticSearchException($"重建索引-设置别名 {indexName} 失败：{aliasResult.ServerError.Error.Reason}");
            }

            var delResult = await client.DeleteIndexAsync(oldName);
            if (!delResult.Acknowledged)
            {
                throw new ElasticSearchException($"重建索引-删除旧索引 {indexName} 失败：{delResult.ServerError.Error.Reason}");
            }
        }

        /// <summary>
        /// 保存
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="indexName">索引名</param>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        public virtual async Task SaveAsync<T>(string indexName, T entity) where T : class
        {
            var client = await GetClientAsync();
            var exists =
                await client.DocumentExistsAsync(DocumentPath<T>.Id(new Id(entity)), dd => dd.Index(indexName));
            if (exists.Exists)
            {
                var result = await client.UpdateAsync(DocumentPath<T>.Id(new Id(entity)),
                    ss => ss.Index(indexName).Doc(entity).RetryOnConflict(3));

                if (result.ServerError == null)
                {
                    return;
                }

                throw new ElasticSearchException($"更新文档在索引 {indexName} 失败：{result.ServerError.Error.Reason}");
            }
            else
            {
                var result = await client.IndexAsync<T>(entity, ss => ss.Index(indexName));
                if (result.ServerError == null)
                {
                    return;
                }
                throw new ElasticSearchException($"插入文档在索引 {indexName} 失败：{result.ServerError.Error.Reason}");
            }
        }

        /// <summary>
        /// 批量保存
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="indexName">索引名</param>
        /// <param name="entities">实体集合</param>
        /// <param name="bulkNum">批量操作数</param>
        /// <returns></returns>
        public virtual async Task BulkSaveAsync<T>(string indexName, List<T> entities, int bulkNum = 1000) where T : class
        {
            if (entities.Count <= bulkNum)
            {
                await BulkSave<T>(indexName, entities);
            }
            else
            {
                var total = (int) Math.Ceiling(entities.Count * 1.0f / bulkNum);
                var tasks = new List<Task>();
                for (var i = 0; i < total; i++)
                {
                    var i1 = i;
                    tasks.Add(Task.Factory.StartNew(()=> BulkSave<T>(indexName,entities.Skip(i1*bulkNum).Take(bulkNum).ToList())));
                }

                await Task.WhenAll(tasks.ToArray());
            }
        }

        /// <summary>
        /// 批量保存
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="indexName">索引名</param>
        /// <param name="entities">实体集合</param>
        /// <returns></returns>
        private async Task BulkSave<T>(string indexName, List<T> entities) where T : class
        {
            var client = await GetClientAsync();
            var bulk = new BulkRequest(indexName)
            {
                Operations = new List<IBulkOperation>()
            };
            foreach (var entity in entities)
            {
                bulk.Operations.Add(new BulkIndexOperation<T>(entity));
            }

            var response = await client.BulkAsync(bulk);
            if (response.Errors)
            {
                throw new ElasticSearchException($"批量保存文档在索引 {indexName} 失败：{response.ServerError.Error.Reason}");
            }
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="indexName">索引名</param>
        /// <param name="typeName">类型名</param>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        public virtual async Task DeleteAsync<T>(string indexName, string typeName, T entity) where T : class
        {
            var client = await GetClientAsync();
            var response = await client.DeleteAsync(new DeleteRequest(indexName, typeName, new Id(entity)));
            if (response.ServerError == null)
            {
                return;
            }

            throw new ElasticSearchException($"删除文档在索引 {indexName} 失败：{response.ServerError.Error.Reason}");
        }

        /// <summary>
        /// 批量删除
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="indexName">索引名</param>
        /// <param name="entities">实体集合</param>
        /// <param name="bulkNum">批量操作数</param>
        /// <returns></returns>
        public virtual async Task BulkDeleteAsync<T>(string indexName, List<T> entities, int bulkNum = 1000) where T : class
        {
            if (entities.Count <= bulkNum)
            {
                await BulkDelete<T>(indexName, entities);
            }
            else
            {
                var total = (int)Math.Ceiling(entities.Count * 1.0f / bulkNum);
                var tasks = new List<Task>();
                for (var i = 0; i < total; i++)
                {
                    var i1 = i;
                    tasks.Add(Task.Factory.StartNew(() => BulkDelete(indexName, entities.Skip(i1 * bulkNum).Take(bulkNum).ToList())));
                }

                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// 批量删除
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="indexName">索引名</param>
        /// <param name="entities">实体列表</param>
        /// <returns></returns>
        private async Task BulkDelete<T>(string indexName, List<T> entities) where T : class
        {
            var client = await GetClientAsync();
            var bulk = new BulkRequest(indexName)
            {
                Operations = new List<IBulkOperation>()
            };
            foreach (var entity in entities)
            {
                bulk.Operations.Add(new BulkDeleteOperation<T>(new Id(entity)));
            }
            var response = await client.BulkAsync(bulk);
            if (response.Errors)
            {
                throw new ElasticSearchException($"批量删除文档在索引 {indexName} 失败：{response.ServerError.Error.Reason}");
            }
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="indexName">索引名称</param>
        /// <param name="query">查询</param>
        /// <param name="skip">跳过的行数</param>
        /// <param name="size">每页显示记录数</param>
        /// <param name="includeFields">包含字段</param>
        /// <param name="preTags">高亮标签</param>
        /// <param name="postTags">高亮标签</param>
        /// <param name="disableHigh">是否禁用高亮</param>
        /// <param name="highFields">高亮字段</param>
        /// <returns></returns>
        public virtual async Task<ISearchResponse<T>> SearchAsync<T>(string indexName, SearchDescriptor<T> query, int skip, int size, string[] includeFields = null,
            string preTags = "<strong style=\"color: red;\">", string postTags = "</strong>", bool disableHigh = false,
            params string[] highFields) where T : class
        {
            query.Index(indexName);
            var highdes = new HighlightDescriptor<T>();
            if (disableHigh)
            {
                preTags = "";
                postTags = "";
            }
            highdes.PreTags(preTags).PostTags(postTags);

            var isHigh = highFields != null && highFields.Length > 0;
            var hfs = new List<Func<HighlightFieldDescriptor<T>, IHighlightField>>();

            // 分页
            query.Skip(skip).Take(size);
            // 关键词高亮
            if (isHigh)
            {
                foreach (var highField in highFields)
                {
                    hfs.Add(f=>f.Field(highField));
                }
            }

            highdes.Fields(hfs.ToArray());
            query.Highlight(h => highdes);
            if (includeFields != null)
            {
                query.Source(ss => ss.Includes(ff => ff.Fields(includeFields.ToArray())));
            }

            var client = await GetClientAsync();
            var response = await client.SearchAsync<T>(query);
            return response;
        }
    }
}
