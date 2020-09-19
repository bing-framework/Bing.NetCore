using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Youzan.Configs;

namespace Bing.Biz.OAuthLogin.Youzan
{
    /// <summary>
    /// 有赞授权提供程序
    /// </summary>
    public class YouzanAuthorizationProvider : AuthorizationProviderBase<IYouzanAuthorizationConfigProvider, YouzanAuthorizationConfig>, IYouzanAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://open.youzan.com/oauth/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://open.youzan.com/oauth/token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "YouzanTraceLog";

        /// <summary>
        /// 初始化一个<see cref="YouzanAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">有赞授权配置提供程序</param>
        public YouzanAuthorizationProvider(IYouzanAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, YouzanAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ClientId(config.AppId)
                .ResponseType(param.ResponseType)
                .State(param.State)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Youzan;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">有赞授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(YouzanAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
