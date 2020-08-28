using System.Threading.Tasks;
using Bing.Admin.Domain.Shared.Results;
using Bing.Admin.Service.Requests.Systems;
using Bing.Application.Services;
using Bing.Permissions.Identity.JwtBearer;

namespace Bing.Admin.Service.Abstractions
{
    /// <summary>
    /// 安全服务
    /// </summary>
    public interface ISecurityService : IAppService
    {
        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="request">请求</param>
        Task<SignInWithTokenResult> SignInAsync(AdminLoginRequest request);

        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <param name="refreshToken">刷新令牌</param>
        Task<JsonWebToken> RefreshTokenAsync(string refreshToken);
    }
}
