using System;
using System.Linq;
using Bing.Domains.Repositories;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Security.Identity.Repositories;

namespace Bing.DbDesigner.Systems.Domain.Repositories {
    /// <summary>
    /// 用户仓储
    /// </summary>
    public interface IUserRepository : IUserRepository<User, Guid>
    {
        /// <summary>
        /// 过滤角色
        /// </summary>
        /// <param name="queryable">查询对象</param>
        /// <param name="roleId">角色标识</param>
        /// <param name="except">是否排除该角色</param>
        /// <returns></returns>
        IQueryable<User> FilterRole(IQueryable<User> queryable, Guid roleId, bool except = false);
    }
}