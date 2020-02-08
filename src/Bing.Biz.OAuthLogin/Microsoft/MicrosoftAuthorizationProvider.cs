using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Microsoft.Configs;

namespace Bing.Biz.OAuthLogin.Microsoft
{
    /// <summary>
    /// Microsoft 授权提供程序
    /// </summary>
    public class MicrosoftAuthorizationProvider : AuthorizationProviderBase<IMicrosoftAuthorizationConfigProvider, MicrosoftAuthorizationConfig>, IMicrosoftAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://login.live.com/oauth20_authorize.srf";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://login.live.com/oauth20_token.srf";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "MicrosoftTraceLog";

        /// <summary>
        /// 初始化一个<see cref="MicrosoftAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">Microsoft 授权配置提供程序</param>
        public MicrosoftAuthorizationProvider(IMicrosoftAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, MicrosoftAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ClientId(config.AppId)
                .Scope(param.Scope)
                .ResponseType(param.ResponseType)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri);
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Microsoft;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">Microsoft 授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(MicrosoftAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
