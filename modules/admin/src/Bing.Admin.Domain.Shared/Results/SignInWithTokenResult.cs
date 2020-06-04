using Bing.Permissions.Identity.JwtBearer;
using Bing.Permissions.Identity.Results;

namespace Bing.Admin.Domain.Shared.Results
{
    /// <summary>
    /// 登录结果
    /// </summary>
    public class SignInWithTokenResult : SignInResult
    {
        /// <summary>
        /// 令牌
        /// </summary>
        public JsonWebToken Token { get; set; }
    }
}
