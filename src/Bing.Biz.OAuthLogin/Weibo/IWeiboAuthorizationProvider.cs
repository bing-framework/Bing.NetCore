using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Weibo
{
    /// <summary>
    /// 微博授权提供程序
    /// </summary>
    public interface IWeiboAuthorizationProvider : IAuthorizationUrlProvider<WeiboAuthorizationRequest>        
    {
    }
}
