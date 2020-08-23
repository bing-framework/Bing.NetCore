using Bing.Applications;
using Bing.Datas.Sql;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Commons;

namespace Bing.Admin.Service.Implements.Commons
{
    /// <summary>
    /// 字典 查询服务
    /// </summary>
    public class QueryDictionaryService : Bing.Application.Services.ApplicationServiceBase, IQueryDictionaryService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 字典仓储
        /// </summary>
        protected IDictionaryRepository DictionaryRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryDictionaryService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="dictionaryRepository">字典仓储</param>
        public QueryDictionaryService( ISqlQuery sqlQuery, IDictionaryRepository dictionaryRepository )
        {
            SqlQuery = sqlQuery;
            DictionaryRepository = dictionaryRepository;
        }
    }
}
