using System.Linq;
using System.Security.Claims;
using Bing.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Bing.Sessions
{
    /// <summary>
    /// Http上下文会话
    /// </summary>
    public class HttpContextSession : ISession
    {
        /// <summary>
        /// 空声明数组
        /// </summary>
        private static readonly Claim[] EmptyClaimsArray = new Claim[0];

        /// <summary>
        /// Http上下文访问器
        /// </summary>
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// 初始化一个<see cref="HttpContextSession"/>类型的实例
        /// </summary>
        /// <param name="httpContextAccessor">Http上下文访问器</param>
        public HttpContextSession(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// 用户标识
        /// </summary>
        public virtual string UserId
        {
            get
            {
                var result = this.FindClaimValue(BingClaimTypes.UserId);
                if (string.IsNullOrWhiteSpace(result))
                    result = this.FindClaimValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(result))
                    result = this.FindClaimValue("sid");
                if (string.IsNullOrWhiteSpace(result))
                    result = this.FindClaimValue("sub");
                return result;
            }
        }

        /// <summary>
        /// 是否认证
        /// </summary>
        public virtual bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        /// <summary>
        /// 查找声明
        /// </summary>
        /// <param name="claimType">声明类型</param>
        public virtual Claim FindClaim(string claimType) => _httpContextAccessor.HttpContext?.User?.Claims.FirstOrDefault(c => c.Type == claimType);

        /// <summary>
        /// 查找声明列表
        /// </summary>
        /// <param name="claimType">声明类型</param>
        public virtual Claim[] FindClaims(string claimType) =>
            _httpContextAccessor.HttpContext?.User?.Claims?.Where(c => c.Type == claimType).ToArray() ??
            EmptyClaimsArray;

        /// <summary>
        /// 获取所有声明列表
        /// </summary>
        public virtual Claim[] GetAllClaims() => _httpContextAccessor.HttpContext?.User?.Claims?.ToArray() ?? EmptyClaimsArray;
    }
}
