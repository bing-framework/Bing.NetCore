using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Domains.Repositories;
using Bing.DbDesigner.Systems.Domain.Models;

namespace Bing.DbDesigner.Systems.Domain.Repositories {
    /// <summary>
    /// 权限仓储
    /// </summary>
    public interface IPermissionRepository : IRepository<Permission>
    {
        /// <summary>
        /// 通过角色标识获取权限列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="isDeny">是否拒绝</param>
        /// <returns></returns>
        Task<List<Permission>> GetPermissionsAsync(Guid roleId, bool isDeny);

        /// <summary>
        /// 通过角色标识获取资源列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="isDeny">是否拒绝</param>
        /// <param name="applicationId">应用程序标识</param>
        /// <returns></returns>
        Task<List<Resource>> GetResoucesAsync(Guid roleId, bool isDeny, Guid? applicationId);

        /// <summary>
        /// 获取权限标识列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="resouceIds">资源标识列表</param>
        /// <returns></returns>
        Task<List<Guid>> GetPermissionIdsAsync(Guid roleId, List<Guid> resouceIds);

        /// <summary>
        /// 移除权限
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="resourceIds">资源标识列表</param>
        /// <returns></returns>
        Task RemoveAsync(Guid roleId, List<Guid> resourceIds);
    }
}