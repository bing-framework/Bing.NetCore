using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Caching;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Caching.Controllers
{
    /// <summary>
    /// 多缓存控制器
    /// </summary>
    public class MuiltCacheController:ApiControllerBase
    {
        /// <summary>
        /// 缓存提供程序工厂
        /// </summary>
        private readonly ICacheProiderFactory _cacheProiderFactory;

        /// <summary>
        /// 初始化一个<see cref="MuiltCacheController"/>类型的实例
        /// </summary>
        /// <param name="cacheProiderFactory">缓存提供程序工厂</param>
        public MuiltCacheController(ICacheProiderFactory cacheProiderFactory)
        {
            _cacheProiderFactory = cacheProiderFactory;
        }

        /// <summary>
        /// 异步获取参数
        /// </summary>
        /// <param name="str">类型</param>
        /// <returns></returns>
        [HttpGet("getAsync")]
        public async Task<string> GetAsync(string str)
        {
            var method = str.ToLower();

            switch (method)
            {
                case "get":
                    //var res = await _cacheProvider.GetAsync("demo", async () => await Task.FromResult("456"),
                    //    TimeSpan.FromMinutes(1));
                    //return $"已获取缓存 : {res}";
                    return string.Empty;
                case "set":
                    await CacheAct(CacheConst.DefaultInMemoryName,
                        async x => { await x.SetAsync("demo", "Memory 123", TimeSpan.FromMinutes(1)); });
                    await CacheAct(CacheConst.DefaultRedisName,
                        async x => { await x.SetAsync("demo", "Redis", TimeSpan.FromMinutes(1)); });
                    await CacheAct($"{CacheConst.DefaultRedisName}001",
                        async x => { await x.SetAsync("demo", "Redis 1", TimeSpan.FromMinutes(1)); });
                    await CacheAct($"{CacheConst.DefaultRedisName}002",
                        async x => { await x.SetAsync("demo", "Redis 2", TimeSpan.FromMinutes(1)); });

                    return "已设置缓存";
                case "remove":
                    //await _cacheProvider.RemoveAsync("demo");
                    return "已移除缓存";
                case "getcount":
                    //var count = _cacheProvider.GetCount();
                    //return $"获取缓存数量 : {count}";
                    return string.Empty;
                default:
                    return "未知操作";
            }
        }

        private async Task CacheAct(string name, Action<ICacheProvider> action)
        {
            action.Invoke(_cacheProiderFactory.GetCacheProvider(name));
        }

        private async Task<string> CacheGet(string name)
        {
            var provider = _cacheProiderFactory.GetCacheProvider(name);
            var res = await provider.GetAsync("demo", async () => await Task.FromResult("456"),
                TimeSpan.FromMinutes(1));
            return res.Value;
        }
    }
}
