using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Jd
{
    /// <summary>
    /// 京东授权请求
    /// </summary>
    public class JdAuthorizationRequest : AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围。目前支持参数值：read
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// 显示页面类型。移动端授权，该值固定为wap；非移动端授权，无需传值
        /// </summary>
        public string View { get; set; }
    }
}
