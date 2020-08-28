using Bing.Applications;
using Bing.Datas.Sql;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Commons;

namespace Bing.Admin.Service.Implements.Commons
{
    /// <summary>
    /// 地区 查询服务
    /// </summary>
    public class QueryAreaService : Bing.Application.Services.AppServiceBase, IQueryAreaService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 地区仓储
        /// </summary>
        protected IAreaRepository AreaRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryAreaService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="areaRepository">地区仓储</param>
        public QueryAreaService( ISqlQuery sqlQuery, IAreaRepository areaRepository )
        {
            SqlQuery = sqlQuery;
            AreaRepository = areaRepository;
        }
    }
}
