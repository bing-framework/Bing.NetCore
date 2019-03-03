using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Facebook.Configs;

namespace Bing.Biz.OAuthLogin.Facebook
{
    /// <summary>
    /// Facebook 授权提供程序
    /// </summary>
    public class FacebookAuthorizationProvider: AuthorizationProviderBase<IFacebookAuthorizationConfigProvider,FacebookAuthorizationConfig>,IFacebookAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://www.facebook.com/v3.2/dialog/oauth";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://graph.facebook.com/v3.2/oauth/access_token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "FacebookTraceLog";

        /// <summary>
        /// 初始化一个<see cref="FacebookAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">Facebook 授权配置提供程序</param>
        public FacebookAuthorizationProvider(IFacebookAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void Config(AuthorizationParameterBuilder builder, AuthorizationParam param, FacebookAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ClientId(config.AppId)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
                .State(param.State)
                .ResponseType(param.ResponseType)
                .Scope(param.Scope);
        }

        /// <summary>
        /// 获取跟踪日志名
        /// </summary>
        /// <returns></returns>
        protected override string GetTraceLogName() => TraceLogName;

        /// <summary>
        /// 获取授权方式
        /// </summary>
        /// <returns></returns>
        protected override OAuthWay GetOAuthWay() => OAuthWay.Facebook;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">Facebook 授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(FacebookAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
