using Microsoft.AspNetCore.Authorization;

namespace Bing.AspNetCore.Mvc
{
    /// <summary>
    /// 允许匿名访问API控制器基类
    /// </summary>
    [AllowAnonymous]
    public class AnonymousApiController : ApiControllerBase
    {
    }
}
