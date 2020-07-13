using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc
{
    /// <summary>
    /// WebApi的区域控制器基类
    /// </summary>
    [ApiController]
    [Route("api/[area]/[controller]/[action]")]
    public abstract class AreaApiControllerBase : ControllerBase
    {
    }
}
