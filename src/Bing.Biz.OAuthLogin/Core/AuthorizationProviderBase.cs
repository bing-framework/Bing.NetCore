using System;
using System.Threading.Tasks;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Logs;
using Bing.Logs.Extensions;
using Bing.Utils.Parameters.Parsers;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权提供程序基类
    /// </summary>
    public abstract class AuthorizationProviderBase<TAuthorizationConfigProvider, TAuthorizationConfig> : IAuthorizationProvider
        where TAuthorizationConfigProvider : class, IAuthorizationConfigProvider<TAuthorizationConfig>
        where TAuthorizationConfig : class, IAuthorizationConfig
    {
        /// <summary>
        /// 配置提供程序
        /// </summary>
        protected readonly TAuthorizationConfigProvider ConfigProvider;

        protected AuthorizationProviderBase(TAuthorizationConfigProvider provider)
        {
            provider.CheckNotNull(nameof(provider));
            ConfigProvider = provider;
        }

        #region GenerateUrlAsync(生成授权地址)

        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="param">授权参数</param>
        /// <returns></returns>
        public virtual async Task<string> GenerateUrlAsync(AuthorizationParam param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param);
            var builder = new AuthorizationParameterBuilder();
            ConfigGenerateUrl(builder, param, config);
            return HandlerUrl(builder);
        }

        /// <summary>
        /// 处理授权地址
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <returns></returns>
        protected virtual string HandlerUrl(AuthorizationParameterBuilder builder)
        {
            return builder.ToString();
        }

        #endregion

        #region GetTokenAsync(获取访问令牌)

        /// <summary>
        /// 获取访问令牌
        /// </summary>
        /// <param name="param">访问令牌参数</param>
        /// <returns></returns>
        public virtual async Task<AuthorizationResult> GetTokenAsync(AccessTokenParam param)
        {
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config, param);
            var builder = new AuthorizationParameterBuilder();
            ConfigGetToken(builder, param, config);
            return await RequestResult(config, builder, ParameterParserType.Url, Success);
        }

        #endregion

        #region RefreshTokenAsync(刷新令牌)

        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <param name="token">刷新令牌</param>
        /// <returns></returns>
        public virtual async Task<AuthorizationResult> RefreshTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentNullException(nameof(token));
            }
            var config = await ConfigProvider.GetConfigAsync();
            Validate(config);
            var builder = new AuthorizationParameterBuilder();
            ConfigRefreshToken(builder, token, config);
            return await RequestResult(config, builder, ParameterParserType.Url, Success);
        }

        #endregion

        #region Validate(验证)

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="config">授权配置</param>
        /// <param name="param">授权参数</param>
        protected void Validate(TAuthorizationConfig config, AuthorizationParam param)
        {
            param.CheckNotNull(nameof(param));
            param.Validate();
            Validate(config);
            ValidateParam(param);
        }

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="config">授权配置</param>
        protected void Validate(TAuthorizationConfig config)
        {
            config.CheckNotNull(nameof(config));
            config.Validate();
        }

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="config">授权配置</param>
        /// <param name="param">访问令牌参数</param>
        protected void Validate(TAuthorizationConfig config, AccessTokenParam param)
        {
            param.CheckNotNull(nameof(param));
            param.Validate();
            Validate(config);
            ValidateParam(param);
        }

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="config">授权配置</param>
        /// <param name="param">授权用户参数</param>
        protected void Validate(TAuthorizationConfig config, AuthorizationUserParam param)
        {
            param.CheckNotNull(nameof(param));
            param.Validate();
            Validate(config);
            ValidateParam(param);
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="param">授权参数</param>
        protected virtual void ValidateParam(AuthorizationParam param)
        {
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="param">访问令牌参数</param>
        protected virtual void ValidateParam(AccessTokenParam param)
        {
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="param">授权用户参数</param>
        protected virtual void ValidateParam(AuthorizationUserParam param)
        {
        }

        #endregion

        #region Config(配置)

        /// <summary>
        /// 配置生成授权地址
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected virtual void ConfigGenerateUrl(AuthorizationParameterBuilder builder, AuthorizationParam param,
            TAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AuthorizationUrl)
                .ClientId(config.AppId)
                .ResponseType(param.ResponseType)
                .State(param.State)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri);
        }

        /// <summary>
        /// 配置获取访问令牌
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">访问令牌参数</param>
        /// <param name="config">授权配置</param>
        protected virtual void ConfigGetToken(AuthorizationParameterBuilder builder, AccessTokenParam param,
            TAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AccessTokenUrl)
                .GrantType(OAuthConst.AuthorizationCode)
                .ClientId(config.AppId)
                .ClientSecret(config.AppKey)
                .Code(param.Code)
                .RedirectUri(string.IsNullOrWhiteSpace(param.RedirectUri) ? config.CallbackUrl : param.RedirectUri);
        }

        /// <summary>
        /// 配置刷新令牌
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="token">访问令牌参数</param>
        /// <param name="config">授权配置</param>
        protected virtual void ConfigRefreshToken(AuthorizationParameterBuilder builder, string token,
            TAuthorizationConfig config)
        {
            builder.GatewayUrl(config.AccessTokenUrl)
                .GrantType(OAuthConst.RefreshToken)
                .ClientId(config.AppId)
                .ClientSecret(config.AppKey)
                .RefreshToken(token);
        }

        #endregion

        #region Request(请求)

        /// <summary>
        /// 请求结果
        /// </summary>
        /// <param name="config">授权配置</param>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="type">参数解析器类型</param>
        /// <param name="success">请求成功条件</param>
        /// <returns></returns>
        protected virtual async Task<AuthorizationResult> RequestResult(TAuthorizationConfig config,
            AuthorizationParameterBuilder builder, ParameterParserType type, Func<AuthorizationResult, bool> success)
        {
            var result = new AuthorizationResult(await Request(builder), success, type);
            result.Parameter = builder.ToString();
            result.Message = GetMessage(result);
            WriteLog(config, builder, result);
            return result;
        }

        /// <summary>
        /// Post请求结果
        /// </summary>
        /// <param name="config">授权配置</param>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="type">参数解析器类型</param>
        /// <param name="success">请求成功条件</param>
        /// <returns></returns>
        protected virtual async Task<AuthorizationResult> PostRequestResult(TAuthorizationConfig config,
            AuthorizationParameterBuilder builder, ParameterParserType type, Func<AuthorizationResult, bool> success)
        {
            var result = new AuthorizationResult(await PostRequest(builder), success, type);
            result.Parameter = builder.ToString();
            result.Message = GetMessage(result);
            WriteLog(config, builder, result);
            return result;
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <returns></returns>
        protected virtual async Task<string> Request(AuthorizationParameterBuilder builder)
        {
            return await Web.Client().Get(builder.ToString()).ResultAsync();
        }

        /// <summary>
        /// 发送Post请求
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <returns></returns>
        protected virtual async Task<string> PostRequest(AuthorizationParameterBuilder builder)
        {
            return await Web.Client().Post(builder.GetGatewayUrl()).Data(builder.GetDictionary()).ResultAsync();
        }

        /// <summary>
        /// 授权结果是否成功
        /// </summary>
        /// <param name="result">授权结果</param>
        /// <returns></returns>
        protected virtual bool Success(AuthorizationResult result)
        {
            return result.HasKey("access_token");
        }

        /// <summary>
        /// 获取错误消息
        /// </summary>
        /// <param name="result">授权结果</param>
        /// <returns></returns>
        protected virtual string GetMessage(AuthorizationResult result)
        {
            return result.Success ? string.Empty : result.Raw;
        }

        #endregion

        #region Log(日志)

        /// <summary>
        /// 获取跟踪日志名
        /// </summary>
        /// <returns></returns>
        protected abstract string GetTraceLogName();

        /// <summary>
        /// 获取授权方式
        /// </summary>
        /// <returns></returns>
        protected abstract OAuthWay GetOAuthWay();

        /// <summary>
        /// 获取日志操作
        /// </summary>
        /// <returns></returns>
        private ILog GetLog()
        {
            try
            {
                return Log.GetLog(GetTraceLogName());
            }
            catch
            {
                return Log.Null;
            }
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="config">授权配置</param>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="result">授权结果</param>
        protected void WriteLog(TAuthorizationConfig config, AuthorizationParameterBuilder builder,
            AuthorizationResult result)
        {
            var log = GetLog();
            if (log.IsTraceEnabled == false)
            {
                return;
            }

            log.Class(GetType().FullName)
                .Caption("OAuth授权登录")
                .Content($"授权渠道 : {GetOAuthWay().Description()}")
                .Content("请求参数")
                .Content(builder.ToString())
                .Content()
                .Content("返回结果:")
                .Content(result.GetDictionary())
                .Content("原始响应：")
                .Content(result.Raw)
                .Trace();
        }

        #endregion
    }
}
