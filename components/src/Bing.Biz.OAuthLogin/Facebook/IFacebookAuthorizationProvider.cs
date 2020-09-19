using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Facebook
{
    /// <summary>
    /// Facebook 授权提供程序
    /// </summary>
    public interface IFacebookAuthorizationProvider : IAuthorizationUrlProvider<FacebookAuthorizationRequest>
    {
    }
}
