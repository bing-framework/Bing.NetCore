using System.Threading.Tasks;
using Bing.Domains.Services;
using Bing.Permissions.Identity.Models;
using Bing.Permissions.Identity.Results;

namespace Bing.Permissions.Identity.Services.Abstractions
{
    /// <summary>
    /// 登录管理
    /// </summary>
    /// <typeparam name="TUser">用户类型</typeparam>
    /// <typeparam name="TKey">用户标识类型</typeparam>
    public interface ISignInManager<in TUser, TKey> : IDomainService where TUser : UserBase<TUser, TKey>
    {
        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="password">密码</param>
        /// <param name="isPersistent">cookie是否持久保留。设置为false，当关闭浏览器则cookie失效</param>
        /// <param name="lockoutOnFailure">达到登录失败次数是否锁定</param>
        Task<SignInResult> SignInAsync(TUser user, string password, bool isPersistent = false,
            bool lockoutOnFailure = true);

        /// <summary>
        /// 退出登录
        /// </summary>
        Task SignOutAsync();
    }
}
