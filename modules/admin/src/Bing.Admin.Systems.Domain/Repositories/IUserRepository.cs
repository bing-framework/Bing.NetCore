using System;
using System.Linq;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Systems.Domain.Repositories
{
    /// <summary>
    /// 用户仓储
    /// </summary>
    public interface IUserRepository : Bing.Permissions.Identity.Repositories.IUserRepository<User, Guid>
    {
        /// <summary>
        /// 过滤角色
        /// </summary>
        /// <param name="queryable">查询对象</param>
        /// <param name="roleId">角色标识</param>
        /// <param name="except">是否排除该角色</param>
        IQueryable<User> FilterByRole(IQueryable<User> queryable, Guid roleId, bool except = false);
    }
}
