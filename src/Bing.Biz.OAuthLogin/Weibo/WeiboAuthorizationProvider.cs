using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Weibo.Configs;

namespace Bing.Biz.OAuthLogin.Weibo
{
    /// <summary>
    /// 微博授权提供程序
    /// </summary>
    public class WeiboAuthorizationProvider:AuthorizationProviderBase<IWeiboAuthorizationConfigProvider,WeiboAuthorizationConfig>,IWeiboAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://api.weibo.com/oauth2/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://api.weibo.com/oauth2/access_token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "WeiboTraceLog";

        /// <summary>
        /// 初始化一个<see cref="WeiboAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">微博授权配置提供程序</param>
        public WeiboAuthorizationProvider(IWeiboAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void Config(AuthorizationParameterBuilder builder, AuthorizationParam param, WeiboAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ClientId(config.AppId)
                .ResponseType(param.ResponseType)
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Weibo;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">微博授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(WeiboAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
