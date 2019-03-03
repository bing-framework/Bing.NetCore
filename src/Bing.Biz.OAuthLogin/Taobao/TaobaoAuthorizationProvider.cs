using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Taobao.Configs;

namespace Bing.Biz.OAuthLogin.Taobao
{
    /// <summary>
    /// 淘宝授权提供程序
    /// </summary>
    public class TaobaoAuthorizationProvider:AuthorizationProviderBase<ITaobaoAuthorizationConfigProvider,TaobaoAuthorizationConfig>,ITaobaoAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://oauth.taobao.com/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://oauth.taobao.com/token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "TaobaoTraceLog";

        /// <summary>
        /// 初始化一个<see cref="TaobaoAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">淘宝授权配置提供程序</param>
        public TaobaoAuthorizationProvider(ITaobaoAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void Config(AuthorizationParameterBuilder builder, AuthorizationParam param, TaobaoAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ResponseType(param.ResponseType)
                .ClientId(config.AppId)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
                .State(param.State)
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Taobao;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">淘宝授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(TaobaoAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
