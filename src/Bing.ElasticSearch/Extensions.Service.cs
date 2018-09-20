using System;
using Bing.ElasticSearch.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.ElasticSearch
{
    /// <summary>
    /// Elasticsearch 扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册Elasticsearch日志操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="setupAction">配置操作</param>
        public static void AddElasticsearch(this IServiceCollection services, Action<ElasticSearchConfig> setupAction)
        {
            var config=new ElasticSearchConfig();
            setupAction?.Invoke(config);
            services.TryAddSingleton<IElasticSearchConfigProvider>(new DefaultElasticSearchConfigProvider(config));
            services.TryAddScoped<IElasticSearchManager,ElasticSearchManager>();
        }

        /// <summary>
        /// 注册Elasticsearch日志操作
        /// </summary>
        /// <typeparam name="TElasticSearchConfigProvider">Elasticsearch配置提供器</typeparam>
        /// <param name="services">服务集合</param>
        public static void AddElasticsearch<TElasticSearchConfigProvider>(this IServiceCollection services)
            where TElasticSearchConfigProvider : class, IElasticSearchConfigProvider
        {
            services.TryAddScoped<IElasticSearchConfigProvider, TElasticSearchConfigProvider>();
            services.TryAddScoped<IElasticSearchManager, ElasticSearchManager>();
        }
    }
}
