using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.QQ.Configs;
using Bing.Utils.Extensions;
using Bing.Utils.Json;

namespace Bing.Biz.OAuthLogin.QQ
{
    /// <summary>
    /// QQ授权提供程序
    /// </summary>
    public class QQAuthorizationProvider : AuthorizationProviderBase, IQQAuthorizationProvider
    {
        /// <summary>
        /// 配置提供器
        /// </summary>
        protected readonly IQQAuthorizationConfigProvider ConfigProvider;

        /// <summary>
        /// 初始化一个<see cref="QQAuthorizationProvider"/>类型的实例
        /// </summary>
        /// <param name="provider">QQ授权配置提供器</param>
        public QQAuthorizationProvider(IQQAuthorizationConfigProvider provider)
        {
            provider.CheckNotNull(nameof(provider));
            ConfigProvider = provider;
        }

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="param">授权参数</param>
        /// <returns></returns>
        public override async Task<string> GenerateUrlAsync(AuthorizationParam param)
        {            
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param);
            var builder = new AuthorizationParameterBuilder();
            builder.GatewayUrl(QQConst.PcAuthorizationUrl)
                .ClientId(config.AppId)
                .ResponseType(param.ResponseType)
                .State(param.State)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri);
            return builder.ToString();
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
            builder.GatewayUrl(QQConst.PcAccessTokenUrl)
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
            builder.GatewayUrl(QQConst.PcAccessTokenUrl)
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
        public async Task<string> GenerateUrlAsync(QQAuthorizationRequest request)
        {
            return await GenerateUrlAsync(request.ToParam());
        }

        /// <summary>
        /// 获取跟踪日志名
        /// </summary>
        /// <returns></returns>
        protected override string GetTraceLogName()
        {
            return QQConst.TraceLogName;
        }

        /// <summary>
        /// 授权方式
        /// </summary>
        /// <returns></returns>
        protected override OAuthWay GetOAuthWay()
        {
            return OAuthWay.QQ;
        }
    }
}
