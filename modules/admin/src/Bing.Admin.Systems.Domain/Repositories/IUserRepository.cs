using System;
using Bing.Domains.Repositories;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Systems.Domain.Repositories
{
    /// <summary>
    /// 用户仓储
    /// </summary>
    public interface IUserRepository : Bing.Permissions.Identity.Repositories.IUserRepository<User, Guid>
    {
    }
}
