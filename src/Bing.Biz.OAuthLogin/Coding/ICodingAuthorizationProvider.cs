using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Coding
{
    /// <summary>
    /// Coding.NET 授权提供程序
    /// </summary>
    public interface ICodingAuthorizationProvider: IAuthorizationUrlProvider<CodingAuthorizationRequest>
        , IAccessTokenProvider
        , IGetUserInfoProvider<CodingAuthorizationUserInfoResult,CodingAuthorizationUserRequest>
    {
    }
}
