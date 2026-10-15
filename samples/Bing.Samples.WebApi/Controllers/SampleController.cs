using Bing.AspNetCore.Mvc;
using Bing.Samples.WebApi.Modules;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.WebApi.Controllers;

/// <summary>
/// 演示 Bing 控制器基类属性注入和延迟服务解析。
/// </summary>
[ApiController]
[Route("sample")]
public sealed class SampleController : BingControllerBase
{
    /// <summary>
    /// 返回延迟解析的示例业务值。
    /// </summary>
    /// <returns>属性注入成功时返回 HTTP 200。</returns>
    [HttpGet("lazy")]
    public IActionResult Lazy()
    {
        return Ok(new
        {
            Injected = LazyServiceProvider != null,
            ProviderType = LazyServiceProvider?.GetType().FullName,
            Value = LazyServiceProvider?.LazyGetRequiredService<SampleLazyDependency>().Value
        });
    }
}
