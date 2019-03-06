using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Weibo.Configs;
using Bing.Utils.Helpers;
using Bing.Utils.Json;
using Bing.Utils.Parameters.Parsers;
using Bing.Utils.Webs.Clients;

namespace Bing.Biz.OAuthLogin.Weibo
{
    /// <summary>
    /// 微博授权提供程序
    /// </summary>
    public class WeiboAuthorizationProvider:AuthorizationProviderBase<IWeiboAuthorizationConfigProvider,WeiboAuthorizationConfig>,IWeiboAuthorizationProvider
    {
        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "WeiboTraceLog";

        /// <summary>
        /// 获取用户信息Url
        /// </summary>
        internal const string GetUserInfoUrl = "https://api.weibo.com/2/users/show.json";

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
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, WeiboAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AuthorizationUrl)
                .ClientId(config.AppId)
                .ResponseType(param.ResponseType)
                .State(param.State)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
                .Scope(param.Scope)
                .Add("display", param.Display)
                .Add("forcelogin", param.Forcelogin.ToString().ToLower())
                .Add("language", param.Language);
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
            return await PostRequestResult(config, builder, ParameterParserType.Json, Success);
        }

        /// <summary>
        /// 配置获取访问令牌
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">访问令牌参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGetToken(AuthorizationParameterBuilder builder, AccessTokenParam param, WeiboAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AccessTokenUrl)
                .GrantType(OAuthConst.AuthorizationCode)
                .ClientId(config.AppId)
                .ClientSecret(config.AppKey)
                .Code(param.Code)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri,
                    false);
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="param">授权用户参数</param>
        /// <returns></returns>
        public async Task<WeiboAuthorizationUserInfoResult> GetUserInfoAsync(WeiboAuthorizationUserRequest param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param.ToParam());
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(GetUserInfoUrl)
                .AccessToken(param.AccessToken)
                .Add("uid", param.UnionId)
                .Add("screen_name", param.ScreenName);

            var result = await RequestResult(config, builder, ParameterParserType.Json,
                (t) => t.HasKey("id"));
            return result.Success ? result.Result.ToObject<WeiboAuthorizationUserInfoResult>() : null;
        }
    }
}
