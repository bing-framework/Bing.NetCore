using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Baidu
{
    /// <summary>
    /// 百度授权提供程序
    /// </summary>
    public interface IBaiduAuthorizationProvider: IAuthorizationUrlProvider<BaiduAuthorizationRequest>
    {
    }
}
