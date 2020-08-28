using Bing.Applications;
using Bing.Datas.Sql;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 应用程序 查询服务
    /// </summary>
    public class QueryApplicationService : Bing.Application.Services.AppServiceBase, IQueryApplicationService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 应用程序仓储
        /// </summary>
        protected IApplicationRepository ApplicationRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryApplicationService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="applicationRepository">应用程序仓储</param>
        public QueryApplicationService( ISqlQuery sqlQuery, IApplicationRepository applicationRepository )
        {
            SqlQuery = sqlQuery;
            ApplicationRepository = applicationRepository;
        }
    }
}
