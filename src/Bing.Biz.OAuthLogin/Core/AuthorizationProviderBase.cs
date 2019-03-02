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
    public abstract class AuthorizationProviderBase : IAuthorizationProvider
    {
        /// <summary>
        /// 生成授权地址
        /// </summary>
        /// <param name="param">授权参数</param>
        /// <returns></returns>
        public abstract Task<string> GenerateUrlAsync(AuthorizationParam param);

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="config">授权配置</param>
        protected void Validate(IAuthorizationConfig config)
        {
            config.CheckNotNull(nameof(config));
            config.Validate();
        }

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="config">授权配置</param>
        /// <param name="param">授权参数</param>
        protected void Validate(IAuthorizationConfig config, AuthorizationParam param)
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
        /// <param name="param">访问令牌参数</param>
        protected void Validate(IAuthorizationConfig config, AccessTokenParam param)
        {
            param.CheckNotNull(nameof(param));
            param.Validate();
            Validate(config);
            ValidateParam(param);
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
        /// 验证参数
        /// </summary>
        /// <param name="param">授权参数</param>
        protected virtual void ValidateParam(AuthorizationParam param) { }

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="param">访问令牌参数</param>
        protected virtual void ValidateParam(AccessTokenParam param) { }

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
    }
}
