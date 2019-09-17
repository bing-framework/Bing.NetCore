using System.Threading.Tasks;
using Bing.Domains.Services;
using Bing.Security.Identity.Models;

namespace Bing.Security.Identity.Services.Abstractions
{
    /// <summary>
    /// 角色服务
    /// </summary>
    /// <typeparam name="TRole">角色类型</typeparam>
    /// <typeparam name="TKey">角色标识类型</typeparam>
    /// <typeparam name="TParentId">角色父标识类型</typeparam>
    public interface IRoleManager<TRole,in TKey,TParentId>:IDomainService where TRole:RoleBase<TRole,TKey,TParentId>
    {
        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="role">角色</param>
        /// <returns></returns>
        Task CreateAsync(TRole role);

        /// <summary>
        /// 修改角色
        /// </summary>
        /// <param name="role">角色</param>
        /// <returns></returns>
        Task UpdateAsync(TRole role);
    }
}
