using Bing.Applications;
using Bing.Datas.Sql;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 权限 查询服务
    /// </summary>
    public class QueryPermissionService : Bing.Application.Services.ApplicationServiceBase, IQueryPermissionService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 权限仓储
        /// </summary>
        protected IPermissionRepository PermissionRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryPermissionService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="permissionRepository">权限仓储</param>
        public QueryPermissionService( ISqlQuery sqlQuery, IPermissionRepository permissionRepository )
        {
            SqlQuery = sqlQuery;
            PermissionRepository = permissionRepository;
        }
    }
}
