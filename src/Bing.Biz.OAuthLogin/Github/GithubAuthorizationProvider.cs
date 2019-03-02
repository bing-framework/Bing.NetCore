using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Github.Configs;

namespace Bing.Biz.OAuthLogin.Github
{
    /// <summary>
    /// Github授权提供程序
    /// </summary>
    public class GithubAuthorizationProvider: AuthorizationProviderBase<IGithubAuthorizationConfigProvider,GithubAuthorizationConfig>,IGithubAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://github.com/login/oauth/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://github.com/login/oauth/access_token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "GithubTraceLog";

        /// <summary>
        /// 初始化一个<see cref="GithubAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">微博授权配置提供程序</param>
        public GithubAuthorizationProvider(IGithubAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void Config(AuthorizationParameterBuilder builder, AuthorizationParam param, GithubAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ClientId(config.AppId)
                .Scope(param.Scope)
                .State(param.State)
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Github;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">Github授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(GithubAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
