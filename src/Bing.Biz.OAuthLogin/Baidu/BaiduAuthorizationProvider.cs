using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Baidu.Configs;
using Bing.Biz.OAuthLogin.Core;

namespace Bing.Biz.OAuthLogin.Baidu
{
    /// <summary>
    /// 百度授权提供程序
    /// </summary>
    public class BaiduAuthorizationProvider: AuthorizationProviderBase<IBaiduAuthorizationConfigProvider,BaiduAuthorizationConfig>,IBaiduAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://openapi.baidu.com/oauth/2.0/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://openapi.baidu.com/oauth/2.0/token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "BaiduTraceLog";

        /// <summary>
        /// 初始化一个<see cref="BaiduAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">百度授权配置提供程序</param>
        public BaiduAuthorizationProvider(IBaiduAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, BaiduAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ResponseType(param.ResponseType)
                .ClientId(config.AppId)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
                .Scope(param.Scope)
                .Add("display", param.Display);
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Baidu;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">百度授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(BaiduAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());
    }
}
