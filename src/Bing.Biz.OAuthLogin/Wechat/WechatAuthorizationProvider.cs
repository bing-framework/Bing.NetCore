using System;
using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Wechat.Configs;
using Bing.Utils.Json;
using Bing.Utils.Parameters.Parsers;

namespace Bing.Biz.OAuthLogin.Wechat
{
    /// <summary>
    /// 微信授权提供程序
    /// </summary>
    public class WechatAuthorizationProvider : AuthorizationProviderBase<IWechatAuthorizationConfigProvider, WechatAuthorizationConfig>, IWechatAuthorizationProvider
    {
        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "WechatTraceLog";

        /// <summary>
        /// 获取用户信息Url
        /// </summary>
        internal const string GetUserInfoUrl = "https://api.weixin.qq.com/sns/userinfo";

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
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, WechatAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AuthorizationUrl)
                .AppId(config.AppId)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
                .ResponseType(param.ResponseType)
                .Scope(param.Scope)
                .State(param.State);
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
        public override async Task<AuthorizationResult> GetTokenAsync(AccessTokenParam param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param);
            var builder = new AuthorizationParameterBuilder();
            ConfigGetToken(builder, param, config);
            return await RequestResult(config, builder, ParameterParserType.Json, Success);
        }

        /// <summary>
        /// 配置获取访问令牌
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">访问令牌参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGetToken(AuthorizationParameterBuilder builder, AccessTokenParam param, WechatAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AccessTokenUrl)
                .AppId(config.AppId)
                .Secret(config.AppKey)
                .Code(param.Code)
                .GrantType(OAuthConst.AuthorizationCode);
        }

        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        /// <returns></returns>
        public override async Task<AuthorizationResult> RefreshTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentNullException(nameof(token));
            }
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config);
            var builder = new AuthorizationParameterBuilder();
            ConfigRefreshToken(builder, token, config);
            return await RequestResult(config, builder, ParameterParserType.Json, Success);
        }

        /// <summary>
        /// 配置刷新令牌
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="token">访问令牌参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigRefreshToken(AuthorizationParameterBuilder builder, string token, WechatAuthorizationConfig config)
        {
            builder.GatewayUrl(config.RefreshTokenUrl)
                .AppId(config.AppId)
                .GrantType(OAuthConst.RefreshToken)
                .RefreshToken(token);
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="param">授权用户参数</param>
        /// <returns></returns>
        public async Task<WechatAuthorizationUserInfoResult> GetUserInfoAsync(WechatAuthorizationUserRequest param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param.ToParam());
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(GetUserInfoUrl)
                .AccessToken(param.AccessToken)
                .OpenId(param.OpenId)
                .Add("lang", param.Lang);

            var result = await RequestResult(config, builder, ParameterParserType.Json,
                (t) => t.HasKey("openid"));
            return result.Success ? result.Result.ToObject<WechatAuthorizationUserInfoResult>() : null;
        }
    }
}
