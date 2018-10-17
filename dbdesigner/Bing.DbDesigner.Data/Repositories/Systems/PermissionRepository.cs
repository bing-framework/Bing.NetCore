using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;
using Microsoft.EntityFrameworkCore;

namespace Bing.DbDesigner.Data.Repositories.Systems {
    /// <summary>
    /// 权限仓储
    /// </summary>
    public class PermissionRepository : RepositoryBase<Permission>, IPermissionRepository {
        /// <summary>
        /// 初始化权限仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public PermissionRepository(IDbDesignerUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        #region GetPermissionsAsync(通过角色标识获取权限列表)

        /// <summary>
        /// 通过角色标识获取权限列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="isDeny">是否拒绝</param>
        /// <returns></returns>
        public async Task<List<Permission>> GetPermissionsAsync(Guid roleId, bool isDeny)
        {
            return await FindAllAsync(x => x.RoleId == roleId && x.IsDeny == isDeny);
        }

        #endregion

        #region GetResoucesAsync(通过角色标识获取资源列表)

        /// <summary>
        /// 通过角色标识获取资源列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="isDeny">是否拒绝</param>
        /// <param name="applicationId">应用程序标识</param>
        /// <returns></returns>
        public async Task<List<Resource>> GetResoucesAsync(Guid roleId, bool isDeny, Guid? applicationId)
        {
            var queryable = from permission in Find()
                join resource in UnitOfWork.Set<Resource>() on permission.ResourceId equals resource.Id
                where permission.RoleId == roleId && permission.IsDeny == isDeny
                select resource;
            queryable = queryable.WhereIfNotEmpty(x => x.ApplicationId == applicationId);
            var resources = await queryable.ToListAsync();
            return resources;
        }

        #endregion

        #region GetPermissionIdsAsync(获取权限标识列表)

        /// <summary>
        /// 获取权限标识列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="resouceIds">资源标识列表</param>
        /// <returns></returns>
        public async Task<List<Guid>> GetPermissionIdsAsync(Guid roleId, List<Guid> resouceIds)
        {
            return await Find().Where(x => x.RoleId == roleId && resouceIds.Contains(x.ResourceId)).Select(x => x.Id)
                .ToListAsync();
        }

        #endregion

        #region RemoveAsync(移除权限)

        /// <summary>
        /// 移除权限
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="resourceIds">资源标识列表</param>
        /// <returns></returns>
        public async Task RemoveAsync(Guid roleId, List<Guid> resourceIds)
        {
            var permissionIds = await GetPermissionIdsAsync(roleId, resourceIds);
            await RemoveAsync(permissionIds);
        }

        #endregion

    }
}