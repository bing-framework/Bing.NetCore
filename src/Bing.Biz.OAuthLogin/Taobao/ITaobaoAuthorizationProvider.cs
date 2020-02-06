using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Taobao
{
    /// <summary>
    /// 淘宝授权提供程序
    /// </summary>
    public interface ITaobaoAuthorizationProvider : IAuthorizationUrlProvider<TaobaoAuthorizationRequest>
    {
    }
}
