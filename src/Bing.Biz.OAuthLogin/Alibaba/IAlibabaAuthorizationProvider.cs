using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Alibaba
{
    /// <summary>
    /// 阿里巴巴授权提供程序
    /// </summary>
    public interface IAlibabaAuthorizationProvider: IAuthorizationUrlProvider<AlibabaAuthorizationRequest>
    {
    }
}
