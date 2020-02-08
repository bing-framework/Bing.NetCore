using System;
using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.QQ.Configs;
using Bing.Utils.Json;
using Bing.Utils.Parameters.Parsers;

namespace Bing.Biz.OAuthLogin.QQ
{
    /// <summary>
    /// QQ授权提供程序
    /// </summary>
    public class QQAuthorizationProvider : AuthorizationProviderBase<IQQAuthorizationConfigProvider, QQAuthorizationConfig>, IQQAuthorizationProvider
    {
        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "QQTraceLog";

        /// <summary>
        /// 获取用户OpenId地址
        /// </summary>
        internal const string GetOpenIdUrl = "https://graph.qq.com/oauth2.0/me";

        /// <summary>
        /// 获取用户信息地址
        /// </summary>
        internal const string GetUserInfoUrl = "https://graph.qq.com/user/get_user_info";

        /// <summary>
        /// 初始化一个<see cref="QQAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">QQ授权配置提供程序</param>
        public QQAuthorizationProvider(IQQAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="request">QQ授权请求</param>
        /// <returns></returns>
        public async Task<string> GenerateUrlAsync(QQAuthorizationRequest request) => await GenerateUrlAsync(request.ToParam());

        /// <summary>
        /// 获取跟踪日志名
        /// </summary>
        /// <returns></returns>
        protected override string GetTraceLogName() => TraceLogName;

        /// <summary>
        /// 获取授权方式
        /// </summary>
        /// <returns></returns>
        protected override OAuthWay GetOAuthWay() => OAuthWay.QQ;

        /// <summary>
        /// 获取用户OpenId
        /// </summary>
        /// <param name="token">授权令牌</param>
        /// <returns></returns>
        public async Task<string> GetOpenIdAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentNullException(nameof(token));
            }
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config);
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(GetOpenIdUrl)
                .AccessToken(token);
            var result = await RequestResult(config, builder, ParameterParserType.Jsonp, (t) => t.HasKey("openid"));
            if (!config.AppId.Equals(result.GetValue(OAuthConst.ClientId)))
            {
                throw new ArgumentException("客户端ID不一致");
            }
            return result.GetValue("openid");
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="param">授权用户参数</param>
        /// <returns></returns>
        public async Task<QQAuthorizationUserInfoResult> GetUserInfoAsync(QQAuthorizationUserRequest param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param.ToParam());
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(GetUserInfoUrl)
                .AccessToken(param.AccessToken)
                .Add("oauth_consumer_key", config.AppId)
                .OpenId(param.OpenId);
            var result = await RequestResult(config, builder, ParameterParserType.Json,
                (t) => t.GetValue("ret") == "0");
            return result.Success ? result.Result.ToObject<QQAuthorizationUserInfoResult>() : null;
        }
    }
}
