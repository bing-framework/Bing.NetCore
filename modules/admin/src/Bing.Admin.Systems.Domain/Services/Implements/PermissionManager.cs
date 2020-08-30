using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Domains.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 权限 管理
    /// </summary>
    public class PermissionManager : DomainServiceBase, IPermissionManager
    {
        /// <summary>
        /// 权限仓储
        /// </summary>
        protected IPermissionRepository PermissionRepository { get; set; }

        /// <summary>
        /// 初始化一个<see cref="PermissionManager"/>类型的实例
        /// </summary>
        public PermissionManager(IPermissionRepository permissionRepository)
        {
            PermissionRepository = permissionRepository;
        }

        /// <summary>
        /// 保存权限
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleId">角色标识</param>
        /// <param name="resourceIds">资源标识列表</param>
        /// <param name="isDeny">是否拒绝</param>
        public async Task SaveAsync(Guid applicationId, Guid roleId, List<Guid> resourceIds, bool isDeny)
        {
            if (resourceIds == null)
                return;
            var oldResourceIds = await PermissionRepository.GetResourceIdsAsync(applicationId, roleId, isDeny);
            var result = resourceIds.Compare(oldResourceIds);
            await PermissionRepository.AddAsync(ToPermissions(roleId, result.CreateList, isDeny));
            await PermissionRepository.RemoveAsync(roleId, result.DeleteList);
        }

        /// <summary>
        /// 转换为权限实体列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="resourceIds">资源标识列表</param>
        /// <param name="isDeny">是否拒绝</param>
        private List<Permission> ToPermissions(Guid roleId, List<Guid> resourceIds, bool isDeny) => resourceIds.Select(resourceId => ToPermission(roleId, resourceId, isDeny)).ToList();

        /// <summary>
        /// 转换为权限实体
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="resourceId">资源标识</param>
        /// <param name="isDeny">是否拒绝</param>
        private Permission ToPermission(Guid roleId, Guid resourceId, bool isDeny)
        {
            var result = new Permission()
            {
                RoleId = roleId,
                ResourceId = resourceId,
                IsDeny = isDeny
            };
            result.Init();
            return result;
        }
    }
}
