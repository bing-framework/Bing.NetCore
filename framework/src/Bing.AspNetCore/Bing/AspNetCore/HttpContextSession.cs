using System.Linq;
using System.Security.Claims;
using Bing.Security.Claims;
using Bing.Security.Principals;

namespace Bing.AspNetCore
{
    /// <summary>
    /// Http上下文 - 用户会话
    /// </summary>
    public class HttpContextSession : Bing.Application.ISession
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
        /// 初始化一个<see cref="HttpContextSession"/>类型的实例
        /// </summary>
        /// <param name="principalAccessor">安全主体访问器</param>
        public HttpContextSession(ICurrentPrincipalAccessor principalAccessor) => _principalAccessor = principalAccessor;

        /// <summary>
        /// 用户安全主体
        /// </summary>
        public ClaimsPrincipal User
        {
            get
            {
                if (_principalAccessor.Principal != null)
                    return _principalAccessor.Principal;
                return UnauthenticatedPrincipal.Instance;
            }
        }

        /// <summary>
        /// 用户身份
        /// </summary>
        public ClaimsIdentity Identity
        {
            get
            {
                if (User.Identity is ClaimsIdentity identity)
                    return identity;
                return UnauthenticatedIdentity.Instance;
            }
        }

        /// <summary>
        /// 用户标识
        /// </summary>
        public virtual string UserId
        {
            get
            {
                if (User == null)
                    return null;
                var result = User.FindFirst(BingClaimTypes.UserId)?.Value;
                if (string.IsNullOrWhiteSpace(result))
                    result = User.FindFirst("sid")?.Value;
                if (string.IsNullOrWhiteSpace(result))
                    result = User.FindFirst("sub")?.Value;
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
                if (User == null)
                    return null;
                var result = User.FindFirst(BingClaimTypes.UserName)?.Value;
                if (string.IsNullOrWhiteSpace(result))
                    result = User.FindFirst("name")?.Value;
                return result;
            }
        }

        /// <summary>
        /// 角色数组
        /// </summary>
        public virtual string[] Roles
        {
            get
            {
                if (User == null)
                    return null;
                var result = User.FindAll(BingClaimTypes.Role).Select(x => x.Value).ToArray();
                if (!result.Any())
                    result = User.FindAll("role").Select(x => x.Value).ToArray();
                return result;
            }
        }

        /// <summary>
        /// 是否已认证
        /// </summary>
        public bool IsAuthenticated => Identity.IsAuthenticated;

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
