using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Microsoft
{
    /// <summary>
    /// Microsoft 授权提供程序
    /// </summary>
    public interface IMicrosoftAuthorizationProvider : IAuthorizationUrlProvider<MicrosoftAuthorizationRequest>
    {
    }
}
