using Microsoft.AspNetCore.Authorization;

namespace Bing.Permissions.Authorization.Policies
{
    /// <summary>
    /// Jwt授权
    /// </summary>
    public class JwtAuthorizeAttribute : AuthorizeAttribute
    {
    }
}
