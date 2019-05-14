using Bing.Helpers;
using Bing.Sessions;
using Bing.Utils.Extensions;
using IdentityModel;

namespace Bing.Security.Sessions
{
    /// <summary>
    /// 用户会话
    /// </summary>
    public class Session:ISession
    {
        /// <summary>
        /// 用户标识
        /// </summary>
        public string UserId
        {
            get
            {
                var result = WebIdentity.Identity.GetValue(JwtClaimTypes.Subject);
                return string.IsNullOrWhiteSpace(result)
                    ? WebIdentity.Identity.GetValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                    : result;
            }
        }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName 
        {
            get
            {
                var result = WebIdentity.Identity.GetValue(JwtClaimTypes.Name);
                return string.IsNullOrWhiteSpace(result)
                    ? WebIdentity.Identity.GetValue(System.Security.Claims.ClaimTypes.Name)
                    : result;
            }
        }

        /// <summary>
        /// 是否认证
        /// </summary>
        public bool IsAuthenticated => WebIdentity.Identity.IsAuthenticated;

        /// <summary>
        /// 用户会话
        /// </summary>
        public static readonly ISession Instance = new Session();

        /// <summary>
        /// 空用户会话
        /// </summary>
        public static readonly ISession Null = NullSession.Instance;
    }
}
