using System;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Webs.Controllers
{
    /// <summary>
    /// WebApi的区域控制器基类
    /// </summary>
    [ApiController]
    [Route("api/[area]/[controller]/[action]")]
    [Obsolete("请使用Bing.AspNetCore.Mvc.AreaApiControllerBase")]
    public abstract class AreaApiControllerBase : Controller
    {
    }
}
