using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Jd.Configs;

namespace Bing.Biz.OAuthLogin.Jd
{
    /// <summary>
    /// 京东授权提供程序
    /// </summary>
    public class JdAuthorizationProvider : AuthorizationProviderBase<IJdAuthorizationConfigProvider, JdAuthorizationConfig>, IJdAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://oauth.jd.com/oauth/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://oauth.jd.com/oauth/token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "JdTraceLog";

        /// <summary>
        /// 初始化一个<see cref="JdAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">京东授权配置提供程序</param>
        public JdAuthorizationProvider(IJdAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, JdAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ResponseType(param.ResponseType)
                .ClientId(config.AppId)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
                .State(param.State)
                .Scope(param.Scope)
                .View(param.View);
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Jd;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">京东授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(JdAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
