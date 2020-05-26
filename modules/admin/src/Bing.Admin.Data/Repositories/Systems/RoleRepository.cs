using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Datas.EntityFramework.Core;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 角色 仓储
    /// </summary>
    public class RoleRepository : TreeRepositoryBase<Role>, IRoleRepository, IRoleStore<Role>
    {
        /// <summary>
        /// 初始化一个<see cref="RoleRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public RoleRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }

        #region Dispose(清理)

        /// <summary>
        /// 清理
        /// </summary>
        public void Dispose()
        {
            UnitOfWork.Dispose();
        }

        #endregion Dispose(清理)

        #region CreateAsync(创建角色)

        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<IdentityResult> CreateAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role, cancellationToken);
            await AddAsync(role, cancellationToken);
            return IdentityResult.Success;
        }

        /// <summary>
        /// 验证用户
        /// </summary>
        private void ValidateRole(Role role, CancellationToken cancellationToken)
        {
            role.CheckNull(nameof(role));
            cancellationToken.ThrowIfCancellationRequested();
        }

        #endregion CreateAsync(创建角色)

        #region UpdateAsync(更新角色)

        /// <summary>
        /// 更新角色
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<IdentityResult> UpdateAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role, cancellationToken);
            await UpdateAsync(role);
            return IdentityResult.Success;
        }

        #endregion UpdateAsync(更新角色)

        #region DeleteAsync(删除角色)

        /// <summary>
        /// 删除角色
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<IdentityResult> DeleteAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role, cancellationToken);
            await RemoveAsync(role, cancellationToken);
            return IdentityResult.Success;
        }

        #endregion DeleteAsync(删除角色)

        #region GetRoleIdAsync(获取角色编号)

        /// <summary>
        /// 获取角色编号
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetRoleIdAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role, cancellationToken);
            return Task.FromResult(role.Id.SafeString());
        }

        #endregion GetRoleIdAsync(获取角色编号)

        #region GetRoleNameAsync(获取角色名称)

        /// <summary>
        /// 获取角色名称
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetRoleNameAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role, cancellationToken);
            return Task.FromResult(role.Name);
        }

        #endregion GetRoleNameAsync(获取角色名称)

        #region SetRoleNameAsync(设置角色名称)

        /// <summary>
        /// 设置角色名称
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="roleName">角色名</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetRoleNameAsync(Role role, string roleName, CancellationToken cancellationToken)
        {
            ValidateRole(role, cancellationToken);
            role.Name = roleName;
            return Task.CompletedTask;
        }

        #endregion SetRoleNameAsync(设置角色名称)

        #region GetNormalizedRoleNameAsync(获取标准化角色名称)

        /// <summary>
        /// 获取标准化角色名称
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetNormalizedRoleNameAsync(Role role, CancellationToken cancellationToken)
        {
            ValidateRole(role, cancellationToken);
            return Task.FromResult(role.NormalizedName);
        }

        #endregion GetNormalizedRoleNameAsync(获取标准化角色名称)

        #region SetNormalizedRoleNameAsync(设置标准化角色名称)

        /// <summary>
        /// 设置标准化角色名称
        /// </summary>
        /// <param name="role">角色</param>
        /// <param name="normalizedName">标准化角色名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetNormalizedRoleNameAsync(Role role, string normalizedName, CancellationToken cancellationToken)
        {
            ValidateRole(role, cancellationToken);
            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }

        #endregion SetNormalizedRoleNameAsync(设置标准化角色名称)

        #region FindByIdAsync(通过编号获取角色)

        /// <summary>
        /// 通过编号获取角色
        /// </summary>
        /// <param name="roleId">角色编号</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<Role> FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await FindAsync(roleId.ToGuid(), cancellationToken);
        }

        #endregion FindByIdAsync(通过编号获取角色)

        #region FindByNameAsync(通过名称获取角色)

        /// <summary>
        /// 通过名称获取角色
        /// </summary>
        /// <param name="normalizedRoleName">标准化角色名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<Role> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await SingleAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);
        }

        #endregion FindByNameAsync(通过名称获取角色)

        #region GetRolesAsync(获取用户的角色列表)

        /// <summary>
        /// 获取用户的角色列表
        /// </summary>
        /// <param name="userId">用户标识</param>
        public async Task<List<Role>> GetRolesAsync(Guid userId) => await GetRoleQueryable(userId).ToListAsync();

        /// <summary>
        /// 获取角色查询对象
        /// </summary>
        /// <param name="userId">用户标识</param>
        private IQueryable<Role> GetRoleQueryable(Guid userId)
        {
            return from role in Set
                   join userRole in UnitOfWork.Set<UserRole>() on role.Id equals userRole.RoleId
                   where userRole.UserId == userId
                   select role;
        }

        #endregion

        #region GetRoleIdsAsync(获取用户的角色标识列表)

        /// <summary>
        /// 获取用户的角色标识列表
        /// </summary>
        /// <param name="userId">用户标识</param>
        public async Task<List<Guid>> GetRoleIdsAsync(Guid userId) => await GetRoleQueryable(userId).Select(t => t.Id).ToListAsync();

        #endregion

        #region GetExistsUserIdsAsync(获取已添加的用户标识列表)

        /// <summary>
        /// 获取已添加的用户标识列表
        /// </summary>
        /// <param name="roleId">角色标识</param>
        /// <param name="userIds">用户标识列表</param>
        public async Task<List<Guid>> GetExistsUserIdsAsync(Guid roleId, List<Guid> userIds) =>
            await UnitOfWork.Set<UserRole>()
                .Where(t => t.RoleId == roleId && userIds.Contains(t.UserId))
                .Select(t => t.UserId)
                .ToListAsync();

        #endregion

        #region AddUserRolesAsync(添加用户角色列表)

        /// <summary>
        /// 添加用户角色列表
        /// </summary>
        /// <param name="userRoles">用户角色集合</param>
        public async Task AddUserRolesAsync(IEnumerable<UserRole> userRoles)
        {
            if (userRoles == null)
                return;
            await UnitOfWork.Set<UserRole>().AddRangeAsync(userRoles);
        }

        #endregion

        #region RemoveUserRoles(移除用户角色列表)

        /// <summary>
        /// 移除用户角色列表
        /// </summary>
        /// <param name="userRoles">用户角色集合</param>
        public void RemoveUserRoles(IEnumerable<UserRole> userRoles) => UnitOfWork.Set<UserRole>().RemoveRange(userRoles);

        #endregion
    }
}
