using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Alibaba.Configs;
using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Alibaba
{
    /// <summary>
    /// 阿里巴巴授权提供程序
    /// </summary>
    public class AlibabaAuthorizationProvider: AuthorizationProviderBase<IAlibabaAuthorizationConfigProvider,AlibabaAuthorizationConfig>,IAlibabaAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://auth.1688.com/oauth/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://gw.open.1688.com/openapi/http/1/system.oauth2/getToken";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "AlibabaTraceLog";

        /// <summary>
        /// 初始化一个<see cref="AlibabaAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">阿里巴巴授权配置提供程序</param>
        public AlibabaAuthorizationProvider(IAlibabaAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, AlibabaAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ClientId(config.AppId)
                .Add("site", param.Site)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
                .State(param.State);
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Alibaba;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">阿里巴巴授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(AlibabaAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
