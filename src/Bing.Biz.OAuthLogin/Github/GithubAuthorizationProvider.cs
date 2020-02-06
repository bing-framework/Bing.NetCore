using System;
using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Github.Configs;
using Bing.Helpers;
using Bing.Utils.Json;
using Bing.Utils.Parameters.Parsers;

namespace Bing.Biz.OAuthLogin.Github
{
    /// <summary>
    /// Github授权提供程序
    /// </summary>
    public class GithubAuthorizationProvider : AuthorizationProviderBase<IGithubAuthorizationConfigProvider, GithubAuthorizationConfig>, IGithubAuthorizationProvider
    {
        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "GithubTraceLog";

        /// <summary>
        /// 获取用户信息Url
        /// </summary>
        internal const string GetUserInfoUrl = "https://api.github.com/user";

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
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, GithubAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AuthorizationUrl)
                .ClientId(config.AppId)
                .Scope(param.Scope)
                .State(param.State)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
                .Add("allow_signup", param.AllowSignup ? "true" : "false");
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

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="param">授权用户参数</param>
        /// <returns></returns>
        public async Task<GithubAuthorizationUserInfoResult> GetUserInfoAsync(GithubAuthorizationUserRequest param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param.ToParam());
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(GetUserInfoUrl)
                .AccessToken(param.AccessToken);

            var result = await RequestResult(config, builder, ParameterParserType.Json,
                (t) => t.HasKey("id"));
            return result.Success ? result.Result.ToObject<GithubAuthorizationUserInfoResult>() : null;
        }

        /// <summary>
        /// 请求结果
        /// </summary>
        /// <param name="config">授权配置</param>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="type">参数解析器类型</param>
        /// <param name="success">请求成功条件</param>
        /// <returns></returns>
        protected override async Task<AuthorizationResult> RequestResult(GithubAuthorizationConfig config, AuthorizationParameterBuilder builder, ParameterParserType type,
            Func<AuthorizationResult, bool> success)
        {
            var result = new AuthorizationResult(await Request(builder, config.ApplicationName), success, type);
            result.Parameter = builder.ToString();
            result.Message = GetMessage(result);
            WriteLog(config, builder, result);
            return result;
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="appName">应用名称</param>
        /// <returns></returns>
        private async Task<string> Request(AuthorizationParameterBuilder builder, string appName)
        {
            return await Web.Client().Get(builder.ToString()).Header("User-Agent", appName).ResultAsync();
        }
    }
}
