using System;
using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Extensions
{
    /// <summary>
    /// 授权提供程序(<see cref="IAuthorizationProvider"/>) 扩展
    /// </summary>
    public static class AuthorizationProviderExtensions
    {
        /// <summary>
        /// 获取访问令牌
        /// </summary>
        /// <param name="provider">授权提供程序</param>
        /// <param name="param">访问令牌参数</param>
        /// <returns></returns>
        public static async Task<AccessTokenResult> GetTokenAsync(this IAuthorizationProvider provider,
            AccessTokenParam param)
        {
            if (provider is IAccessTokenProvider accessTokenProvider)
            {
                return await accessTokenProvider.GetTokenAsync(param);
            }

            throw new NotImplementedException(nameof(GetTokenAsync));
        }

        /// <summary>
        /// 刷新访问令牌
        /// </summary>
        /// <param name="provider">授权提供程序</param>
        /// <param name="token">刷新令牌</param>
        /// <returns></returns>
        public static async Task<AccessTokenResult> RefreshTokenAsync(this IAuthorizationProvider provider,
            string token)
        {
            if (provider is IRefreshTokenProvider accessTokenProvider)
            {
                return await accessTokenProvider.RefreshTokenAsync(token);
            }

            throw new NotImplementedException(nameof(RefreshTokenAsync));
        }
    }
}
