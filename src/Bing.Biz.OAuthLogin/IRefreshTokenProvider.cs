using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin
{
    /// <summary>
    /// 刷新访问令牌提供程序
    /// </summary>
    /// <typeparam name="TAccessTokenResult">访问令牌结果类型</typeparam>
    public interface IRefreshTokenProvider<TAccessTokenResult> where TAccessTokenResult: AccessTokenResult
    {
        /// <summary>
        /// 刷新访问令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        /// <returns></returns>
        Task<TAccessTokenResult> RefreshTokenAsync(string token);
    }

    /// <summary>
    /// 刷新访问令牌提供程序
    /// </summary>
    public interface IRefreshTokenProvider : IRefreshTokenProvider<AccessTokenResult> { }
}
