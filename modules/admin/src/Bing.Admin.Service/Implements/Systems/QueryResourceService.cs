using Bing.Applications;
using Bing.Datas.Sql;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 资源 查询服务
    /// </summary>
    public class QueryResourceService : Bing.Application.Services.AppServiceBase, IQueryResourceService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 资源仓储
        /// </summary>
        protected IResourceRepository ResourceRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryResourceService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="resourceRepository">资源仓储</param>
        public QueryResourceService( ISqlQuery sqlQuery, IResourceRepository resourceRepository )
        {
            SqlQuery = sqlQuery;
            ResourceRepository = resourceRepository;
        }
    }
}
