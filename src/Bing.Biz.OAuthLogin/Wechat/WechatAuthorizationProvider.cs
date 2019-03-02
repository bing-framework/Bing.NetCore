using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Wechat.Configs;
using Bing.Utils.Json;

namespace Bing.Biz.OAuthLogin.Wechat
{
    /// <summary>
    /// 微信授权提供程序
    /// </summary>
    public class WechatAuthorizationProvider : AuthorizationProviderBase<IWechatAuthorizationConfigProvider, WechatAuthorizationConfig>, IWechatAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://open.weixin.qq.com/connect/qrconnect";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://api.weixin.qq.com/sns/oauth2/access_token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "WechatTraceLog";

        /// <summary>
        /// 初始化一个<see cref="WechatAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">微信授权配置提供程序</param>
        public WechatAuthorizationProvider(IWechatAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void Config(AuthorizationParameterBuilder builder, AuthorizationParam param, WechatAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .AppId(config.AppId)
                .ResponseType(param.ResponseType)
                .Scope(param.Scope)
                .State(param.State)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri);
        }

        /// <summary>
        /// 处理授权地址
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <returns></returns>
        protected override string HandlerUrl(AuthorizationParameterBuilder builder) => $"{builder.ToString()}#wechat_redirect";

        /// <summary>
        /// 获取跟踪日志名
        /// </summary>
        /// <returns></returns>
        protected override string GetTraceLogName() => TraceLogName;

        /// <summary>
        /// 获取授权方式
        /// </summary>
        /// <returns></returns>
        protected override OAuthWay GetOAuthWay() => OAuthWay.Wechat;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">微信授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(WechatAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());

        /// <summary>
        /// 获取访问令牌
        /// </summary>
        /// <param name="param">访问令牌参数</param>
        /// <returns></returns>
        public async Task<AccessTokenResult> GetTokenAsync(AccessTokenParam param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param);
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(PcAccessTokenUrl)
                .AppId(config.AppId)
                .Secret(config.AppKey)
                .Code(param.Code)
                .GrantType(OAuthConst.AuthorizationCode);
            var result = await Request(builder);
            return result.ToObject<WechatAccessTokenResult>();
        }

        public async Task<AccessTokenResult> RefreshTokenAsync(string token)
        {
            throw new System.NotImplementedException();
        }
    }
}
