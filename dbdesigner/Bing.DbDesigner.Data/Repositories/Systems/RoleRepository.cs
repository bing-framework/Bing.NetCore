using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;
using Bing.Utils.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

        #region DeleteAsync(删除角色)

        /// <summary>
        /// 删除角色
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public async Task<IdentityResult> DeleteAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role,cancellationToken);
            await RemoveAsync(role, cancellationToken);
            return IdentityResult.Success;
        }

        #endregion

        #region GetRoleIdAsync(获取角色标识)

        /// <summary>
        /// 获取角色标识
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public Task<string> GetRoleIdAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role,cancellationToken);
            return Task.FromResult(role.Id.SafeString());
        }

        #endregion

        #region GetRoleNameAsync(获取角色名称)

        /// <summary>
        /// 获取角色名称
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public Task<string> GetRoleNameAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role,cancellationToken);
            return Task.FromResult(role.Name);
        }

        #endregion

        #region SetRoleNameAsync(设置角色名称)

        /// <summary>
        /// 设置角色名称
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="roleName">角色名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public Task SetRoleNameAsync(Role role, string roleName, CancellationToken cancellationToken)
        {
            ValidateRole(role,cancellationToken);
            role.Name = roleName;
            return Task.CompletedTask;
        }

        #endregion

        #region GetNormalizedRoleNameAsync(获取标准化角色名称)

        /// <summary>
        /// 获取标准化角色名称
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public Task<string> GetNormalizedRoleNameAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role,cancellationToken);
            return Task.FromResult(role.NormalizedName);
        }

        #endregion

        #region SetNormalizedRoleNameAsync(设置标准化角色名称)

        /// <summary>
        /// 设置标准化角色名称
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="normalizedName">标准化角色名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public Task SetNormalizedRoleNameAsync(Role role, string normalizedName, CancellationToken cancellationToken)
        {
            ValidateRole(role,cancellationToken);
            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }

        #endregion

        #region FindByIdAsync(通过标识获取角色)

        /// <summary>
        /// 通过标识获取角色
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public async Task<Role> FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await FindAsync(roleId.ToGuid(), cancellationToken);
        }

        #endregion

        #region FindByNameAsync(通过名称获取角色)

        /// <summary>
        /// 通过名称获取角色
        /// </summary>
        /// <param name="normalizedRoleName">标准化角色名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public async Task<Role> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await SingleAsync(x => x.NormalizedName == normalizedRoleName, cancellationToken);
        }

        #endregion

        #region AddUserRolesAsync(添加用户角色列表)

        /// <summary>
        /// 添加用户角色列表
        /// </summary>
        /// <param name="userRoles">用户角色列表</param>
        /// <returns></returns>
        public async Task AddUserRolesAsync(IEnumerable<UserRole> userRoles)
        {
            if (userRoles == null)
            {
                return;
            }

            var list = userRoles.ToList();
            foreach (var userRole in list)
            {
                if (await UnitOfWork.Set<UserRole>()
                        .AnyAsync(x => x.RoleId == userRole.RoleId && x.UserId == userRole.UserId) == false)
                {
                    await UnitOfWork.Set<UserRole>().AddAsync(userRole);
                }
            }
        }

        #endregion

        #region RemoveUserRoles(移除用户角色列表)

        /// <summary>
        /// 移除用户角色列表
        /// </summary>
        /// <param name="userRoles">用户角色列表</param>
        public void RemoveUserRoles(IEnumerable<UserRole> userRoles)
        {
            UnitOfWork.Set<UserRole>().RemoveRange(userRoles);
        }

        #endregion

        #region GetRolesAsync(获取用户的角色列表)

        /// <summary>
        /// 获取用户的角色列表
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <returns></returns>
        public async Task<List<Role>> GetRolesAsync(Guid userId)
        {
            return await (from role in Set
                join userRole in UnitOfWork.Set<UserRole>() on role.Id equals userRole.RoleId
                where userRole.UserId == userId
                select role).ToListAsync();
        }

        #endregion

        #region GetByCodeAsync(通过角色编码查找)

        /// <summary>
        /// 通过角色编码查找
        /// </summary>
        /// <param name="code">角色编码</param>
        /// <returns></returns>
        public async Task<Role> GetByCodeAsync(string code)
        {
            return await UnitOfWork.Set<Role>().Where(x => x.Code == code).FirstOrDefaultAsync();
        }

        #endregion

        #region ExistRoleAsync(是否存在角色)

        /// <summary>
        /// 是否存在角色
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="roleCode">角色编码</param>
        /// <returns></returns>
        public async Task<bool> ExistRoleAsync(Guid userId, string roleCode)
        {
            var count = await (from role in Set
                join userRole in UnitOfWork.Set<UserRole>() on role.Id equals userRole.RoleId
                where userRole.UserId == userId && role.Code == roleCode
                select role).CountAsync();
            return count > 0;
        }

        #endregion

    }
}