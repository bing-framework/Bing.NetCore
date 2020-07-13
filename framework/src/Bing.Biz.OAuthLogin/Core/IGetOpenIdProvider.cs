using System.Threading.Tasks;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 获取用户ID提供程序
    /// </summary>
    public interface IGetOpenIdProvider
    {
        /// <summary>
        /// 获取用户OpenId
        /// </summary>
        /// <param name="token">授权令牌</param>
        /// <returns></returns>
        Task<string> GetOpenIdAsync(string token);
    }
}
