using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Baidu
{
    /// <summary>
    /// 百度授权请求
    /// </summary>
    public class BaiduAuthorizationRequest: AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// 登录和授权页面的展示样式
        /// </summary>
        public string Display { get; set; }

        /// <summary>
        /// 是否强制登录
        /// </summary>
        public bool ForceLogin { get; set; }

        /// <summary>
        /// 是否提示使用当前登录用户对应用授权
        /// </summary>
        public bool ConfirmLogin { get; set; }

        /// <summary>
        /// 登录类型
        /// </summary>
        public string LoginType { get; set; }
    }
}
