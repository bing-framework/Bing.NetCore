using Bing.Security.Identity.Options;
using Microsoft.AspNetCore.Identity;

namespace Bing.Security.Identity.Extensions
{
    /// <summary>
    /// 授权配置(<see cref="IdentityOptions"/>) 扩展
    /// </summary>
    public static class IdentityOptionsExtensions
    {
        /// <summary>
        /// 加载权限配置
        /// </summary>
        /// <param name="options">Identity配置</param>
        /// <param name="permissionOptions">权限配置</param>
        public static void Load(this IdentityOptions options, PermissionOptions permissionOptions)
        {
            if (options == null || permissionOptions == null)
            {
                return;
            }

            LoadPassword(options, permissionOptions);
            LoadUser(options, permissionOptions);
            LoadStore(options, permissionOptions);
            LoadSignIn(options, permissionOptions);
            LoadLockout(options, permissionOptions);
        }

        /// <summary>
        /// 加载密码配置
        /// </summary>
        /// <param name="options">Identity配置</param>
        /// <param name="permissionOptions">权限配置</param>
        private static void LoadPassword(IdentityOptions options, PermissionOptions permissionOptions)
        {
            options.Password.RequiredLength = permissionOptions.Password.MinLenght;
            options.Password.RequireNonAlphanumeric = permissionOptions.Password.NonAlphanumeric;
            options.Password.RequireUppercase = permissionOptions.Password.Uppercase;
            options.Password.RequireLowercase = permissionOptions.Password.Lowercase;
            options.Password.RequireDigit = permissionOptions.Password.Digit;
            options.Password.RequiredUniqueChars = permissionOptions.Password.UniqueChars;
        }

        /// <summary>
        /// 加载用户配置
        /// </summary>
        /// <param name="options">Identity配置</param>
        /// <param name="permissionOptions">权限配置</param>
        private static void LoadUser(IdentityOptions options, PermissionOptions permissionOptions)
        {
            options.User.AllowedUserNameCharacters = permissionOptions.User.UserNameCharacters;
            options.User.RequireUniqueEmail = permissionOptions.User.UniqueEmail;
        }

        /// <summary>
        /// 加载存储配置
        /// </summary>
        /// <param name="options">Identity配置</param>
        /// <param name="permissionOptions">权限配置</param>
        private static void LoadStore(IdentityOptions options, PermissionOptions permissionOptions)
        {
            options.Stores.MaxLengthForKeys = permissionOptions.Store.MaxLengthForKeys;
            options.Stores.ProtectPersonalData = permissionOptions.Store.ProtectPersonalData;
        }

        /// <summary>
        /// 加载登录配置
        /// </summary>
        /// <param name="options">Identity配置</param>
        /// <param name="permissionOptions">权限配置</param>
        private static void LoadSignIn(IdentityOptions options, PermissionOptions permissionOptions)
        {
            options.SignIn.RequireConfirmedEmail = permissionOptions.SignIn.ConfirmedEmail;
            options.SignIn.RequireConfirmedPhoneNumber = permissionOptions.SignIn.ConfirmedPhoneNumber;
        }

        /// <summary>
        /// 加载登录锁定配置
        /// </summary>
        /// <param name="options">Identity配置</param>
        /// <param name="permissionOptions">权限配置</param>
        private static void LoadLockout(IdentityOptions options, PermissionOptions permissionOptions)
        {
            options.Lockout.AllowedForNewUsers = permissionOptions.Lockout.AllowedForNewUsers;
            options.Lockout.DefaultLockoutTimeSpan = permissionOptions.Lockout.LockoutTimeSpan;
            options.Lockout.MaxFailedAccessAttempts = permissionOptions.Lockout.MaxFailedAccessAttempts;
        }
    }
}
