using Bing.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc.ExceptionHandling;

/// <summary>
/// 异常 - 测试 控制器
/// </summary>
[Route("api/exception-test")]
public class ExceptionTestController : ApiControllerBase
{
    /// <summary>
    /// 测试 - 用户友好异常 - void
    /// </summary>
    [HttpGet("UserFriendlyException1")]
    public void UserFriendlyException1()
    {
        throw new UserFriendlyException("This is a sample exception!");
    }

    /// <summary>
    /// 测试 - 用户友好异常 - ActionResult
    /// </summary>
    [HttpGet("UserFriendlyException2")]
    public ActionResult UserFriendlyException2()
    {
        throw new UserFriendlyException("This is a sample exception!");
    }

    /// <summary>
    /// 测试 - 授权异常
    /// </summary>
    [HttpGet("BingAuthorizationException")]
    public void BingAuthorizationException()
    {
        throw new BingAuthorizationException("This is a sample exception!");
    }
}
