using System.Threading.Tasks;
using Bing.Logs;
using Bing.Logs.Extensions;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

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
            Config(builder, param, config);
            return HandlerUrl(builder);
        }

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
        /// 配置
        /// </summary>
        /// <param name="builder">授权参数生成器</param>
        /// <param name="param">授权参数</param>
        /// <param name="config">授权配置</param>
        protected virtual void Config(AuthorizationParameterBuilder builder, AuthorizationParam param, TAuthorizationConfig config)
        {
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

        //protected virtual async Task<AuthorizationResult> RequestResult(IAuthorizationConfig config,
        //    AuthorizationParameterBuilder builder)
        //{

        //}

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

        protected void WriteLog(AuthorizationParameterBuilder builder)
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
                .Trace();
        }

        //protected virtual string GetResult()
        //{

        //}
    }
}
