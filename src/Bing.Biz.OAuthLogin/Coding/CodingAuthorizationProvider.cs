using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Coding.Configs;
using Bing.Biz.OAuthLogin.Core;
using Bing.Utils.Json;
using Bing.Utils.Parameters.Parsers;

namespace Bing.Biz.OAuthLogin.Coding
{
    /// <summary>
    /// Coding.NET 授权提供程序
    /// </summary>
    public class CodingAuthorizationProvider: AuthorizationProviderBase<ICodingAuthorizationConfigProvider,CodingAuthorizationConfig>,ICodingAuthorizationProvider
    {
        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "CodingTraceLog";

        /// <summary>
        /// 获取用户信息Url
        /// </summary>
        internal const string GetUserInfoUrl = "https://coding.net/api/account/current_user";

        /// <summary>
        /// 初始化一个<see cref="CodingAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">Coding.NET 授权配置提供程序</param>
        public CodingAuthorizationProvider(ICodingAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param, CodingAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AuthorizationUrl)
                .ClientId(config.AppId)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri)
                .ResponseType(param.ResponseType)
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
        protected override OAuthWay GetOAuthWay() => OAuthWay.Coding;

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">Coding.NET 授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(CodingAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());

        /// <summary>
        /// 配置获取访问令牌
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">访问令牌参数</param>
        /// <param name="config">授权配置</param>
        protected override void ConfigGetToken(AuthorizationParameterBuilder builder, AccessTokenParam param, CodingAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AccessTokenUrl)
                .GrantType(OAuthConst.AuthorizationCode)
                .ClientId(config.AppId)
                .ClientSecret(config.AppKey)
                .Code(param.Code);
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="param">授权用户参数</param>
        /// <returns></returns>
        public async Task<CodingAuthorizationUserInfoResult> GetUserInfoAsync(CodingAuthorizationUserRequest param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param.ToParam());
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(GetUserInfoUrl)
                .AccessToken(param.AccessToken);

            var result = await RequestResult(config, builder, ParameterParserType.Json,
                (t) => t.GetValue("code") == "0");
            return result.Success ? result.GetValue("data").ToObject<CodingAuthorizationUserInfoResult>() : null;
        }
    }
}
