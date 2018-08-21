using System.Threading.Tasks;
using Bing.Exceptions;
using Bing.Security.Identity.Models;
using Bing.Security.Identity.Results;
using Bing.Security.Identity.Services.Abstractions;
using Bing.Security.Properties;
using Bing.Utils.Extensions;

namespace Bing.Security.Identity.Services.Implements
{
    /// <summary>
    /// 登录服务
    /// </summary>
    /// <typeparam name="TUser">用户类型</typeparam>
    /// <typeparam name="TKey">用户标识类型</typeparam>
    public class SignInManager<TUser,TKey>:ISignInManager<TUser,TKey> where TUser:User<TUser,TKey>
    {
        /// <summary>
        /// Identity登录服务
        /// </summary>
        protected IdentitySignInManager<TUser,TKey> IdentitySignInManager { get; }

        /// <summary>
        /// 用户服务
        /// </summary>
        protected IUserManager<TUser,TKey> UserManager { get; }

        /// <summary>
        /// 初始化一个<see cref="SignInManager{TUser,TKey}"/>类型的实例
        /// </summary>
        /// <param name="identitySignInManager">Identity登录服务</param>
        /// <param name="userManager">用户服务</param>
        public SignInManager(IdentitySignInManager<TUser, TKey> identitySignInManager,
            IUserManager<TUser, TKey> userManager)
        {
            IdentitySignInManager = identitySignInManager;
            UserManager = userManager;
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="account">账号，可以是用户名、手机号或电子邮件</param>
        /// <param name="password">密码</param>
        /// <param name="isPersistent">cookie是否持久保留，设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        public async Task<SignInResult> SignInAsync(string account, string password, bool isPersistent = false, bool lockoutOnFailure = true,
            string applicationCode = "")
        {
            var user = await GetUser(account);
            if (user == null)
            {
                throw new Warning(SecurityResources.InvalidAccountOrPassword);
            }
            var result = await PasswordSignIn(user, password, isPersistent, lockoutOnFailure, applicationCode);
            if (result.State == SignInState.Failed)
            {
                throw new Warning(SecurityResources.InvalidAccountOrPassword);
            }
            return result;
        }

        /// <summary>
        /// 获取用户
        /// </summary>
        /// <param name="account">账号</param>
        /// <returns></returns>
        private async Task<TUser> GetUser(string account)
        {
            var user = await UserManager.FindByPhoneAsync(account);
            if (user == null)
            {
                user = await UserManager.FindByNameAsync(account);
            }
            if (user == null)
            {
                user = await UserManager.FindByEmailAsync(account);
            }
            return user;
        }

        /// <summary>
        /// 密码登录
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="password">密码</param>
        ///  <param name="isPersistent">cookie是否持久保留，设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        private async Task<SignInResult> PasswordSignIn(TUser user, string password, bool isPersistent = false,
            bool lockoutOnFailure = true, string applicationCode = "")
        {
            await AddClaimsToUser(user, applicationCode);
            var signInResult =
                await IdentitySignInManager.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);
            if (signInResult.IsNotAllowed)
            {
                throw new Warning(SecurityResources.UserIsDisabled);
            }
            if (signInResult.IsLockedOut)
            {
                throw new Warning(SecurityResources.LoginFailLock);
            }
            if (signInResult.Succeeded)
            {
                return new SignInResult(SignInState.Succeeded,user.Id.SafeString());
            }
            if (signInResult.RequiresTwoFactor)
            {
                return new SignInResult(SignInState.TwoFactor,user.Id.SafeString());
            }
            return new SignInResult(SignInState.Failed,string.Empty);
        }

        /// <summary>
        /// 添加声明到用户
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        protected virtual Task AddClaimsToUser(TUser user, string applicationCode)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 用户名登录
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="isPersistent">cookie是否持久保留，设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        public async Task<SignInResult> SignInByUserNameAsync(string userName, string password, bool isPersistent = false, bool lockoutOnFailure = true,
            string applicationCode = "")
        {
            var user = await UserManager.FindByNameAsync(userName);
            if (user == null)
            {
                throw new Warning(SecurityResources.InvalidUserNameOrPassword);
            }
            var result = await PasswordSignIn(user, password, isPersistent, lockoutOnFailure, applicationCode);
            if (result.State == SignInState.Failed)
            {
                throw new Warning(SecurityResources.InvalidUserNameOrPassword);
            }
            return result;
        }

        /// <summary>
        /// 电子邮件登录
        /// </summary>
        /// <param name="email">电子邮件</param>
        /// <param name="password">密码</param>
        /// <param name="isPersistent">cookie是否持久保留，设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        public async Task<SignInResult> SignInByEmailAsync(string email, string password, bool isPersistent = false, bool lockoutOnFailure = true,
            string applicationCode = "")
        {
            var user = await UserManager.FindByEmailAsync(email);
            if (user == null)
            {
                throw new Warning(SecurityResources.InvalidEmailOrPassword);
            }
            var result = await PasswordSignIn(user, password, isPersistent, lockoutOnFailure, applicationCode);
            if (result.State == SignInState.Failed)
            {
                throw new Warning(SecurityResources.InvalidEmailOrPassword);
            }
            return result;
        }

        /// <summary>
        /// 手机号登录
        /// </summary>
        /// <param name="phone">手机号</param>
        /// <param name="password">密码</param>
        /// <param name="isPersistent">cookie是否持久保留，设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        public async Task<SignInResult> SignInByPhoneAsync(string phone, string password, bool isPersistent = false, bool lockoutOnFailure = true,
            string applicationCode = "")
        {
            var user = await UserManager.FindByPhoneAsync(phone);
            if (user == null)
            {
                throw new Warning(SecurityResources.InvalidPhoneOrPassword);
            }
            var result = await PasswordSignIn(user, password, isPersistent, lockoutOnFailure, applicationCode);
            if (result.State == SignInState.Failed)
            {
                throw new Warning(SecurityResources.InvalidPhoneOrPassword);
            }
            return result;
        }

        /// <summary>
        /// 退出登录
        /// </summary>
        /// <returns></returns>
        public async Task SignOutAsync()
        {
            await IdentitySignInManager.SignOutAsync();
        }
    }
}
