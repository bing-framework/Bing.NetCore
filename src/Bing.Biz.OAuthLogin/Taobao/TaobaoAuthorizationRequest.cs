using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Taobao
{
    /// <summary>
    /// 淘宝授权请求
    /// </summary>
    public class TaobaoAuthorizationRequest:AuthorizationParamBase
    {
        /// <summary>
        /// 显示页面类型。Web对应PC端（淘宝logo）浏览器页面样式；Tmall对应天猫的浏览器页面样式；Wap对应无线端的浏览器页面样式。
        /// </summary>
        public string View { get; set; }
    }
}
