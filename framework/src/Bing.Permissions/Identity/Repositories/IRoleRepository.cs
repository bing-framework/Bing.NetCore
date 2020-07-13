using Bing.Domains.Repositories;
using Bing.Permissions.Identity.Models;

namespace Bing.Permissions.Identity.Repositories
{
    /// <summary>
    /// 角色仓储
    /// </summary>
    /// <typeparam name="TRole">角色类型</typeparam>
    /// <typeparam name="TKey">角色标识类型</typeparam>
    /// <typeparam name="TParentId">角色父标识类型</typeparam>
    public interface IRoleRepository<TRole, in TKey, in TParentId> : ITreeRepository<TRole, TKey, TParentId> where TRole : RoleBase<TRole, TKey, TParentId>
    {
    }
}
