namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权参数
    /// </summary>
    public class AuthorizationParam: AuthorizationParamBase
    {
        /// <summary>
        /// 申请的权限范围
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// 显示视图
        /// </summary>
        public string View { get; set; }

        /// <summary>
        /// 显示样式
        /// </summary>
        public string Display { get; set; }
    }
}
