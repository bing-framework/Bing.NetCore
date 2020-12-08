using System.Threading.Tasks;
using Bing.Extensions;
using Bing.Permissions.Identity.Models;
using Bing.Permissions.Identity.Results;
using Bing.Permissions.Identity.Services.Abstractions;
using Bing.Permissions.Properties;

namespace Bing.Permissions.Identity.Services.Implements
{
    /// <summary>
    /// 登录服务
    /// </summary>
    /// <typeparam name="TUser">用户类型</typeparam>
    /// <typeparam name="TKey">用户标识类型</typeparam>
    public class SignInManager<TUser, TKey> : ISignInManager<TUser, TKey> where TUser : UserBase<TUser, TKey>
    {
        /// <summary>
        /// Identity登录管理
        /// </summary>
        protected Microsoft.AspNetCore.Identity.SignInManager<TUser> IdentitySignInManager { get; }

        /// <summary>
        /// 用户管理
        /// </summary>
        protected IUserManager<TUser, TKey> UserManager { get; }

        /// <summary>
        /// 初始化一个<see cref="SignInManager{TUser,TKey}"/>类型的实例
        /// </summary>
        /// <param name="identitySignInManager">Identity登录管理</param>
        /// <param name="userManager">用户管理</param>
        public SignInManager(Microsoft.AspNetCore.Identity.SignInManager<TUser> identitySignInManager
            , IUserManager<TUser, TKey> userManager)
        {
            IdentitySignInManager = identitySignInManager;
            UserManager = userManager;
        }

        #region SignInAsync(登录)

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="password">密码</param>
        /// <param name="isPersistent">cookie是否持久保留。设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        public virtual async Task<SignInResult> SignInAsync(TUser user, string password, bool isPersistent = false, bool lockoutOnFailure = true)
        {
            if (user == null)
                return new SignInResult(SignInState.Failed, null, SecurityResources.InvalidAccountOrPassword);
            return await PasswordSignIn(user, password, isPersistent, lockoutOnFailure);
        }

        /// <summary>
        /// 密码登录
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="password">密码</param>
        ///  <param name="isPersistent">cookie是否持久保留，设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        private async Task<SignInResult> PasswordSignIn(TUser user, string password, bool isPersistent = false,
            bool lockoutOnFailure = true)
        {
            var signInResult =
                await IdentitySignInManager.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);
            if (signInResult.IsNotAllowed)
                return new SignInResult(SignInState.Failed, null, SecurityResources.UserIsDisabled);
            if (signInResult.IsLockedOut)
                return new SignInResult(SignInState.Failed, null, SecurityResources.LoginFailLock);
            if (signInResult.Succeeded)
                return new SignInResult(SignInState.Succeeded, user.Id.SafeString());
            if (signInResult.RequiresTwoFactor)
                return new SignInResult(SignInState.TwoFactor, user.Id.SafeString());
            return new SignInResult(SignInState.Failed, null, SecurityResources.InvalidAccountOrPassword);
        }

        #endregion

        #region SignOutAsync(退出登录)

        /// <summary>
        /// 退出登录
        /// </summary>
        public virtual async Task SignOutAsync() => await IdentitySignInManager.SignOutAsync();

        #endregion
    }
}
