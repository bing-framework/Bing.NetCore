using System;
using System.Threading.Tasks;
using Bing.Domains.Services;
using Bing.Security.Identity.Extensions;
using Bing.Security.Identity.Models;
using Bing.Security.Identity.Options;
using Bing.Security.Identity.Repositories;
using Bing.Security.Identity.Services.Abstractions;
using Bing.Utils.Extensions;
using IdentityModel.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Bing.Security.Identity.Services.Implements
{
    /// <summary>
    /// 用户服务
    /// </summary>
    /// <typeparam name="TUser">用户类型</typeparam>
    /// <typeparam name="TKey">用户标识类型</typeparam>
    public class UserManager<TUser,TKey>:DomainServiceBase,IUserManager<TUser,TKey> where TUser:User<TUser,TKey>,new()
    {
        /// <summary>
        /// Identity用户服务
        /// </summary>
        private IdentityUserManager<TUser,TKey> Manager { get; }

        /// <summary>
        /// 权限配置
        /// </summary>
        private IOptions<PermissionOptions> Options { get; }

        /// <summary>
        /// 用户仓储
        /// </summary>
        private IUserRepository<TUser,TKey> UserRepository { get; }

        /// <summary>
        /// 初始化一个<see cref="UserManager{TUser,TKey}"/>类型的实例
        /// </summary>
        /// <param name="userManager">Identity用户服务</param>
        /// <param name="options">权限配置</param>
        /// <param name="userRepository">用户仓储</param>
        public UserManager(IdentityUserManager<TUser, TKey> userManager, IOptions<PermissionOptions> options,
            IUserRepository<TUser, TKey> userRepository)
        {
            Manager = userManager;
            Options = options;
            UserRepository = userRepository;
        }

        #region Create(创建用户)

        public async Task CreateAsync(TUser user, string password)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            user.Init();
            var result = await Manager.CreateAsync(user, password);
            result.ThrowIfError();
            user.SetPassword(Options?.Value.Store.StoreOriginalPassword, password);
        }

        #endregion

        #region GenerateToken(生成令牌)

        public async Task<string> GenerateTokenAsync(string phone, string purpose, string application = "", string provider = "")
        {
            var user = await GetUserOrDefault(phone);
            return await GenerateTokenAsync(user, purpose, application, provider);
        }

        /// <summary>
        /// 获取用户
        /// </summary>
        /// <param name="phone">手机号</param>
        /// <returns></returns>
        private async Task<TUser> GetUserOrDefault(string phone)
        {
            var user = await this.FindByPhoneAsync(phone);
            if (user == null)
            {
                user=new TUser()
                {
                    PhoneNumber = phone,
                    SecurityStamp = CreateSecurityStamp()
                };
            }
            return user;
        }

        /// <summary>
        /// 创建安全戳
        /// </summary>
        /// <returns></returns>
        protected virtual string CreateSecurityStamp()
        {
            return "56df9984-bc05-460a-a4ce-9dec3922a5e9";
        }


        public async Task<string> GenerateTokenAsync(TUser user, string purpose, string application = "", string provider = "")
        {
            user.CheckNotNull(nameof(user));
            purpose = GetPurpose(purpose, application);
            if (provider.IsEmpty())
            {
                provider = TokenOptions.DefaultPhoneProvider;
            }
            return await Manager.GenerateUserTokenAsync(user, provider, purpose);
        }

        /// <summary>
        /// 创建用途
        /// </summary>
        /// <param name="purpose">用途</param>
        /// <param name="application">应用程序</param>
        /// <returns></returns>
        private string GetPurpose(string purpose, string application)
        {
            return $"{purpose}_{application}";
        }

        #endregion



        public async Task<bool> VerifyTokenAsync(string phone, string purpose, string token, string application = "", string provider = "")
        {
            throw new System.NotImplementedException();
        }

        public async Task<bool> VerifyTokenAsync(TUser user, string purpose, string token, string application = "", string provider = "")
        {
            throw new System.NotImplementedException();
        }

        public async Task<string> GenerateRegisterTokenAsync(string phone, string application = "")
        {
            throw new System.NotImplementedException();
        }

        public async Task<bool> VerifyRegisterTokenAsync(string phone, string token, string application = "")
        {
            throw new System.NotImplementedException();
        }

        public async Task<string> GenerateEmailConfirmationTokenAsync(TUser user)
        {
            throw new System.NotImplementedException();
        }

        public async Task ConfirmEmailAsync(TUser user, string token)
        {
            throw new System.NotImplementedException();
        }

        public async Task<string> GenerateEmailPasswordResetTokenAsync(TUser user)
        {
            throw new System.NotImplementedException();
        }

        public async Task ResetPasswordByEmailAsync(TUser user, string token, string newPassword)
        {
            throw new System.NotImplementedException();
        }

        public async Task<string> GeneratePhonePasswordResetTokenAsync(TUser user)
        {
            throw new System.NotImplementedException();
        }

        public async Task ResetPasswordByPhoneAsync(TUser user, string token, string newPassword)
        {
            throw new System.NotImplementedException();
        }

        public async Task ChangePasswordAsync(TUser user, string currentPassword, string newPassword)
        {
            throw new System.NotImplementedException();
        }

        public async Task<TUser> FindByNameAsync(string userName)
        {
            throw new System.NotImplementedException();
        }

        public async Task<TUser> FindByEmailAsync(string email)
        {
            throw new System.NotImplementedException();
        }

        public async Task<TUser> FindByPhoneAsync(string phone)
        {
            throw new System.NotImplementedException();
        }
    }
}
