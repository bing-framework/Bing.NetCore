using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bing.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Security.Claims
{
    /// <summary>
    /// 声明映射中间件
    /// </summary>
    public class BingClaimsMapMiddleware : IMiddleware
    {
        /// <summary>
        /// 方法
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// 初始化一个<see cref="BingClaimsMapMiddleware"/>类型的实例
        /// </summary>
        /// <param name="next">方法</param>
        public BingClaimsMapMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// 执行中间件拦截逻辑
        /// </summary>
        /// <param name="context">Http上下文</param>
        public async Task InvokeAsync(HttpContext context)
        {
            var currentPrincipalAccessor = context
                .RequestServices.GetRequiredService<ICurrentPrincipalAccessor>();
            var mapOptions = context
                .RequestServices.GetRequiredService<IOptions<BingClaimsMapOptions>>().Value;
            var mapClaims = currentPrincipalAccessor
                    .Principal
                    .Claims
                    .Where(claim => mapOptions.Maps.Keys.Contains(claim.Type));
            currentPrincipalAccessor
                .Principal
                .AddIdentity(
                    new ClaimsIdentity(
                        mapClaims
                            .Select(
                                claim => new Claim(
                                    mapOptions.Maps[claim.Type](),
                                    claim.Value,
                                    claim.ValueType,
                                    claim.Issuer
                                )
                            )
                    )
                );

            await _next(context);
        }
    }
}
