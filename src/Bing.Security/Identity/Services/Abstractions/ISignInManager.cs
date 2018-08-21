using System.Threading.Tasks;
using Bing.Domains.Services;
using Bing.Security.Identity.Models;
using Bing.Security.Identity.Results;

namespace Bing.Security.Identity.Services.Abstractions
{
    /// <summary>
    /// 登录服务
    /// </summary>
    /// <typeparam name="TUser">用户类型</typeparam>
    /// <typeparam name="TKey">用户标识类型</typeparam>
    public interface ISignInManager<in TUser,TKey> :IDomainService where TUser:User<TUser,TKey>
    {
        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="account">账号，可以是用户名、手机号或电子邮件</param>
        /// <param name="password">密码</param>
        /// <param name="isPersistent">cookie是否持久保留，设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        Task<SignInResult> SignInAsync(string account, string password, bool isPersistent = false,
            bool lockoutOnFailure = true, string applicationCode = "");

        /// <summary>
        /// 用户名登录
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="isPersistent">cookie是否持久保留，设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        Task<SignInResult> SignInByUserNameAsync(string userName, string password, bool isPersistent = false,
            bool lockoutOnFailure = true, string applicationCode = "");

        /// <summary>
        /// 电子邮件登录
        /// </summary>
        /// <param name="email">电子邮件</param>
        /// <param name="password">密码</param>
        /// <param name="isPersistent">cookie是否持久保留，设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        Task<SignInResult> SignInByEmailAsync(string email, string password, bool isPersistent = false,
            bool lockoutOnFailure = true, string applicationCode = "");

        /// <summary>
        /// 手机号登录
        /// </summary>
        /// <param name="phone">手机号</param>
        /// <param name="password">密码</param>
        /// <param name="isPersistent">cookie是否持久保留，设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        Task<SignInResult> SignInByPhoneAsync(string phone, string password, bool isPersistent = false,
            bool lockoutOnFailure = true, string applicationCode = "");

        /// <summary>
        /// 退出登录
        /// </summary>
        /// <returns></returns>
        Task SignOutAsync();
    }
}
