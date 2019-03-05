using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.DingTalk.Configs;

namespace Bing.Biz.OAuthLogin.DingTalk
{
    /// <summary>
    /// 钉钉授权提供程序
    /// </summary>
    public class DingTalkAuthorizationProvider: AuthorizationProviderBase<IDingTalkAuthorizationConfigProvider,DingTalkAuthorizationConfig>,IDingTalkAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://oapi.dingtalk.com/connect/oauth2/sns_authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://oauth.jd.com/oauth/token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "DingTalkTraceLog";

        /// <summary>
        /// 初始化一个<see cref="DingTalkAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">钉钉授权配置提供程序</param>
        public DingTalkAuthorizationProvider(IDingTalkAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, DingTalkAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .AppId(config.AppId)
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.DingTalk;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">钉钉授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(DingTalkAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
