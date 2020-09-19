using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Data;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 角色 查询服务
    /// </summary>
    public class QueryRoleService : Bing.Application.Services.AppServiceBase, IQueryRoleService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 角色仓储
        /// </summary>
        protected IRoleRepository RoleRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryRoleService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="roleRepository">角色仓储</param>
        public QueryRoleService( ISqlQuery sqlQuery, IRoleRepository roleRepository )
        {
            SqlQuery = sqlQuery;
            RoleRepository = roleRepository;
        }
    }
}
