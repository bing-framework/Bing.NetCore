using Bing.AspNetCore.Mvc;
using Bing.Tracing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.CorrelationIdProvider;

/// <summary>
/// 提供关联标识测试接口。
/// </summary>
[Route("api/correlation")]
public class CorrelationIdProviderController : ApiControllerBase
{
    /// <summary>
    /// 测试 - 获取关联ID
    /// </summary>
    /// <returns>当前请求的关联标识。</returns>
    [HttpGet]
    [Route("get")]
    public string Get()
    {
        return this.HttpContext.RequestServices.GetRequiredService<ICorrelationIdProvider>().Get();
    }
}
