using Bing.Domains.Repositories;
using Bing.PermissionSystem.Domain.Models;

namespace Bing.PermissionSystem.Domain.Repositories {
    /// <summary>
    /// 权限仓储
    /// </summary>
    public interface IPermissionRepository : IRepository<Permission> {
    }
}