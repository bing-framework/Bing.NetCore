using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.MeiliShuo.Configs;

namespace Bing.Biz.OAuthLogin.MeiliShuo
{
    /// <summary>
    /// 美丽说授权提供程序
    /// </summary>
    public class MeiliShuoAuthorizationProvider: AuthorizationProviderBase<IMeiliShuoAuthorizationConfigProvider, MeiliShuoAuthorizationConfig>,IMeiliShuoAuthorizationProvider
    {
        
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://oauth.meilishuo.com/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://oauth.meilishuo.com/token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "MeiliShuoTraceLog";

        /// <summary>
        /// 初始化一个<see cref="MeiliShuoAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">美丽说授权配置提供程序</param>
        public MeiliShuoAuthorizationProvider(IMeiliShuoAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void Config(AuthorizationParameterBuilder builder, AuthorizationParam param, MeiliShuoAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ResponseType(param.ResponseType)
                .Add("app_key", config.AppId)
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.MeiliShuo;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">美丽说授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(MeiliShuoAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
