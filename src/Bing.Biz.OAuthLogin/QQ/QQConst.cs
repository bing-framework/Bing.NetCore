namespace Bing.Biz.OAuthLogin.QQ
{
    /// <summary>
    /// QQ常量
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal class QQConst
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        public const string PcAuthorizationUrl = "https://graph.qq.com/oauth2.0/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        public const string PcAccessTokenUrl = "https://graph.qq.com/oauth2.0/token";

        /// <summary>
        /// QQ跟踪日志名
        /// </summary>
        public const string TraceLogName = "QQTraceLog";
    }
}
