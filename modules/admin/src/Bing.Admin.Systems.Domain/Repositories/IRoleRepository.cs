using System;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Systems.Domain.Repositories
{
    /// <summary>
    /// 角色仓储
    /// </summary>
    public interface IRoleRepository : Bing.Permissions.Identity.Repositories.IRoleRepository<Role, Guid, Guid?>
    {
    }
}
