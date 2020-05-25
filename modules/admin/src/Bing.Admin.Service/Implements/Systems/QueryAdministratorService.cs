using Bing.Applications;
using Bing.Datas.Sql;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 管理员 查询服务
    /// </summary>
    public class QueryAdministratorService : ServiceBase, IQueryAdministratorService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 管理员仓储
        /// </summary>
        protected IAdministratorRepository AdministratorRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryAdministratorService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="administratorRepository">管理员仓储</param>
        public QueryAdministratorService( ISqlQuery sqlQuery, IAdministratorRepository administratorRepository )
        {
            SqlQuery = sqlQuery;
            AdministratorRepository = administratorRepository;
        }
    }
}
