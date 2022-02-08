using System.Threading.Tasks;
using Bing.Authorization;
using Bing.DependencyInjection;
using Bing.Exceptions;
using Bing.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.ExceptionHandling
{
    /// <summary>
    /// 授权异常处理器
    /// </summary>
    public class DefaultBingAuthorizationExceptionHandler : IBingAuthorizationExceptionHandler, ITransientDependency
    {
        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="exception">授权异常</param>
        /// <param name="httpContext">Http上下文</param>
        public virtual async Task HandleAsync(BingAuthorizationException exception, HttpContext httpContext)
        {
            var handlerOptions = httpContext.RequestServices
                .GetRequiredService<IOptions<BingAuthorizationExceptionHandlerOptions>>().Value;
            var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;
            var authenticationSchemeProvider =
                httpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();

            AuthenticationScheme scheme = null;
            if (!handlerOptions.AuthenticationScheme.IsNullOrWhiteSpace())
            {
                scheme = await authenticationSchemeProvider.GetSchemeAsync(handlerOptions.AuthenticationScheme);
                if (scheme == null)
                    throw new Warning($"No authentication scheme named {handlerOptions.AuthenticationScheme} was found.");
            }
            else
            {
                if (isAuthenticated)
                {
                    scheme = await authenticationSchemeProvider.GetDefaultForbidSchemeAsync();
                    if (scheme == null)
                        throw new Warning($"There was no DefaultForbidScheme found.");
                }
                else
                {
                    scheme = await authenticationSchemeProvider.GetDefaultChallengeSchemeAsync();
                    if (scheme == null)
                        throw new Warning($"There was no DefaultForbidScheme found.");
                }
            }

            var handlers = httpContext.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
            var handler = await handlers.GetHandlerAsync(httpContext, scheme.Name);
            if (handler == null)
                throw new Warning($"No handler of {scheme.Name} was found.");
            if (isAuthenticated)
                await handler.ForbidAsync(null);
            else
                await handler.ChallengeAsync(null);
        }
    }
}
