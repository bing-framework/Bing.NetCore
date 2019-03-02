using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.QQ
{
    /// <summary>
    /// QQ授权提供程序。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public interface IQQAuthorizationProvider : IAuthorizationUrlProvider<QQAuthorizationRequest>, IAccessTokenProvider
    {
    }
}
