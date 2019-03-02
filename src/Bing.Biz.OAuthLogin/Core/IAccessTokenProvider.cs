using System.Threading.Tasks;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权访问令牌提供程序
    /// </summary>
    public interface IAccessTokenProvider
    {
        /// <summary>
        /// 获取访问令牌
        /// </summary>
        /// <param name="param">访问令牌参数</param>
        /// <returns></returns>
        Task<AccessTokenResult> GetTokenAsync(AccessTokenParam param);

        /// <summary>
        /// 刷新访问令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        /// <returns></returns>
        Task<AccessTokenResult> RefreshTokenAsync(string token);
    }
}
