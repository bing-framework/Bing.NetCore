using System.Threading;
using System.Threading.Tasks;
using Bing.Auditing.Persistence;
using Bing.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Auditing;

/// <summary>
/// 提供受保护的审计日志元数据查询。
/// </summary>
/// <remarks>
/// 端点只返回审计元数据，不返回请求正文、异常正文或实体属性明细；宿主必须显式启用数据库审计查询。
/// </remarks>
[Authorize]
public sealed class AuditLogController : ApiControllerBase
{
    /// <summary>
    /// 审计查询服务。
    /// </summary>
    private readonly IAuditLogQueryService _service;

    /// <summary>
    /// 初始化一个 <see cref="AuditLogController" /> 类型的实例。
    /// </summary>
    /// <param name="service">审计查询服务；未启用查询时为空。</param>
    public AuditLogController(IAuditLogQueryService service = null)
    {
        _service = service;
    }

    /// <summary>
    /// 查询审计日志元数据。
    /// </summary>
    /// <param name="query">租户和过滤条件。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>审计元数据或查询功能未启用时的 404。</returns>
    [HttpGet("~/internal/auditing/logs")]
    public async Task<IActionResult> QueryAsync([FromQuery] AuditLogQuery query,
        CancellationToken cancellationToken)
    {
        if (_service == null)
            return NotFound();
        if (query == null || string.IsNullOrWhiteSpace(query.TenantId))
            return BadRequest();
        var entries = await _service.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        return Success(entries);
    }
}
