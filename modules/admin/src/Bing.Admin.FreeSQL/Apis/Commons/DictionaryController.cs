using Bing.Admin.Service.Abstractions.Commons;
using Bing.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Commons
{
    /// <summary>
    /// 字典 控制器
    /// </summary>
    public class DictionaryController : ApiControllerBase
    {
        /// <summary>
        /// 字典 服务
        /// </summary>
        public IDictionaryService DictionaryService { get; }
    
        /// <summary>
        /// 字典 查询服务
        /// </summary>
        public IQueryDictionaryService QueryDictionaryService { get; }

        /// <summary>
        /// 初始化一个<see cref="DictionaryController"/>类型的实例
        /// </summary>
        /// <param name="service">字典服务</param>
        /// <param name="queryService">字典查询服务</param>
        public DictionaryController( IDictionaryService service, IQueryDictionaryService queryService)
        {
            DictionaryService = service;
            QueryDictionaryService = queryService;
        }
    }
}
