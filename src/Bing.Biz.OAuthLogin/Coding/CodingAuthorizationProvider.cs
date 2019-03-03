using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Coding.Configs;
using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Coding
{
    /// <summary>
    /// Coding.NET 授权提供程序
    /// </summary>
    public class CodingAuthorizationProvider: AuthorizationProviderBase<ICodingAuthorizationConfigProvider,CodingAuthorizationConfig>,ICodingAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://coding.net/oauth_authorize.html";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://coding.net/api/oauth/access_token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "CodingTraceLog";

        /// <summary>
        /// 初始化一个<see cref="CodingAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">Coding.NET 授权配置提供程序</param>
        public CodingAuthorizationProvider(ICodingAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void Config(AuthorizationParameterBuilder builder, AuthorizationParam param, CodingAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ClientId(config.AppId)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Coding;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">Coding.NET 授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(CodingAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
