using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Jd
{
    /// <summary>
    /// 京东授权提供程序
    /// </summary>
    public interface IJdAuthorizationProvider:IAuthorizationUrlProvider<JdAuthorizationRequest>
    {
    }
}
