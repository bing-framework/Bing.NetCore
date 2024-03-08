using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc.ExceptionHandling;

/// <summary>
/// 异常测试 控制器
/// </summary>
[ApiController]
//[Route("api/exception-test")]
[Route("api/ExceptionTest")]
public class ExceptionTestController : ControllerBase
{
    /// <summary>
    /// 测试 - 用户友好异常 - 1
    /// </summary>
    [HttpGet]
    //[HttpGet("UserFriendlyException1")]
    public IActionResult UserFriendlyException1()
    {
        throw new UserFriendlyException("This is a sample exception!");
    }
}
