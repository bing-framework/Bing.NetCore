using System;
using System.Web;
using Bing.Utils.Parameters;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权参数生成器
    /// </summary>
    public class AuthorizationParameterBuilder
    {
        /// <summary>
        /// 参数生成器
        /// </summary>
        private readonly UrlParameterBuilder _builder;

        /// <summary>
        /// 网关地址
        /// </summary>
        private string _gatewayUrl;

        /// <summary>
        /// 初始化一个<see cref="AuthorizationParameterBuilder"/>类型的实例
        /// </summary>
        public AuthorizationParameterBuilder()
        {
            _builder = new UrlParameterBuilder();
        }

        /// <summary>
        /// 设置网关地址
        /// </summary>
        /// <param name="url">网关地址</param>
        /// <returns></returns>
        public AuthorizationParameterBuilder GatewayUrl(string url)
        {
            _gatewayUrl = url;
            return this;
        }

        /// <summary>
        /// 设置客户端ID
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <returns></returns>
        public AuthorizationParameterBuilder ClientId(string clientId)
        {
            _builder.Add(OAuthConst.ClientId, clientId);
            return this;
        }

        /// <summary>
        /// 设置客户端密钥
        /// </summary>
        /// <param name="clientSecret">客户端密钥</param>
        /// <returns></returns>
        public AuthorizationParameterBuilder ClientSecret(string clientSecret)
        {
            _builder.Add(OAuthConst.ClientSecret, clientSecret);
            return this;
        }

        /// <summary>
        /// 设置权限范围
        /// </summary>
        /// <param name="scope">权限范围</param>
        /// <returns></returns>
        public AuthorizationParameterBuilder Scope(string scope)
        {
            _builder.Add(OAuthConst.Scope, scope);
            return this;
        }

        /// <summary>
        /// 设置客户端状态
        /// </summary>
        /// <param name="state">客户端状态</param>
        /// <returns></returns>
        public AuthorizationParameterBuilder State(string state)
        {
            _builder.Add(OAuthConst.State, state);
            return this;
        }

        /// <summary>
        /// 设置响应类型
        /// </summary>
        /// <param name="code">响应类型</param>
        /// <returns></returns>
        public AuthorizationParameterBuilder ResponseType(string code)
        {
            _builder.Add(OAuthConst.ResponseType, code);
            return this;
        }

        /// <summary>
        /// 设置授权类型
        /// </summary>
        /// <param name="code">授权类型</param>
        /// <returns></returns>
        public AuthorizationParameterBuilder GrantType(string code)
        {
            _builder.Add(OAuthConst.GrantType, code);
            return this;
        }

        /// <summary>
        /// 设置重定向地址
        /// </summary>
        /// <param name="url">重定向地址</param>
        /// <returns></returns>
        public AuthorizationParameterBuilder RedirectUri(string url)
        {
            _builder.Add(OAuthConst.RedirectUri, HttpUtility.UrlEncode(url));
            return this;
        }

        /// <summary>
        /// 设置授权码
        /// </summary>
        /// <param name="code">授权码</param>
        /// <returns></returns>
        public AuthorizationParameterBuilder Code(string code)
        {
            _builder.Add(OAuthConst.Code, code);
            return this;
        }

        /// <summary>
        /// 设置刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        /// <returns></returns>
        public AuthorizationParameterBuilder RefreshToken(string token)
        {
            _builder.Add(OAuthConst.RefreshToken, token);
            return this;
        }

        /// <summary>
        /// 输出结果
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(_gatewayUrl))
            {
                throw new ArgumentNullException(nameof(_gatewayUrl));
            }

            return _builder.JoinUrl(_gatewayUrl);
        }
    }
}
