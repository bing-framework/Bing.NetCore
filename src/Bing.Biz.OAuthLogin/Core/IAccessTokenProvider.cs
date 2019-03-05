using System.Threading.Tasks;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权访问令牌提供程序
    /// </summary>
    /// <typeparam name="TAccessTokenResult">访问令牌结果类型</typeparam>
    public interface IAccessTokenProvider<TAccessTokenResult> where TAccessTokenResult:AccessTokenResult
    {
        /// <summary>
        /// 获取访问令牌
        /// </summary>
        /// <param name="param">访问令牌参数</param>
        /// <returns></returns>
        Task<TAccessTokenResult> GetTokenAsync(AccessTokenParam param);
    }

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
        Task<AuthorizationResult> GetTokenAsync(AccessTokenParam param);
    }
}
