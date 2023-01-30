using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 权限 仓储
    /// </summary>
    public class PermissionRepository : RepositoryBase<Permission>, IPermissionRepository
    {
        /// <summary>
        /// 初始化一个<see cref="PermissionRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public PermissionRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }

        #region GetResourceIdsAsync(获取资源标识列表)

        /// <summary>
        /// 获取资源标识列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleId">角色标识</param>
        /// <param name="isDeny">是否拒绝</param>
        public Task<List<Guid>> GetResourceIdsAsync(Guid applicationId, Guid roleId, bool isDeny)
        {
            var queryable = from permission in Set.Select.AsQueryable()
                join resource in UnitOfWork.Set<ResourcePo>().Select.AsQueryable() on permission.ResourceId equals resource.Id
                where resource.ApplicationId == applicationId && permission.RoleId == roleId &&
                      permission.IsDeny == isDeny
                select resource.Id;
            return Task.FromResult(queryable.ToList());

        }

        #endregion

        #region GetPermissionIdsAsync(获取权限标识列表)

        /// <summary>
        /// 获取权限标识列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="resourceIds">资源标识列表</param>
        public Task<List<Guid>> GetPermissionIdsAsync(Guid roleId, List<Guid> resourceIds) =>
            Task.FromResult(Find()
                .Where(t => t.RoleId == roleId && resourceIds.Contains(t.ResourceId))
                .Select(t => t.Id)
                .ToList());

        #endregion

        #region RemoveAsync(移除权限)

        /// <summary>
        /// 移除权限
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="resourceIds">资源标识列表</param>
        public async Task RemoveAsync(Guid roleId, List<Guid> resourceIds)
        {
            var permissionIds = await GetPermissionIdsAsync(roleId, resourceIds);
            await RemoveAsync(permissionIds);
        }

        #endregion
    }
}
