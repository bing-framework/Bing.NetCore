using System;
using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Gitee.Configs;
using Bing.Utils.Helpers;
using Bing.Utils.Json;
using Bing.Utils.Parameters.Parsers;

namespace Bing.Biz.OAuthLogin.Gitee
{
    /// <summary>
    /// Gitee 授权提供程序
    /// </summary>
    public class GiteeAuthorizationProvider: AuthorizationProviderBase<IGiteeAuthorizationConfigProvider,GiteeAuthorizationConfig>,IGiteeAuthorizationProvider
    {
        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "GiteeTraceLog";

        /// <summary>
        /// 获取用户信息Url
        /// </summary>
        internal const string GetUserInfoUrl = "https://gitee.com/api/v5/user";

        /// <summary>
        /// 初始化一个<see cref="GiteeAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">Gitee 授权配置提供程序</param>
        public GiteeAuthorizationProvider(IGiteeAuthorizationConfigProvider provider) : base(provider)
        {
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Gitee;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">Gitee 授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(GiteeAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());

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
        protected override void ConfigGetToken(AuthorizationParameterBuilder builder, AccessTokenParam param, GiteeAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AccessTokenUrl)
                .GrantType(OAuthConst.AuthorizationCode)
                .Code(param.Code)
                .ClientId(config.AppId)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri,
                    false)
                .ClientSecret(config.AppKey);
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="param">授权用户参数</param>
        /// <returns></returns>
        public async Task<GiteeAuthorizationUserInfoResult> GetUserInfoAsync(GiteeAuthorizationUserRequest param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param.ToParam());
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(GetUserInfoUrl)
                .AccessToken(param.AccessToken);

            var result = await RequestResult(config, builder, ParameterParserType.Json,
                (t) => t.HasKey("id"));
            return result.Success ? result.Result.ToObject<GiteeAuthorizationUserInfoResult>() : null;
        }
    }
}
