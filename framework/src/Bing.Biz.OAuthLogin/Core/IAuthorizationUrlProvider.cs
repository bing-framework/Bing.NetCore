using System.Threading.Tasks;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权地址提供程序
    /// </summary>
    /// <typeparam name="TAuthorizationParam">授权参数</typeparam>
    public interface IAuthorizationUrlProvider<in TAuthorizationParam>
        where TAuthorizationParam : AuthorizationParamBase
    {
        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="param">授权参数</param>
        /// <returns></returns>
        Task<string> GenerateUrlAsync(TAuthorizationParam param);
    }
}
