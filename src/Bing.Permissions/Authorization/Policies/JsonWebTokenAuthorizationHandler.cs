using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Extensions;
using Bing.Permissions.Identity.JwtBearer;
using Bing.Security;
using Bing.Utils.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bing.Permissions.Authorization.Policies
{
    /// <summary>
    /// Jwt授权处理器
    /// </summary>
    public class JsonWebTokenAuthorizationHandler : AuthorizationHandler<JsonWebTokenAuthorizationRequirement>
    {
        /// <summary>
        /// Jwt选项配置
        /// </summary>
        private readonly JwtOptions _options;

        /// <summary>
        /// Jwt令牌校验器
        /// </summary>
        private readonly IJsonWebTokenValidator _tokenValidator;

        /// <summary>
        /// Jwt令牌存储器
        /// </summary>
        private readonly IJsonWebTokenStore _tokenStore;

        /// <summary>
        /// 初始化一个<see cref="JsonWebTokenAuthorizationHandler"/>类型的实例
        /// </summary>
        /// <param name="options">Jwt选项配置</param>
        /// <param name="tokenValidator">Jwt令牌校验器</param>
        /// <param name="tokenStore">Jwt令牌存储器</param>
        public JsonWebTokenAuthorizationHandler(
            IOptions<JwtOptions> options
            , IJsonWebTokenValidator tokenValidator
            , IJsonWebTokenStore tokenStore)
        {
            _options = options.Value;
            _tokenValidator = tokenValidator;
            _tokenStore = tokenStore;
        }

        /// <summary>
        /// 重载异步处理
        /// </summary>
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, JsonWebTokenAuthorizationRequirement requirement)
        {
            if (_options.ThrowEnabled)
            {
                await ThrowExceptionHandleAsync(context, requirement);
                return;
            }
            await ResultHandleAsync(context, requirement);
        }

        /// <summary>
        /// 抛异常处理方式
        /// </summary>
        protected virtual async Task ThrowExceptionHandleAsync(AuthorizationHandlerContext context,
            JsonWebTokenAuthorizationRequirement requirement)
        {
            var httpContext = (context.Resource as AuthorizationFilterContext)?.HttpContext;
            if (httpContext == null)
                return;
            // 未登录而被拒绝
            var result = httpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader);
            if (!result || string.IsNullOrWhiteSpace(authorizationHeader))
                throw new UnauthorizedAccessException("未授权，请传递Header头的Authorization参数");
            var token = authorizationHeader.ToString().Split(' ').Last().Trim();
            if (!await _tokenStore.ExistsTokenAsync(token))
                throw new UnauthorizedAccessException("未授权，无效参数");
            if (!_tokenValidator.Validate(token, _options, requirement.ValidatePayload))
                throw new UnauthorizedAccessException("验证失败，请查看传递的参数是否正确或是否有权限访问该地址。");
            if (_options.SingleDeviceEnabled)
            {
                var payload = GetPayload(token);
                var bindDeviceInfo = await _tokenStore.GetUserDeviceTokenAsync(payload["sub"], payload["clientType"]);
                if (bindDeviceInfo.DeviceId != payload["clientId"])
                    throw new UnauthorizedAccessException("该账号已在其它设备登录");
            }
            var isAuthenticated = httpContext.User.Identity.IsAuthenticated;
            if (!isAuthenticated)
                return;
            context.Succeed(requirement);
        }

        /// <summary>
        /// 结果处理方式
        /// </summary>
        protected virtual async Task ResultHandleAsync(AuthorizationHandlerContext context,
            JsonWebTokenAuthorizationRequirement requirement)
        {
            var filterContext = (context.Resource as AuthorizationFilterContext);
            var httpContext = filterContext?.HttpContext;
            if (httpContext == null)
                return;
            // 未登录而被拒绝
            var result = httpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader);
            if (!result || string.IsNullOrWhiteSpace(authorizationHeader))
            {
                filterContext.Result = new AuthorizeResult(AuthorizationStatus.Unauthorized.Value(),
                    AuthorizationStatus.Unauthorized.Description());
                context.Succeed(requirement);
                return;
            }

            var token = authorizationHeader.ToString().Split(' ').Last().Trim();
            if (!await _tokenStore.ExistsTokenAsync(token))
            {
                filterContext.Result = new AuthorizeResult(AuthorizationStatus.Unauthorized.Value(),
                    AuthorizationStatus.Unauthorized.Description());
                context.Succeed(requirement);
                return;
            }

            if (!_tokenValidator.Validate(token, _options, requirement.ValidatePayload))
            {
                filterContext.Result = new AuthorizeResult(AuthorizationStatus.Unauthorized.Value(),
                    AuthorizationStatus.Unauthorized.Description());
                context.Succeed(requirement);
                return;
            }

            // 登录超时
            var accessToken = await _tokenStore.GetTokenAsync(token);
            if (accessToken.IsExpired())
            {
                filterContext.Result = new AuthorizeResult(AuthorizationStatus.LoginTimeout.Value(),
                    AuthorizationStatus.LoginTimeout.Description());
                context.Succeed(requirement);
                return;
            }

            // 单设备登录
            if (_options.SingleDeviceEnabled)
            {
                var payload = GetPayload(token);
                var bindDeviceInfo = await _tokenStore.GetUserDeviceTokenAsync(payload["sub"], payload["clientType"]);
                if (bindDeviceInfo.DeviceId != payload["clientId"])
                {
                    filterContext.Result = new AuthorizeResult(AuthorizationStatus.OtherDeviceLogin.Value(),
                        AuthorizationStatus.OtherDeviceLogin.Description());
                    context.Succeed(requirement);
                    return;
                }
            }

            var isAuthenticated = httpContext.User.Identity.IsAuthenticated;
            if (!isAuthenticated)
                return;
            context.Succeed(requirement);
        }

        /// <summary>
        /// 获取Payload
        /// </summary>
        /// <param name="encodeJwt">加密后的Jwt令牌</param>
        private IDictionary<string, string> GetPayload(string encodeJwt)
        {
            var jwtArray = encodeJwt.Split('.');
            if (jwtArray.Length < 3)
                throw new ArgumentException($"非有效Jwt令牌");
            var payload = JsonHelper.ToObject<Dictionary<string, string>>(Base64UrlEncoder.Decode(jwtArray[1]));
            return payload;
        }
    }
}
