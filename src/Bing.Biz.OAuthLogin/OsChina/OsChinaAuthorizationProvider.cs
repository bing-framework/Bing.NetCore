using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.OsChina.Configs;

namespace Bing.Biz.OAuthLogin.OsChina
{
    /// <summary>
    /// 开源中国授权提供程序
    /// </summary>
    public class OsChinaAuthorizationProvider : AuthorizationProviderBase<IOsChinaAuthorizationConfigProvider, OsChinaAuthorizationConfig>, IOsChinaAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://www.oschina.net/action/oauth2/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://www.oschina.net/action/openapi/token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "OsChinaTraceLog";

        /// <summary>
        /// 初始化一个<see cref="OsChinaAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">开源中国授权配置提供程序</param>
        public OsChinaAuthorizationProvider(IOsChinaAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, OsChinaAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ClientId(config.AppId)
                .ResponseType(param.ResponseType)
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.OsChina;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">开源中国授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(OsChinaAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
