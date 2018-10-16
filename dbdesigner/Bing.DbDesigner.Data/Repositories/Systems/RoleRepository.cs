using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;
using Bing.Utils.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Bing.DbDesigner.Data.Repositories.Systems {
    /// <summary>
    /// 角色仓储
    /// </summary>
    public class RoleRepository : TreeRepositoryBase<Role>, IRoleRepository,IRoleStore<Role> {
        /// <summary>
        /// 初始化角色仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public RoleRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }

        #region Dispose(清理)

        /// <summary>
        /// 清理
        /// </summary>
        public void Dispose()
        {
            UnitOfWork.Dispose();
        }

        #endregion

        #region CreateAsync(创建角色)

        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public async Task<IdentityResult> CreateAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role,cancellationToken);
            await AddAsync(role, cancellationToken);
            return IdentityResult.Success;
        }

        /// <summary>
        /// 验证角色
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        private void ValidateRole(Role role, CancellationToken cancellationToken)
        {
            role.CheckNotNull(nameof(role));
            cancellationToken.ThrowIfCancellationRequested();
        }

        #endregion

        #region UpdateAsync(更新角色)

        /// <summary>
        /// 更新角色
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public async Task<IdentityResult> UpdateAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role,cancellationToken);
            await UpdateAsync(role);
            return IdentityResult.Success;
        }

        #endregion



        public async Task<IdentityResult> DeleteAsync(Role role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<string> GetRoleIdAsync(Role role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<string> GetRoleNameAsync(Role role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task SetRoleNameAsync(Role role, string roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<string> GetNormalizedRoleNameAsync(Role role, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task SetNormalizedRoleNameAsync(Role role, string normalizedName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<Role> FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<Role> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override async Task<int> GenerateSortIdAsync(Guid? parentId)
        {
            throw new NotImplementedException();
        }

        #region AddUserRolesAsync(添加用户角色列表)

        /// <summary>
        /// 添加用户角色列表
        /// </summary>
        /// <param name="userRoles">用户角色列表</param>
        /// <returns></returns>
        public async Task AddUserRolesAsync(IEnumerable<UserRole> userRoles)
        {
            throw new NotImplementedException();
        }

        #endregion


        /// <summary>
        /// 移除用户角色列表
        /// </summary>
        /// <param name="userRoles">用户角色列表</param>
        public void RemoveUserRoles(IEnumerable<UserRole> userRoles)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 获取用户的角色列表
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <returns></returns>
        public async Task<List<Role>> GetRolesAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 通过角色编码查找
        /// </summary>
        /// <param name="code">角色编码</param>
        /// <returns></returns>
        public async Task<Role> GetByCodeAsync(string code)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 是否存在角色
        /// </summary>
        /// <param name="user">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        /// <returns></returns>
        public async Task<bool> ExistRoleAsync(Guid user, string roleCode)
        {
            throw new NotImplementedException();
        }
    }
}