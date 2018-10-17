using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

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
            throw new NotImplementedException();
        }

        #endregion


        /// <summary>
        /// 获取权限标识列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="resouceIds">资源标识列表</param>
        /// <returns></returns>
        public async Task<List<Guid>> GetPermissionIdsAsync(Guid roleId, List<Guid> resouceIds)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除权限
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="resourceIds">资源标识列表</param>
        /// <returns></returns>
        public async Task RemoveAsync(Guid roleId, List<Guid> resourceIds)
        {
            throw new NotImplementedException();
        }
    }
}