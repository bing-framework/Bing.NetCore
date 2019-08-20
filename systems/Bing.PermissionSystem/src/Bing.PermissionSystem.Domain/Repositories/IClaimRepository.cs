using Bing.Domains.Repositories;
using Bing.PermissionSystem.Domain.Models;

namespace Bing.PermissionSystem.Domain.Repositories {
    /// <summary>
    /// 声明仓储
    /// </summary>
    public interface IClaimRepository : IRepository<Claim> {
    }
}