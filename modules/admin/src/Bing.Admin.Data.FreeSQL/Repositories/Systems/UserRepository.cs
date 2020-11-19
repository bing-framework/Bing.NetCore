using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Domain.Repositories;
using Bing.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 用户 仓储
    /// </summary>
    public class UserRepository : RepositoryBase<User>, IUserRepository, IUserPasswordStore<User>, IUserSecurityStampStore<User>, IUserLockoutStore<User>, IUserEmailStore<User>, IUserPhoneNumberStore<User>, IQueryableUserStore<User>
    {
        /// <summary>
        /// 初始化一个<see cref="UserRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public UserRepository(IAdminUnitOfWork unitOfWork) : base(unitOfWork) { }

        #region Dispose(清理)

        /// <summary>
        /// 清理
        /// </summary>
        public void Dispose() => UnitOfWork.Dispose();

        #endregion

        #region GetUserIdAsync(获取用户标识)

        /// <summary>
        /// 获取用户标识
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.Id.SafeString());
        }

        /// <summary>
        /// 验证用户
        /// </summary>
        private void ValidateUser(User user, CancellationToken cancellationToken)
        {
            user.CheckNull(nameof(user));
            cancellationToken.ThrowIfCancellationRequested();
        }

        #endregion

        #region GetUserNameAsync(获取用户名)

        /// <summary>
        /// 获取用户名
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetUserNameAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.UserName);
        }

        #endregion

        #region SetUserNameAsync(设置用户名)

        /// <summary>
        /// 设置用户名
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="userName">用户名</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetUserNameAsync(User user, string userName, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.UserName = userName;
            return Task.CompletedTask;
        }

        #endregion

        #region GetNormalizedUserNameAsync(获取标准化用户名)

        /// <summary>
        /// 获取标准化用户名
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.NormalizedUserName);
        }

        #endregion

        #region SetNormalizedUserNameAsync(设置标准化用户名)

        /// <summary>
        /// 设置标准化用户名
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="normalizedName">标准化用户名</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetNormalizedUserNameAsync(User user, string normalizedName, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        #endregion

        #region CreateAsync(创建用户)

        /// <summary>
        /// 创建用户
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            Add(user);
            return Task.FromResult(IdentityResult.Success);
        }

        #endregion

        #region UpdateAsync(更新用户)

        /// <summary>
        /// 更新用户
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            await UpdateAsync(user);
            return IdentityResult.Success;
        }

        #endregion

        #region DeleteAsync(删除用户)

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
        {
            await RemoveAsync(user, cancellationToken);
            return IdentityResult.Success;
        }

        #endregion

        #region FindByIdAsync(根据标识查找)

        /// <summary>
        /// 根据标识查找
        /// </summary>
        /// <param name="userId">用户编号</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<User> FindByIdAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return FindAsync(userId.ToGuid(), cancellationToken);
        }

        #endregion

        #region FindByNameAsync(通过用户名查找)

        /// <summary>
        /// 通过用户名查找
        /// </summary>
        /// <param name="normalizedUserName">标准化用户名</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<User> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SingleAsync(t => t.NormalizedUserName == normalizedUserName, cancellationToken);
        }

        #endregion

        #region SetPasswordHashAsync(设置密码散列)

        /// <summary>
        /// 设置密码散列
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="passwordHash">密码散列</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetPasswordHashAsync(User user, string passwordHash, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        #endregion

        #region GetPasswordHashAsync(获取密码散列)

        /// <summary>
        /// 获取密码散列
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetPasswordHashAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.PasswordHash);
        }

        #endregion

        #region HasPasswordAsync(是否设置密码)

        /// <summary>
        /// 是否设置密码
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.PasswordHash.IsEmpty() == false);
        }

        #endregion

        #region SetSecurityStampAsync(设置安全戳)

        /// <summary>
        /// 设置安全戳
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="stamp">安全戳</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            if (string.IsNullOrWhiteSpace(stamp))
                throw new ArgumentNullException(nameof(stamp));
            user.SecurityStamp = stamp;
            return Task.CompletedTask;
        }

        #endregion

        #region GetSecurityStampAsync(获取安全戳)

        /// <summary>
        /// 获取安全戳
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetSecurityStampAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.SecurityStamp);
        }

        #endregion

        #region GetLockoutEndDateAsync(获取锁定结束日期)

        /// <summary>
        /// 获取锁定结束日期
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.LockoutEnd);
        }

        #endregion

        #region SetLockoutEndDateAsync(设置锁定结束日期)

        /// <summary>
        /// 设置锁定结束日期
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="lockoutEnd">锁定结束日期</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.LockoutEnd = lockoutEnd;
            return Task.CompletedTask;
        }

        #endregion

        #region IncrementAccessFailedCountAsync(增加访问失败次数)

        /// <summary>
        /// 增加访问失败次数
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.AccessFailedCount++;
            return Task.FromResult(user.AccessFailedCount);
        }

        #endregion

        #region ResetAccessFailedCountAsync(重置访问失败次数)

        /// <summary>
        /// 重置访问失败次数
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task ResetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.AccessFailedCount = 0;
            return Task.CompletedTask;
        }

        #endregion

        #region GetAccessFailedCountAsync(获取登录失败次数)

        /// <summary>
        /// 获取登录失败次数
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.AccessFailedCount);
        }

        #endregion

        #region GetLockoutEnabledAsync()

        /// <summary>
        /// 获取锁定启用状态
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.LockoutEnabled);
        }

        #endregion

        #region SetLockoutEnabledAsync(设置锁定启用状态)

        /// <summary>
        /// 设置锁定启用状态
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="enabled">是否启用锁定</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.LockoutEnabled = enabled;
            return Task.CompletedTask;
        }

        #endregion

        #region SetEmailAsync(设置电子邮件)

        /// <summary>
        /// 设置电子邮件
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="email">电子邮件</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetEmailAsync(User user, string email, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.Email = email;
            return Task.CompletedTask;
        }

        #endregion

        #region GetEmailAsync(获取电子邮件)

        /// <summary>
        /// 获取电子邮件
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetEmailAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.Email);
        }

        #endregion

        #region GetEmailConfirmedAsync(获取电子邮件确认状态)

        /// <summary>
        /// 获取电子邮件确认状态
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.EmailConfirmed);
        }

        #endregion

        #region SetEmailConfirmedAsync(确认电子邮件)

        /// <summary>
        /// 确认电子邮件
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="confirmed">是否确认</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.EmailConfirmed = confirmed;
            return Task.CompletedTask;
        }

        #endregion

        #region FindByEmailAsync(通过电子邮件查找)

        /// <summary>
        /// 通过电子邮件查找
        /// </summary>
        /// <param name="normalizedEmail">标准化电子邮件</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<User> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await SingleAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        }

        #endregion

        #region GetNormalizedEmailAsync(获取标准化电子邮件)

        /// <summary>
        /// 获取标准化电子邮件
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.NormalizedEmail);
        }

        #endregion

        #region SetNormalizedEmailAsync(设置标准化电子邮件)

        /// <summary>
        /// 设置标准化电子邮件
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="normalizedEmail">标准化电子邮件</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetNormalizedEmailAsync(User user, string normalizedEmail, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.NormalizedEmail = normalizedEmail;
            return Task.CompletedTask;
        }

        #endregion

        #region SetPhoneNumberAsync(设置手机号)

        /// <summary>
        /// 设置手机号
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="phoneNumber">手机号</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetPhoneNumberAsync(User user, string phoneNumber, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.PhoneNumber = phoneNumber;
            return Task.CompletedTask;
        }

        #endregion

        #region GetPhoneNumberAsync(获取手机号)

        /// <summary>
        /// 获取手机号
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<string> GetPhoneNumberAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.PhoneNumber);
        }

        #endregion

        #region GetPhoneNumberConfirmedAsync(获取手机号确认状态)

        /// <summary>
        /// 获取手机号确认状态
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<bool> GetPhoneNumberConfirmedAsync(User user, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            return Task.FromResult(user.PhoneNumberConfirmed);
        }

        #endregion

        #region SetPhoneNumberConfirmedAsync(设置手机号确认状态)

        /// <summary>
        /// 设置手机号确认状态
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="confirmed">是否确认</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task SetPhoneNumberConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
        {
            ValidateUser(user, cancellationToken);
            user.PhoneNumberConfirmed = confirmed;
            return Task.CompletedTask;
        }

        #endregion

        #region FilterByRole(过滤角色)

        /// <summary>
        /// 过滤角色
        /// </summary>
        /// <param name="queryable">查询对象</param>
        /// <param name="roleId">角色标识</param>
        /// <param name="except">是否排除该角色</param>
        public IQueryable<User> FilterByRole(IQueryable<User> queryable, Guid roleId, bool except = false)
        {
            if (roleId.IsEmpty())
                return queryable;
            var selectedUsers = from user in queryable
                                join userRole in UnitOfWork.Set<UserRole>().Select.AsQueryable() on user.Id equals userRole.UserId
                                where userRole.RoleId == roleId
                                select user;
            if (except)
                return queryable.Except(selectedUsers);
            return selectedUsers;
        }

        #endregion

        #region FindByAccount(通过账号查找)

        /// <summary>
        /// 通过账号查找
        /// </summary>
        /// <param name="account">账号</param>
        public async Task<User> FindByAccount(string account) => await Set.Where((x => x.UserName == account)).ToOneAsync();

        #endregion

        /// <summary>
        /// 用户
        /// </summary>
        public IQueryable<User> Users => this.FindAsNoTracking();
    }
}
