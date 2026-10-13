using System.ComponentModel;
using Bing.AspNetCore;
using Bing.Caching;
using Bing.Caching.CSRedis;
using Bing.Core.Modularity;
using CSRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// 注册管理站点使用的缓存服务。
    /// </summary>
    [Description("缓存模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class CacheModule : AspNetCoreBingModule
    {
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Framework;

        /// <inheritdoc />
        public override int Order => 1;

        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            var configuration = services.GetConfiguration();
            var connectionStr = configuration.GetConnectionString("RedisConnection");
            var client = new CSRedisClient(connectionStr);
            services.AddSingleton(client);
            services.AddSingleton(new CacheConfiguration(configuration["Caching:Namespace"], "CSRedis"));
            services.AddScoped<ICache, CSRedisCacheManager>();
            return services;
        }
    }
}
