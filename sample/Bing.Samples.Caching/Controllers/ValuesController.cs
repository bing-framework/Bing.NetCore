using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using Bing.Caching;
using Bing.Caching.CacheStats;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Caching.Controllers
{
    /// <summary>
    /// 值控制器
    /// </summary>
    public class ValuesController:ApiControllerBase
    {
        /// <summary>
        /// 缓存提供程序
        /// </summary>
        private readonly ICacheProvider _cacheProvider;

        /// <summary>
        /// 初始化一个<see cref="ValuesController"/>类型的实例
        /// </summary>
        public ValuesController(ICacheProvider cacheProvider)
        {
            _cacheProvider = cacheProvider;
        }

        /// <summary>
        /// 获取参数
        /// </summary>
        /// <param name="str">类型</param>
        /// <returns></returns>
        [HttpGet]
        public string Get(string str)
        {
            var method = str.ToLower();
            switch (method)
            {
                case "get":
                    var res = _cacheProvider.Get("demo", () => "456", TimeSpan.FromMinutes(1));
                    return $"已获取缓存 : {res}";
                case "set":
                    _cacheProvider.Set("demo","123",TimeSpan.FromMinutes(1));
                    return "已设置缓存";
                case "remove":
                    _cacheProvider.Remove("demo");
                    return "已移除缓存";
                case "getcount":
                    var count = _cacheProvider.GetCount();
                    return $"获取缓存数量 : {count}";
                default:
                    return "未知操作";
            }
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
                    var res = await _cacheProvider.GetAsync("demo", async () => await Task.FromResult("456"),
                        TimeSpan.FromMinutes(1));
                    return $"已获取缓存 : {res}";
                case "set":
                    await _cacheProvider.SetAsync("demo", "123", TimeSpan.FromMinutes(1));
                    return "已设置缓存";
                case "remove":
                    await _cacheProvider.RemoveAsync("demo");
                    return "已移除缓存";
                case "getcount":
                    var count = _cacheProvider.GetCount();
                    return $"获取缓存数量 : {count}";
                default:
                    return "未知操作";
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("stats")]
        public string Stats()
        {
            var hit = _cacheProvider.CacheStatsInfo.GetStatistic(StatsType.Hit);
            var missed = _cacheProvider.CacheStatsInfo.GetStatistic(StatsType.Missed);

            return $"hit = {hit} ,missed = {missed}";
        }
    }
}
