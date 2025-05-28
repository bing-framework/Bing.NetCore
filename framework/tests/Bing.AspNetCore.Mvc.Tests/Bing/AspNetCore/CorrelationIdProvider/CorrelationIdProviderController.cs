using Bing.AspNetCore.Mvc;
using Bing.Tracing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.CorrelationIdProvider;

[Route("api/correlation")]
public class CorrelationIdProviderController : ApiControllerBase
{
    /// <summary>
    /// 测试 - 获取关联ID
    /// </summary>
    [HttpGet]
    [Route("get")]
    public string Get()
    {
        return this.HttpContext.RequestServices.GetRequiredService<ICorrelationIdProvider>().Get();
    }
}
