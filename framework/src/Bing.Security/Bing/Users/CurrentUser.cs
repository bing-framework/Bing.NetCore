using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Bing.DependencyInjection;
using Bing.Security.Claims;

namespace Bing.Users
{
    /// <summary>
    /// 当前用户
    /// </summary>
    public class CurrentUser : ICurrentUser, ITransientDependency
    {
        /// <summary>
        /// 空声明数组
        /// </summary>
        private static readonly Claim[] EmptyClaimsArray = new Claim[0];

        /// <summary>
        /// 安全主体访问器
        /// </summary>
        private readonly ICurrentPrincipalAccessor _principalAccessor;

        /// <summary>
        /// 初始化一个<see cref="CurrentUser"/>类型的实例
        /// </summary>
        /// <param name="principalAccessor">安全主体访问器</param>
        public CurrentUser(ICurrentPrincipalAccessor principalAccessor) => _principalAccessor = principalAccessor;

        /// <summary>
        /// 是否已认证
        /// </summary>
        public virtual bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);

        /// <summary>
        /// 用户标识
        /// </summary>
        public virtual string UserId
        {
            get
            {
                var result = this.FindClaimValue(BingClaimTypes.UserId);
                if (string.IsNullOrWhiteSpace(result))
                    result = this.FindClaimValue("sid");
                if (string.IsNullOrWhiteSpace(result))
                    result = this.FindClaimValue("sub");
                return result;
            }
        }

        /// <summary>
        /// 用户名
        /// </summary>
        public virtual string UserName
        {
            get
            {
                var result = this.FindClaimValue(BingClaimTypes.UserName);
                if (string.IsNullOrWhiteSpace(result))
                    result = this.FindClaimValue("name");
                return result;
            }
        }

        /// <summary>
        /// 手机号码
        /// </summary>
        public virtual string PhoneNumber => this.FindClaimValue(BingClaimTypes.PhoneNumber);

        /// <summary>
        /// 是否已验证手机号码
        /// </summary>
        public virtual bool PhoneNumberVerified => string.Equals(this.FindClaimValue(BingClaimTypes.PhoneNumberVerified), "true",
            StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// 电子邮箱
        /// </summary>
        public virtual string Email => this.FindClaimValue(BingClaimTypes.Email);

        /// <summary>
        /// 是否已验证邮箱
        /// </summary>
        public virtual bool EmailVerified => string.Equals(this.FindClaimValue(BingClaimTypes.EmailVerified), "true",
            StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// 租户标识
        /// </summary>
        public virtual string TenantId =>_principalAccessor.Principal?.FindTenantId()?.ToString() ?? string.Empty;

        /// <summary>
        /// 角色列表
        /// </summary>
        public virtual string[] Roles
        {
            get
            {
                var result = FindClaims(BingClaimTypes.Role).Select(x => x.Value).ToArray();
                if (!result.Any())
                    result = FindClaims("role").Select(x => x.Value).ToArray();
                return result;
            }
        }

        /// <summary>
        /// 查找声明
        /// </summary>
        /// <param name="claimType">声明类型</param>
        public virtual Claim FindClaim(string claimType) => _principalAccessor.Principal?.Claims.FirstOrDefault(c => c.Type == claimType);

        /// <summary>
        /// 查找声明列表
        /// </summary>
        /// <param name="claimType">声明类型</param>
        public virtual Claim[] FindClaims(string claimType) => _principalAccessor.Principal?.Claims.Where(c => c.Type == claimType).ToArray() ?? EmptyClaimsArray;

        /// <summary>
        /// 获取所有声明列表
        /// </summary>
        public virtual Claim[] GetAllClaims() => _principalAccessor.Principal?.Claims.ToArray() ?? EmptyClaimsArray;

        /// <summary>
        /// 是否包含指定角色
        /// </summary>
        /// <param name="roleName">角色名</param>
        public virtual bool IsInRole(string roleName) => FindClaims(BingClaimTypes.Role).Any(c => c.Value == roleName);
    }
}
