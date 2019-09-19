using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Permissions.Identity.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Bing.Permissions.Authorization.Middlewares
{
    /// <summary>
    /// JWT客户授权中间件
    /// </summary>
    public class JsonWebTokenCustomerAuthorizeMiddleware
    {
        /// <summary>
        /// 方法
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// Jwt选项配置
        /// </summary>
        private readonly JwtOptions _options;

        /// <summary>
        /// 校验负载
        /// </summary>
        private readonly Func<IDictionary<string, string>, JwtOptions, bool> _validatePayload;

        /// <summary>
        /// 匿名访问路径列表
        /// </summary>
        private readonly IList<string> _anonymousPathList;

        /// <summary>
        /// Jwt令牌校验器
        /// </summary>
        private readonly IJsonWebTokenValidator _tokenValidator;

        /// <summary>
        /// 初始化一个<see cref="JsonWebTokenCustomerAuthorizeMiddleware"/>类型的实例
        /// </summary>
        /// <param name="next">方法</param>
        /// <param name="options">Jwt选项配置</param>
        /// <param name="tokenValidator">Jwt令牌校验器</param>
        /// <param name="validatePayload">校验负载</param>
        /// <param name="anonymousPathList">匿名访问路径列表</param>
        public JsonWebTokenCustomerAuthorizeMiddleware(
            RequestDelegate next
            , IOptions<JwtOptions> options
            , IJsonWebTokenValidator tokenValidator
            , Func<IDictionary<string, string>, JwtOptions, bool> validatePayload
            , IList<string> anonymousPathList)
        {
            _next = next;
            _options = options.Value;
            _tokenValidator = tokenValidator;
            _validatePayload = validatePayload;
            _anonymousPathList = anonymousPathList;
        }

        /// <summary>
        /// 执行中间件拦截逻辑
        /// </summary>
        /// <param name="context">Http上下文</param>
        public async Task Invoke(HttpContext context)
        {
            // 如果是匿名访问路径，则直接跳过
            if (_anonymousPathList.Contains(context.Request.Path.Value))
            {
                await _next(context);
                return;
            }

            var result = context.Request.Headers.TryGetValue("Authorization", out var authStr);
            if (!result || string.IsNullOrWhiteSpace(authStr.ToString()))
                throw new UnauthorizedAccessException("未授权，请传递Header头的Authorization参数");
            // 校验以及自定义校验
            result = _tokenValidator.Validate(authStr.ToString().Substring("Bearer ".Length).Trim(), _options,
                _validatePayload);
            if(!result)
                throw new UnauthorizedAccessException("验证失败，请查看传递的参数是否正确或是否有权限访问该地址。");
            await _next(context);
        }
    }
}
