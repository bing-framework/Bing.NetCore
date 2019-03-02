using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.QQ.Configs;
using Bing.Utils.Json;

namespace Bing.Biz.OAuthLogin.QQ
{
    /// <summary>
    /// QQ授权提供程序
    /// </summary>
    public class QQAuthorizationProvider : AuthorizationProviderBase<IQQAuthorizationConfigProvider, QQAuthorizationConfig>, IQQAuthorizationProvider
    {
        /// <summary>
        /// PC端授权地址
        /// </summary>
        internal const string PcAuthorizationUrl = "https://graph.qq.com/oauth2.0/authorize";

        /// <summary>
        /// PC端获取访问令牌地址
        /// </summary>
        internal const string PcAccessTokenUrl = "https://graph.qq.com/oauth2.0/token";

        /// <summary>
        /// 跟踪日志名
        /// </summary>
        internal const string TraceLogName = "QQTraceLog";

        /// <summary>
        /// 初始化一个<see cref="QQAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">QQ授权配置提供程序</param>
        public QQAuthorizationProvider(IQQAuthorizationConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected override void Config(AuthorizationParameterBuilder builder, AuthorizationParam param, QQAuthorizationConfig config)
        {
            builder.GatewayUrl(PcAuthorizationUrl)
                .ClientId(config.AppId)
                .ResponseType(param.ResponseType)
                .State(param.State)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri);
        }        

        /// <summary>
        /// 获取访问令牌
        /// </summary>
        /// <param name="param">访问令牌参数</param>
        /// <returns></returns>
        public virtual async Task<AccessTokenResult> GetTokenAsync(AccessTokenParam param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param);
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(PcAccessTokenUrl)
                .GrantType(OAuthConst.AuthorizationCode)
                .ClientId(config.AppId)
                .ClientSecret(config.AppKey)
                .Code(param.Code)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri);
            var result = await Request(builder);
            return ToObject(result);
        }

        /// <summary>
        /// 刷新访问令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        /// <returns></returns>
        public virtual async Task<AccessTokenResult> RefreshTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentNullException(nameof(token));
            }
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config);
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(PcAccessTokenUrl)
                .GrantType(OAuthConst.RefreshToken)
                .ClientId(config.AppId)
                .ClientSecret(config.AppKey)
                .RefreshToken(token);
            var result = await Request(builder);
            return ToObject(result);
        }

        /// <summary>
        /// 转换成对象
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        private AccessTokenResult ToObject(string value)
        {
            var dict = new Dictionary<string, object>();
            foreach (var item in value.Split('&'))
            {
                var keyValue = item.Split('=');
                if (keyValue.Length == 2)
                {
                    dict[keyValue[0]] = keyValue[1];
                }
            }

            return dict.ToJson().ToObject<AccessTokenResult>();
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
    }
}
