using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc
{
    /// <summary>
    /// WebApi 控制器基类
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ApiControllerBase : BingControllerBase
    {
    }
}
